using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarSignal.Realtime.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarSignal.Realtime.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarSignal.Realtime.Actor;

/// <summary>
/// Routes live futures trades through the actor-centric bar model and forwards completed bars to the Command actor.
/// Open state and pending handoffs are checkpointed before delivery succeeds. No projection is performed.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001", Justification = "The actor lifecycle owns the barrier registration: OnShutdown awaits the loop and disposes its cancellation source in finally; OnStartup creates a fresh source for restart.")]
public sealed class FuturesTradeSessionBarSignalRealtimeActor(
    IRealtimeActorContext<FuturesTradeSessionBarSignalRealtimeActor> actorContext)
    : BaseEventActor<FuturesTradeSessionBarSignalRealtimeActor>(actorContext, actorContext.Logger), IBatchedRealtimeActor
{
    /// <summary>Gets the Realtime actor mailbox name.</summary>
    public const string ActorName = FuturesTradeSessionBarClosedRealtimeEvent.Actor;

    /// <inheritdoc />
    protected override bool PropagateHandlerFailures => true;

    /// <summary>Applies the scheduler's FIFO group and checkpoints its complete state before any delivery is acknowledged.</summary>
    public async ValueTask HandleBatchAsync(IReadOnlyList<IActorMessage> messages, ActorThreadId threadId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(messages);
        if (messages.Count is < 1 or > 64) throw new ArgumentOutOfRangeException(nameof(messages));
        using var generation = RealtimeGeneration.Enter();
        cancellationToken.ThrowIfCancellationRequested();
        var id = FuturesTradeSessionBarAccumulatorEntityId.Parse(threadId.EntityId);
        var inputs = new List<IEvent>(messages.Count);
        foreach (var message in messages)
        {
            try
            {
                var input = ParseMessage(context, message);
                if (input is FuturesMarketPriceUpdatedRealtimeEvent price)
                {
                    if (price.UpdateSource != FuturesMarketPriceUpdateSource.Trade) continue;
                    if (context.Accumulators.Resolve(price.EntityId.ContractId, price.EntityId.ValueDate) != id)
                        throw new InvalidDataException("Trade batch crosses accumulator partitions.");
                }
                else if (input is FuturesTradeSessionBarSignalBarrierRealtimeEvent barrier && barrier.EntityId != id)
                    throw new InvalidDataException("Bar barrier crosses accumulator partitions.");
                if (input is not null) inputs.Add(input);
            }
            finally { message.ReleasePayload(); }
        }
        if (inputs.Count == 0) return;
        await context.Accumulators.ProcessAsync(id, accumulator =>
        {
            var bars = new List<FuturesTradeSessionBarReadModel>();
            foreach (var input in inputs)
            {
                cancellationToken.ThrowIfCancellationRequested();
                switch (input)
                {
                    case FuturesMarketPriceUpdatedRealtimeEvent price: bars.AddRange(accumulator.Accept(price)); break;
                    case FuturesTradeSessionBarSignalBarrierRealtimeEvent barrier: bars.AddRange(accumulator.CloseThrough(barrier.BarrierUtc)); break;
                }
            }
            return bars;
        }, async bar =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await context.PublishFuturesTradeSessionBarAsync(bar).ConfigureAwait(false);
            if (!result.Success) throw new IOException($"Bar {bar.ObservationId} publication failed: {result.ErrorCode}; {result.ErrorMessage}");
        }).ConfigureAwait(false);
    }

    static readonly ActorTypeId MarketPriceRoute = new(
        ActorType.Realtime,
        FuturesMarketPriceUpdatedRealtimeEvent.Actor,
        FuturesMarketPriceUpdatedRealtimeEvent.Verb);

    static readonly IReadOnlyDictionary<string, Func<IActorMessage, IEvent>> _parseMap =
        new Dictionary<string, Func<IActorMessage, IEvent>>(StringComparer.Ordinal)
        {
            [FuturesMarketPriceUpdatedRealtimeEvent.Verb] =
                message => message.AsEvent<FuturesMarketPriceUpdatedRealtimeEvent>()!,
            [FuturesTradeSessionBarSignalBarrierRealtimeEvent.Verb] =
                message => message.AsEvent<FuturesTradeSessionBarSignalBarrierRealtimeEvent>()!
        };

    readonly IFuturesTradeSessionBarSignalRealtimeContext context = IsArgumentNull.Named(actorContext as IFuturesTradeSessionBarSignalRealtimeContext, nameof(actorContext))!;
    readonly ILogger<FuturesTradeSessionBarSignalRealtimeActor> logger = IsArgumentNull.Set(actorContext.Logger);
    CancellationTokenSource? barrierStopping;
    Task barrierLoop = Task.CompletedTask;

    readonly IReadOnlyDictionary<Type, Func<IEvent, IFuturesTradeSessionBarSignalRealtimeContext, ILogger, ValueTask<bool>>>
        _receiveMap = new Dictionary<Type, Func<IEvent, IFuturesTradeSessionBarSignalRealtimeContext, ILogger, ValueTask<bool>>>
        {
            [typeof(FuturesMarketPriceUpdatedRealtimeEvent)] = static (@event, context, logger) =>
                ((FuturesMarketPriceUpdatedRealtimeEvent)@event).ExecuteAsync(context, logger),
            [typeof(FuturesTradeSessionBarSignalBarrierRealtimeEvent)] = static (@event, context, logger) =>
                ((FuturesTradeSessionBarSignalBarrierRealtimeEvent)@event).ExecuteAsync(context, logger)
        };

    /// <summary>Registers the market-price route and starts the server-owned interval barrier.</summary>
    protected override ValueTask OnStartup(
        IEventActorContext<FuturesTradeSessionBarSignalRealtimeActor> context)
    {
        ArgumentNullException.ThrowIfNull(context);
        this.context.Accumulators.Restore();
        context.AddRealtimeRouter(MarketPriceRoute, Id, ResolveAccumulatorEntityId);
        if (this.context.TimeProvider is TomasAI.IFM.Shared.Time.IRecordedRealtimeSourceReplay replay
            && replay.UsesRecordedSource(ActorName, FuturesTradeSessionBarSignalBarrierRealtimeEvent.Verb))
            return ValueTask.CompletedTask;
        barrierStopping = new CancellationTokenSource();
        barrierLoop = RunBarrierLoopAsync(
            context,
            this.context.Accumulators,
            this.context.TimeProvider,
            barrierStopping.Token);
        return ValueTask.CompletedTask;
    }

    /// <summary>Releases the market-price route and stops the interval barrier.</summary>
    protected override async ValueTask OnShutdown(
        IEventActorContext<FuturesTradeSessionBarSignalRealtimeActor> context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.RemoveRealtimeRouter(MarketPriceRoute, Id);
        var stopping = barrierStopping;
        if (stopping is null) return;
        await stopping.CancelAsync().ConfigureAwait(false);
        try
        {
            await barrierLoop.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stopping.IsCancellationRequested)
        {
            // The publisher barrier loop completed after its stop request.
        }
        finally
        {
            stopping.Dispose();
            barrierStopping = null;
            barrierLoop = Task.CompletedTask;
        }
    }

    /// <summary>Parses routed trade updates and the private publisher barrier event.</summary>
    protected override IEvent ParseMessage(
        IEventActorContext<FuturesTradeSessionBarSignalRealtimeActor> context,
        IActorMessage message)
        => ParseMappedRealtimeEvent(context, message, _parseMap);

    /// <summary>Dispatches each supported event to its dedicated extension handler.</summary>
    protected override async ValueTask ReceiveAsync(
        IEventActorContext<FuturesTradeSessionBarSignalRealtimeActor> context,
        IEvent @event)
    {
        ArgumentNullException.ThrowIfNull(context);
        var handler = ResolveMappedEventHandler(@event, _receiveMap);
        if (!await handler(@event, this.context, logger).ConfigureAwait(false))
            throw new IOException("Trade-session bar delivery did not complete.");
    }

    static async Task RunBarrierLoopAsync(
        IEventActorContext<FuturesTradeSessionBarSignalRealtimeActor> actorContext,
        FuturesTradeSessionBarAccumulatorRegistry accumulators,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        await Task.Yield();
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
        {
            if (!actorContext.IsReady)
                return;
            var barrierUtc = timeProvider.GetUtcNow();
            foreach (var entityId in accumulators.ActivePartitions(barrierUtc))
                await actorContext.SendAsync<
                        FuturesTradeSessionBarSignalBarrierRealtimeEvent,
                        FuturesTradeSessionBarAccumulatorEntityId>(
                        FuturesTradeSessionBarSignalBarrierRealtimeEvent.Create(barrierUtc, entityId))
                    .ConfigureAwait(false);
        }
    }

    string ResolveAccumulatorEntityId(ActorSubject source)
    {
        var sourceEntityId = TickDataEntityId.Parse(source.EntityId);
        return context.Accumulators.Resolve(sourceEntityId.ContractId, sourceEntityId.ValueDate).Format();
    }

    /// <inheritdoc />
    protected override async ValueTask OnExceptionAsync(
        IEventActorContext<FuturesTradeSessionBarSignalRealtimeActor> context,
        ActorThreadId threadId,
        IEvent @event,
        Exception ex) => await ex.SendErrorEventAsync<
            TomasAI.IFM.Shared.EventModelActor.Events.EventExceptionEvent,
            ActorEntityId>(ErrorType.EventService, context).ConfigureAwait(false);
}
