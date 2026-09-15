using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.MarketData.MarketOutlook;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Realtime;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Model.Processing;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Actor;

/// <summary>
/// Realtime/NATS adapter for Market Outlook. It validates routed source events and submits strongly
/// typed local updates; it never mutates or publishes the cache directly.
/// </summary>
public class MarketOutlookSnapshotRealtimeActor(
    IRealtimeActorContext<MarketOutlookSnapshotRealtimeActor> actorContext)
    : BaseEventActor<MarketOutlookSnapshotRealtimeActor>(actorContext, actorContext.Logger)
{
    public const string ActorName = "MarketOutlook";

    static readonly ActorTypeId MarketPriceRoute = new(
        ActorType.Realtime,
        FuturesMarketPriceUpdatedRealtimeEvent.Actor,
        FuturesMarketPriceUpdatedRealtimeEvent.Verb);

    static readonly ActorTypeId SessionStatisticsRoute = new(
        ActorType.Realtime,
        FuturesSessionStatisticsUpdatedRealtimeEvent.Actor,
        FuturesSessionStatisticsUpdatedRealtimeEvent.Verb);

    static readonly IReadOnlyDictionary<string, Func<IActorMessage, IEvent>> _parseMap =
        new Dictionary<string, Func<IActorMessage, IEvent>>(StringComparer.Ordinal)
        {
            [MarketOutlookComponentChangedRealtimeEvent.Verb] =
                message => message.AsEvent<MarketOutlookComponentChangedRealtimeEvent>()!,
            [FuturesMarketPriceUpdatedRealtimeEvent.Verb] =
                message => message.AsEvent<FuturesMarketPriceUpdatedRealtimeEvent>()!,
            [FuturesSessionStatisticsUpdatedRealtimeEvent.Verb] =
                message => message.AsEvent<FuturesSessionStatisticsUpdatedRealtimeEvent>()!,
            [MarketOutlookEodUpdatedRealtimeEvent.Verb] =
                message => message.AsEvent<MarketOutlookEodUpdatedRealtimeEvent>()!,
            [MarketOutlookSnapshotInsertedEvent.Verb] =
                message => message.AsEvent<MarketOutlookSnapshotInsertedEvent>()!
        };

    static readonly IReadOnlyDictionary<Type, Func<IEvent,
        IEventActorContext<MarketOutlookSnapshotRealtimeActor>,
        ILogger<MarketOutlookSnapshotRealtimeActor>, ValueTask>> _receiveMap =
        new Dictionary<Type, Func<IEvent,
            IEventActorContext<MarketOutlookSnapshotRealtimeActor>,
            ILogger<MarketOutlookSnapshotRealtimeActor>, ValueTask>>
        {
            [typeof(MarketOutlookComponentChangedRealtimeEvent)] = static (@event, context, logger) =>
                ((MarketOutlookComponentChangedRealtimeEvent)@event).ExecuteAsync(context, logger),
            [typeof(MarketOutlookEodUpdatedRealtimeEvent)] = static (@event, context, logger) =>
                ((MarketOutlookEodUpdatedRealtimeEvent)@event).ExecuteAsync(context, logger),
            [typeof(FuturesMarketPriceUpdatedRealtimeEvent)] = static (@event, context, logger) =>
                ((FuturesMarketPriceUpdatedRealtimeEvent)@event).ExecuteAsync(context, logger),
            [typeof(FuturesSessionStatisticsUpdatedRealtimeEvent)] = static (@event, context, logger) =>
                ((FuturesSessionStatisticsUpdatedRealtimeEvent)@event).ExecuteAsync(context, logger),
            [typeof(MarketOutlookSnapshotInsertedEvent)] = static (@event, context, logger) =>
                ((MarketOutlookSnapshotInsertedEvent)@event).ExecuteAsync(context, logger)
        };

    protected override ValueTask OnStartup(IEventActorContext<MarketOutlookSnapshotRealtimeActor> context)
    {
        context.AddRealtimeRouter(MarketPriceRoute, Id);
        context.AddRealtimeRouter(SessionStatisticsRoute, Id);
        return ValueTask.CompletedTask;
    }

    protected override ValueTask OnShutdown(IEventActorContext<MarketOutlookSnapshotRealtimeActor> context)
    {
        context.RemoveRealtimeRouter(MarketPriceRoute, Id);
        context.RemoveRealtimeRouter(SessionStatisticsRoute, Id);
        return ValueTask.CompletedTask;
    }

    protected override IEvent ParseMessage(
        IEventActorContext<MarketOutlookSnapshotRealtimeActor> context,
        IActorMessage message) => ParseMappedRealtimeEvent(context, message, _parseMap);

    protected override ValueTask ReceiveAsync(
        IEventActorContext<MarketOutlookSnapshotRealtimeActor> context,
        IEvent @event)
    {
        var receive = ResolveMappedEventHandler(@event, _receiveMap);
        return receive(@event, context, actorContext.Logger);
    }

    protected override ValueTask OnExceptionAsync(
        IEventActorContext<MarketOutlookSnapshotRealtimeActor> context,
        ActorThreadId threadId,
        IEvent @event,
        Exception exception)
    {
        actorContext.Logger.LogErrorEvent(
            ActorName,
            exception,
            "Market Outlook local update submission failed for {EntityId}",
            @event.Subject.EntityId);
        return ValueTask.CompletedTask;
    }
}
