using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Realtime.Extensions;
using TomasAI.IFM.Domain.MarketData.Feed.Event.Extensions;
using TomasAI.IFM.Domain.MarketData.Feed.Command.Extensions;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Realtime.Actor;

/// <summary>Consumes the futures-option branch of normalized live trades over Core NATS.</summary>
public class FuturesOptionTickDataRealtimeActor(IRealtimeActorContext<FuturesOptionTickDataRealtimeActor> actorContext)
    : BaseEventActor<FuturesOptionTickDataRealtimeActor>(actorContext, actorContext.Logger)
{
    public const string ActorName = "FuturesOptionTickDataRealtime";

    /// <summary>Gets the typed realtime context supplied at construction.</summary>
    protected IFuturesOptionTickDataRealtimeContext RealtimeContext { get; } = IsArgumentNull.Set(actorContext as IFuturesOptionTickDataRealtimeContext, nameof(actorContext))!;

    static readonly ActorTypeId TickTradeRoute = new(
        ActorType.Realtime,
        FuturesTickTradeDataInsertedEvent.Actor,
        FuturesTickTradeDataInsertedEvent.Verb);

    static readonly IReadOnlyDictionary<string, Func<IActorMessage, IEvent>> _parseMap =
        new Dictionary<string, Func<IActorMessage, IEvent>>(StringComparer.Ordinal)
        {
            [FuturesTickTradeDataInsertedEvent.Verb] =
                message => message.AsEvent<FuturesTickTradeDataInsertedEvent>()!
        };

    readonly IReadOnlyDictionary<Type, Func<IEvent, IFuturesOptionTickDataRealtimeContext, ValueTask<bool>>> _receiveMap =
        new Dictionary<Type, Func<IEvent, IFuturesOptionTickDataRealtimeContext, ValueTask<bool>>>
        {
            [typeof(FuturesTickTradeDataInsertedEvent)] = (@event, context) =>
                ((FuturesTickTradeDataInsertedEvent)@event).ExecuteAsync(
                    context,
                    ((IFuturesOptionTickDataRealtimeContext)actorContext).MarketDataApi,
                    ((IFuturesOptionTickDataRealtimeContext)actorContext).StatusConsoleWriter,
                    actorContext.Logger)
        };

    protected override ValueTask OnStartup(IEventActorContext<FuturesOptionTickDataRealtimeActor> context)
    {
        context.AddRealtimeRouter(TickTradeRoute, Id);
        return ValueTask.CompletedTask;
    }

    protected override ValueTask OnShutdown(IEventActorContext<FuturesOptionTickDataRealtimeActor> context)
    {
        context.RemoveRealtimeRouter(TickTradeRoute, Id);
        return ValueTask.CompletedTask;
    }

    protected override IEvent ParseMessage(
        IEventActorContext<FuturesOptionTickDataRealtimeActor> context,
        IActorMessage message)
        => ParseMappedRealtimeEvent(context, message, _parseMap);

    protected override async ValueTask ReceiveAsync(
        IEventActorContext<FuturesOptionTickDataRealtimeActor> context,
        IEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(context);
        var handler = ResolveMappedEventHandler(domainEvent, _receiveMap);
        _ = await handler(domainEvent, RealtimeContext).ConfigureAwait(false);
    }

    protected override async ValueTask OnExceptionAsync(
        IEventActorContext<FuturesOptionTickDataRealtimeActor> context,
        ActorThreadId threadId,
        IEvent domainEvent,
        Exception exception) =>
        await exception.SendErrorEventAsync<
            TomasAI.IFM.Shared.EventModelActor.Events.EventExceptionEvent,
            ActorEntityId>(ErrorType.EventService, context).ConfigureAwait(false);
}
