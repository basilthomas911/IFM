using System.Collections.Frozen;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Domain.Trade.Futures.Option.Realtime;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Realtime.Actor;

public sealed class FuturesOptionRealtimeActor(
    IRealtimeActorContext<FuturesOptionRealtimeActor> context)
    : BaseEventActor<FuturesOptionRealtimeActor>(context, ResolveContext(context).Logger)
{
    public const string ActorName = "FuturesOptionRealtime";

    static readonly ActorTypeId TickRoute = new(
        ActorType.Realtime,
        FuturesTickTradeDataChangedEvent.Actor,
        FuturesTickTradeDataChangedEvent.Verb);

    static readonly IReadOnlyDictionary<string, Func<IActorMessage, IEvent>> ParseMap =
        new Dictionary<string, Func<IActorMessage, IEvent>>
        {
            [FuturesTickTradeDataChangedEvent.Verb] =
                message => message.AsEvent<FuturesTickTradeDataChangedEvent>()!,
            [OpenPositionRoutesChangedEvent.Verb] =
                message => message.AsEvent<OpenPositionRoutesChangedEvent>()!
        }.ToFrozenDictionary();

    static readonly IReadOnlyDictionary<Type, Func<IFuturesOptionRealtimeContext, IEvent, ValueTask>> ReceiveMap =
        new Dictionary<Type, Func<IFuturesOptionRealtimeContext, IEvent, ValueTask>>
        {
            [typeof(FuturesTickTradeDataChangedEvent)] =
                static (runtime, @event) =>
                    ((FuturesTickTradeDataChangedEvent)@event).ExecuteAsync(runtime),
            [typeof(OpenPositionRoutesChangedEvent)] =
                static (runtime, @event) =>
                    ((OpenPositionRoutesChangedEvent)@event).ExecuteAsync(runtime)
        }.ToFrozenDictionary();

    readonly IFuturesOptionRealtimeContext _runtime = ResolveContext(context);

    protected override async ValueTask OnStartup(
        IEventActorContext<FuturesOptionRealtimeActor> context)
    {
        var routes = await _runtime.DbFactory.TradeDb
            .GetOpenPositionRouteSnapshotAsync();

        _runtime.RouteIndex.ReplaceFromSnapshot(
            routes.Where(route =>
                route.Route.TradeType is TradeStrategyKind.IronCondor
                    or TradeStrategyKind.VerticalSpread));

        context.AddRealtimeRouter(TickRoute, Id);
    }

    protected override ValueTask OnShutdown(
        IEventActorContext<FuturesOptionRealtimeActor> context)
    {
        context.RemoveRealtimeRouter(TickRoute, Id);
        return ValueTask.CompletedTask;
    }

    protected override IEvent ParseMessage(
        IEventActorContext<FuturesOptionRealtimeActor> context,
        IActorMessage message) =>
        ParseMappedRealtimeEvent(context, message, ParseMap);

    protected override ValueTask ReceiveAsync(
        IEventActorContext<FuturesOptionRealtimeActor> context,
        IEvent @event) =>
        ResolveMappedEventHandler(@event, ReceiveMap)(_runtime, @event);

    protected override async ValueTask OnExceptionAsync(
        IEventActorContext<FuturesOptionRealtimeActor> context,
        ActorThreadId actorThreadId,
        IEvent @event,
        Exception exception) =>
        await exception.SendErrorEventAsync<
            TomasAI.IFM.Shared.EventModelActor.Events.EventExceptionEvent,
            ActorEntityId>(ErrorType.EventService, context);

    static IFuturesOptionRealtimeContext ResolveContext(
        IRealtimeActorContext<FuturesOptionRealtimeActor> context) =>
        context as IFuturesOptionRealtimeContext
        ?? throw new ArgumentException(
            "Typed Futures Option realtime context required.");
}
