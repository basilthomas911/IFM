using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Feed.TickAggregation.Realtime.Extensions;
using TomasAI.IFM.Application.EventProjector.Realtime.Contracts;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.StatusConsole;

namespace TomasAI.IFM.Domain.MarketData.Feed.TickAggregation.Realtime.Actor;

/// <summary>
/// Receives normalized Databento ticks over Core NATS and applies their storage
/// projections without event sourcing or replay.
/// </summary>
public sealed class TickAggregationRealtimeActor(IRealtimeActorContext<TickAggregationRealtimeActor> actorContext)
    : BaseEventActor<TickAggregationRealtimeActor>(actorContext, actorContext.Logger)
{
    public const string ActorName = "TickAggregationRealtime";

    /// <summary>Gets the typed realtime context supplied at construction.</summary>
    ITickAggregationRealtimeContext RealtimeContext { get; } = IsArgumentNull.Set(actorContext as ITickAggregationRealtimeContext, nameof(actorContext))!;

    static readonly IReadOnlyDictionary<string, Func<IActorMessage, IEvent>> _parseMap =
        new Dictionary<string, Func<IActorMessage, IEvent>>(StringComparer.Ordinal)
    {
        [FuturesTickTradeDataChangedEvent.Verb] =
            message => message.AsEvent<FuturesTickTradeDataChangedEvent>()!,
        [FuturesTickQuoteDataChangedEvent.Verb] =
            message => message.AsEvent<FuturesTickQuoteDataChangedEvent>()!,
        [FuturesTickTradeDataInsertedEvent.Verb] =
            message => message.AsEvent<FuturesTickTradeDataInsertedEvent>()!,
        [FuturesTickQuoteDataInsertedEvent.Verb] =
            message => message.AsEvent<FuturesTickQuoteDataInsertedEvent>()!,
        [FuturesSessionStatisticsUpdatedRealtimeEvent.Verb] =
            message => message.AsEvent<FuturesSessionStatisticsUpdatedRealtimeEvent>()!,
        [FuturesTickTradeDataInsertedCompleteEvent.Verb] =
            message => message.AsEvent<FuturesTickTradeDataInsertedCompleteEvent>()!,
        [FuturesTickQuoteDataInsertedCompleteEvent.Verb] =
            message => message.AsEvent<FuturesTickQuoteDataInsertedCompleteEvent>()!,
        [FuturesTickTradeDataInsertedFailEvent.Verb] =
            message => message.AsEvent<FuturesTickTradeDataInsertedFailEvent>()!,
        [FuturesTickQuoteDataInsertedFailEvent.Verb] =
            message => message.AsEvent<FuturesTickQuoteDataInsertedFailEvent>()!
    };

    static readonly IReadOnlyDictionary<Type, Func<IEvent, ITickAggregationRealtimeContext, ValueTask>> _receiveMap =
        new Dictionary<Type, Func<IEvent, ITickAggregationRealtimeContext, ValueTask>>
        {
            [typeof(FuturesTickTradeDataChangedEvent)] = async (@event, context) =>
            {
                var trade = (FuturesTickTradeDataChangedEvent)@event;
                _ = await context.Projector.ProcessRealtimeEventAsync(trade.ToInsertedEvent())
                    .ConfigureAwait(false);
            },
            [typeof(FuturesTickQuoteDataChangedEvent)] = async (@event, context) =>
            {
                var quote = (FuturesTickQuoteDataChangedEvent)@event;
                _ = await context.Projector.ProcessRealtimeEventAsync(quote.ToInsertedEvent())
                    .ConfigureAwait(false);
            },
            [typeof(FuturesTickTradeDataInsertedFailEvent)] = static (@event, context) =>
            {
                LogProjectionFailure((FuturesTickTradeDataInsertedFailEvent)@event, context.Logger);
                return ValueTask.CompletedTask;
            },
            [typeof(FuturesTickQuoteDataInsertedFailEvent)] = static (@event, context) =>
            {
                LogProjectionFailure((FuturesTickQuoteDataInsertedFailEvent)@event, context.Logger);
                return ValueTask.CompletedTask;
            },
            [typeof(FuturesTickTradeDataInsertedEvent)] = static (_, _) => ValueTask.CompletedTask,
            [typeof(FuturesTickQuoteDataInsertedEvent)] = static (_, _) => ValueTask.CompletedTask,
            [typeof(FuturesSessionStatisticsUpdatedRealtimeEvent)] = static (_, _) => ValueTask.CompletedTask,
            [typeof(FuturesTickTradeDataInsertedCompleteEvent)] = static (_, _) => ValueTask.CompletedTask,
            [typeof(FuturesTickQuoteDataInsertedCompleteEvent)] = static (_, _) => ValueTask.CompletedTask
        };

    protected override async ValueTask OnStartup(IEventActorContext<TickAggregationRealtimeActor> context) =>
        await ((ITickAggregationRealtimeContext)actorContext).Projector.StartAsync(context).ConfigureAwait(false);

    protected override async ValueTask OnShutdown(IEventActorContext<TickAggregationRealtimeActor> context) =>
        await ((ITickAggregationRealtimeContext)actorContext).Projector.StopAsync().ConfigureAwait(false);

    protected override IEvent ParseMessage(
        IEventActorContext<TickAggregationRealtimeActor> context,
        IActorMessage message)
        => ParseMappedRealtimeEvent(context, message, _parseMap);

    protected override async ValueTask ReceiveAsync(
        IEventActorContext<TickAggregationRealtimeActor> context,
        IEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(context);
        var handler = ResolveMappedEventHandler(domainEvent, _receiveMap);
        await handler(domainEvent, RealtimeContext).ConfigureAwait(false);
    }

    static void LogProjectionFailure(TickAggregationFailEvent failed, ILogger logger) =>
        logger.LogErrorEvent(
            ActorName,
            "{EventName} for {EntityId}: {ErrorMessage}",
            failed.EventName,
            failed.EntityId,
            failed.ErrorMessage);

    protected override async ValueTask OnExceptionAsync(
        IEventActorContext<TickAggregationRealtimeActor> context,
        ActorThreadId threadId,
        IEvent domainEvent,
        Exception exception) =>
        await exception.SendErrorEventAsync<
            TomasAI.IFM.Shared.EventModelActor.Events.EventExceptionEvent,
            ActorEntityId>(ErrorType.EventService, context).ConfigureAwait(false);
}
