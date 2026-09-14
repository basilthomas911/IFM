using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Realtime.Actor;

/// <summary>Routes eligible realtime ES trade prices to durable Daily Futures ITI command processing.</summary>
public class FuturesItiSignalRealtimeActor(
    IRealtimeActorContext<FuturesItiSignalRealtimeActor> actorContext)
    : BaseEventActor<FuturesItiSignalRealtimeActor>(actorContext,
        ((IFuturesItiSignalRealtimeContext)actorContext).Logger)
{
    /// <summary>Identifies the singleton Futures ITI realtime ingress mailbox.</summary>
    public const string ActorName = "FuturesItiSignalRealtime";

    static readonly ActorTypeId MarketPriceRoute = new(
        ActorType.Realtime,
        FuturesMarketPriceUpdatedRealtimeEvent.Actor,
        FuturesMarketPriceUpdatedRealtimeEvent.Verb);

    static readonly IReadOnlyDictionary<string, Func<IActorMessage, IEvent>> _parseMap =
        new Dictionary<string, Func<IActorMessage, IEvent>>(StringComparer.Ordinal)
        {
            [FuturesMarketPriceUpdatedRealtimeEvent.Verb] =
                static message => message.AsEvent<FuturesMarketPriceUpdatedRealtimeEvent>()!
        };

    static readonly IReadOnlyDictionary<Type, Func<IEvent, IFuturesItiSignalRealtimeContext, ValueTask<bool>>>
        _receiveMap = new Dictionary<Type, Func<IEvent, IFuturesItiSignalRealtimeContext, ValueTask<bool>>>
        {
            [typeof(FuturesMarketPriceUpdatedRealtimeEvent)] = static (@event, context) =>
                ((FuturesMarketPriceUpdatedRealtimeEvent)@event).ExecuteAsync(context)
        };

    IFuturesItiSignalRealtimeContext TypedContext { get; } = IsArgumentNull.Set(
        actorContext as IFuturesItiSignalRealtimeContext, nameof(actorContext))!;

    /// <inheritdoc />
    protected override ValueTask OnStartup(IEventActorContext<FuturesItiSignalRealtimeActor> context)
    {
        context.AddRealtimeRouter(MarketPriceRoute, Id);
        TypedContext.Telemetry.RecordRouteAttached();
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    protected override async ValueTask OnShutdown(IEventActorContext<FuturesItiSignalRealtimeActor> context)
    {
        context.RemoveRealtimeRouter(MarketPriceRoute, Id);
        await TypedContext.GenerationGate.WaitForIdleAsync().ConfigureAwait(false);
        TypedContext.Telemetry.RecordRouteDetached();
    }

    /// <inheritdoc />
    protected override IEvent ParseMessage(
        IEventActorContext<FuturesItiSignalRealtimeActor> context,
        IActorMessage message) => ParseMappedRealtimeEvent(context, message, _parseMap);

    /// <inheritdoc />
    protected override async ValueTask ReceiveAsync(
        IEventActorContext<FuturesItiSignalRealtimeActor> context,
        IEvent @event)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(@event);
        var handler = ResolveMappedEventHandler(@event, _receiveMap);
        _ = await handler(@event, TypedContext).ConfigureAwait(false);
    }

    /// <inheritdoc />
    protected override async ValueTask OnExceptionAsync(
        IEventActorContext<FuturesItiSignalRealtimeActor> context,
        ActorThreadId threadId,
        IEvent @event,
        Exception exception)
    {
        TypedContext.Telemetry.RecordFailure(exception.Message);
        await exception.SendErrorEventAsync<
            TomasAI.IFM.Shared.EventModelActor.Events.EventExceptionEvent,
            ActorEntityId>(ErrorType.EventService, context).ConfigureAwait(false);
    }
}
