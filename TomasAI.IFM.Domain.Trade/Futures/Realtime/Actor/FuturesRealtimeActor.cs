using System.Collections.Frozen;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Domain.Trade.Futures.Realtime.Extensions;
using TomasAI.IFM.Domain.Trade.Shared.Model;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Trade.Futures.Realtime.Actor;

public sealed class FuturesRealtimeActor(IRealtimeActorContext<FuturesRealtimeActor> context)
    : BaseEventActor<FuturesRealtimeActor>(context, Typed(context).Logger)
{
    public const string ActorName = "FuturesRealtime";
    readonly IFuturesRealtimeContext services = Typed(context);
    static readonly ActorTypeId TickRoute = new(
        ActorType.Realtime, FuturesTickTradeDataChangedEvent.Actor, FuturesTickTradeDataChangedEvent.Verb);

    static readonly IReadOnlyDictionary<string, Func<IActorMessage, IEvent>> ParseMap =
        new Dictionary<string, Func<IActorMessage, IEvent>>(StringComparer.Ordinal)
        {
            [FuturesTickTradeDataChangedEvent.Verb] = message => message.AsEvent<FuturesTickTradeDataChangedEvent>()!,
            [OpenPositionRoutesChangedEvent.Verb] = message => message.AsEvent<OpenPositionRoutesChangedEvent>()!
        }.ToFrozenDictionary(StringComparer.Ordinal);

    static readonly IReadOnlyDictionary<Type, Func<IFuturesRealtimeContext, IEvent, ValueTask>> ReceiveMap =
        new Dictionary<Type, Func<IFuturesRealtimeContext, IEvent, ValueTask>>
        {
            [typeof(FuturesTickTradeDataChangedEvent)] = static (context, domainEvent) =>
                ((FuturesTickTradeDataChangedEvent)domainEvent).ExecuteAsync(context),
            [typeof(OpenPositionRoutesChangedEvent)] = static (context, domainEvent) =>
                ((OpenPositionRoutesChangedEvent)domainEvent).ExecuteAsync(context)
        }.ToFrozenDictionary();

    protected override async ValueTask OnStartup(IEventActorContext<FuturesRealtimeActor> context)
    {
        var routes = await services.DbFactory.TradeDb.GetOpenPositionRouteSnapshotAsync().ConfigureAwait(false);
        services.RouteIndex.ReplaceFromSnapshot(routes.Where(static value =>
            value.Route.StrategyKind == TradeStrategyKind.FuturesOutright));
        context.AddRealtimeRouter(TickRoute, Id);
    }

    protected override ValueTask OnShutdown(IEventActorContext<FuturesRealtimeActor> context)
    {
        context.RemoveRealtimeRouter(TickRoute, Id);
        return ValueTask.CompletedTask;
    }

    protected override IEvent ParseMessage(
        IEventActorContext<FuturesRealtimeActor> context,
        IActorMessage message) => ParseMappedRealtimeEvent(context, message, ParseMap);

    protected override ValueTask ReceiveAsync(
        IEventActorContext<FuturesRealtimeActor> context,
        IEvent domainEvent) => ResolveMappedEventHandler(domainEvent, ReceiveMap)(services, domainEvent);

    protected override async ValueTask OnExceptionAsync(
        IEventActorContext<FuturesRealtimeActor> context,
        ActorThreadId threadId,
        IEvent domainEvent,
        Exception exception) =>
        await exception.SendErrorEventAsync<TomasAI.IFM.Shared.EventModelActor.Events.EventExceptionEvent, ActorEntityId>(
            ErrorType.EventService, context).ConfigureAwait(false);

    static IFuturesRealtimeContext Typed(IRealtimeActorContext<FuturesRealtimeActor> context) =>
        context as IFuturesRealtimeContext ??
        throw new ArgumentException("Typed Futures realtime context required.");
}
