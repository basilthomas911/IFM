using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Realtime.Actor;

/// <summary>Routes eligible realtime ES trade prices to durable Daily Futures ITI command processing.</summary>
public class FuturesItiSignalRealtimeActor(
    IRealtimeActorContext<FuturesItiSignalRealtimeActor> actorContext,
    ResidentActorPilotOptions? residency = null)
    : BaseEventActor<FuturesItiSignalRealtimeActor>(actorContext,
        ((IFuturesItiSignalRealtimeContext)actorContext).Logger)
{
    /// <summary>Identifies the singleton Futures ITI realtime ingress mailbox.</summary>
    public const string ActorName = "FuturesItiSignalRealtime";
    protected override bool PropagateHandlerFailures => true;
    CancellationTokenSource? recoveryLifetime;
    Task? recoveryLoop;
    FuturesItiOrderedRuntime? orderedRuntime;
    public object? OrderedIntakeSnapshot => orderedRuntime?.Snapshot;

    static readonly ActorTypeId MarketPriceRoute = new(
        ActorType.Realtime,
        FuturesMarketPriceUpdatedRealtimeEvent.Actor,
        FuturesMarketPriceUpdatedRealtimeEvent.Verb);

    static readonly IReadOnlyDictionary<string, Func<IActorMessage, IEvent>> _parseMap =
        new Dictionary<string, Func<IActorMessage, IEvent>>(StringComparer.Ordinal)
        {
            [FuturesMarketPriceUpdatedRealtimeEvent.Verb] =
                static message =>
                {
                    var source = message.AsEvent<FuturesMarketPriceUpdatedRealtimeEvent>()!;
                    var dataset = string.IsNullOrWhiteSpace(message.SourceDataset) ? source.SourceDataset : message.SourceDataset;
                    var generation = message.SourceGenerationId == Guid.Empty ? source.SourceGenerationId : message.SourceGenerationId;
                    return dataset == source.SourceDataset && generation == source.SourceGenerationId ? source
                        : MessageOrigins.Copy(source, source with { SourceDataset = dataset, SourceGenerationId = generation });
                }
        };

    static readonly IReadOnlyDictionary<Type, Func<IEvent, IFuturesItiSignalRealtimeContext, CancellationToken, ValueTask<bool>>>
        _receiveMap = new Dictionary<Type, Func<IEvent, IFuturesItiSignalRealtimeContext, CancellationToken, ValueTask<bool>>>
        {
            [typeof(FuturesMarketPriceUpdatedRealtimeEvent)] = static (@event, context, cancellationToken) =>
                ((FuturesMarketPriceUpdatedRealtimeEvent)@event).ExecuteAsync(context, cancellationToken)
        };

    IFuturesItiSignalRealtimeContext TypedContext { get; } = IsArgumentNull.Named(actorContext as IFuturesItiSignalRealtimeContext, nameof(actorContext))!;

    /// <inheritdoc />
    protected override async ValueTask OnStartup(IEventActorContext<FuturesItiSignalRealtimeActor> context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (residency?.FuturesItiOrderedIntakeEnabled == true)
        {
            if (TypedContext.IngressRecovery is { PendingCount: > 0 })
                throw new InvalidOperationException("Pending fallback ITI commands must be drained or reconciled before switching to ordered intake.");
            // Managed actors start concurrently. The supervisor's per-actor lifecycle gate
            // joins an in-progress start and propagates owner recovery failure before intake opens.
            await context.Container.Resolve<IActorSupervisor>().StartAsync(new(ActorType.Command,
                Command.Actor.FuturesItiSignalCommandActor.ActorName)).ConfigureAwait(false);
            orderedRuntime = await FuturesItiOrderedRuntime.StartAsync(TypedContext, residency.FuturesItiJournalDirectory).ConfigureAwait(false);
        }
        else
        {
            if (TypedContext.IngressRecovery is { PendingCount: > 0 } recovery)
            {
                await context.Container.Resolve<IActorSupervisor>().StartAsync(new(ActorType.Command,
                    Command.Actor.FuturesItiSignalCommandActor.ActorName)).ConfigureAwait(false);
                if (!await recovery.RecoverAsync((command, attemptToken) => FuturesMarketPriceUpdated.DispatchAsync(TypedContext, command, attemptToken)).ConfigureAwait(false))
                    throw new IOException("Pending ITI ingress recovery failed; intake remains closed.");
            }
            context.AddRealtimeRouter(MarketPriceRoute, Id);
            if (TypedContext.IngressRecovery is not null)
            {
                recoveryLifetime = new();
                recoveryLoop = RecoverAsync(recoveryLifetime.Token);
            }
        }
        TypedContext.Telemetry.RecordRouteAttached();
    }

    /// <inheritdoc />
    protected override async ValueTask OnShutdown(IEventActorContext<FuturesItiSignalRealtimeActor> context)
    {
        ArgumentNullException.ThrowIfNull(context);
        recoveryLifetime?.Cancel();
        if (recoveryLoop is not null) await recoveryLoop.ConfigureAwait(false);
        recoveryLifetime?.Dispose(); recoveryLifetime = null; recoveryLoop = null;
        context.RemoveRealtimeRouter(MarketPriceRoute, Id);
        if (orderedRuntime is not null) await orderedRuntime.DisposeAsync().ConfigureAwait(false);
        TypedContext.Telemetry.RecordRouteDetached();
    }

    /// <inheritdoc />
    protected override IEvent ParseMessage(
        IEventActorContext<FuturesItiSignalRealtimeActor> context,
        IActorMessage message) => ParseMappedRealtimeEvent(context, message, _parseMap);

    /// <inheritdoc />
    protected override ValueTask ReceiveAsync(
        IEventActorContext<FuturesItiSignalRealtimeActor> context,
        IEvent @event)
        => ReceiveAsync(context, @event, CancellationToken.None);

    /// <inheritdoc />
    protected override async ValueTask ReceiveAsync(
        IEventActorContext<FuturesItiSignalRealtimeActor> context,
        IEvent @event,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(@event);
        cancellationToken.ThrowIfCancellationRequested();
        var handler = ResolveMappedEventHandler(@event, _receiveMap);
        if (!await handler(@event, TypedContext, cancellationToken).ConfigureAwait(false))
        {
            var failure = new IOException("ITI required command delivery failed; input must not be acknowledged.");
            failure.Data[FuturesMarketPriceUpdated.FailureRecordedKey] = true;
            throw failure;
        }
    }

    async Task RecoverAsync(CancellationToken token)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        try
        {
            while (await timer.WaitForNextTickAsync(token).ConfigureAwait(false))
            {
                if (!IsRunning || TypedContext.IngressRecovery is not { PendingCount: > 0 } recovery) continue;
                try { _ = await recovery.RecoverAsync((command, attemptToken) => FuturesMarketPriceUpdated.DispatchAsync(TypedContext, command, attemptToken), token).ConfigureAwait(false); }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { return; }
                catch (Exception error) { TypedContext.HealthEvidence.Record("ITI", "ES", "Unhealthy", "Fallback ingress recovery blocked: " + error.Message); }
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
    }

    /// <inheritdoc />
    protected override async ValueTask OnExceptionAsync(
        IEventActorContext<FuturesItiSignalRealtimeActor> context,
        ActorThreadId threadId,
        IEvent @event,
        Exception ex)
    {
        ArgumentNullException.ThrowIfNull(ex);
        if (!ex.Data.Contains(FuturesMarketPriceUpdated.FailureRecordedKey)) TypedContext.Telemetry.RecordFailure(ex.Message);
        await ex.SendErrorEventAsync<
            TomasAI.IFM.Shared.EventModelActor.Events.EventExceptionEvent,
            ActorEntityId>(ErrorType.EventService, context).ConfigureAwait(false);
    }
}
