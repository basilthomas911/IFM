using Microsoft.Extensions.Logging;
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
public sealed class TickAggregationRealtimeActor(
    IActorSupervisor supervisor,
    IRealtimeProjector<TickAggregationRealtimeActor> projector,
    ILogger<TickAggregationRealtimeActor> logger)
    : BaseEventActor<TickAggregationRealtimeActor>(
        supervisor,
        logger,
        new ActorMailboxId(ActorType.Realtime, ActorName))
{
    public const string ActorName = "TickAggregationRealtime";

    static readonly Dictionary<string, Func<IActorMessage, IEvent>> ParseMap = new()
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

    protected override async ValueTask OnStartup(IEventActorContext context) =>
        await projector.StartAsync(context).ConfigureAwait(false);

    protected override async ValueTask OnShutdown(IEventActorContext context) =>
        await projector.StopAsync().ConfigureAwait(false);

    protected override IEvent ParseMessage(
        IEventActorContext context,
        IActorMessage message)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(message);
        var subject = message.Subject;
        if (subject is not { ActorType: ActorType.Realtime, Name: ActorName }
            || !ParseMap.TryGetValue(subject.Verb, out var parser))
            return default!;

        var domainEvent = parser(message);
        ArgumentNullException.ThrowIfNull(domainEvent);
        domainEvent.CheckForEmptyCommandId();
        return domainEvent;
    }

    protected override async ValueTask ReceiveAsync(
        IEventActorContext context,
        IEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(domainEvent);

        switch (domainEvent)
        {
            case FuturesTickTradeDataChangedEvent trade:
                _ = await projector.ProcessRealtimeEventAsync(trade.ToInsertedEvent())
                    .ConfigureAwait(false);
                break;
            case FuturesTickQuoteDataChangedEvent quote:
                _ = await projector.ProcessRealtimeEventAsync(quote.ToInsertedEvent())
                    .ConfigureAwait(false);
                break;
            case TickAggregationFailEvent failed:
                logger.LogErrorEvent(
                    ActorName,
                    "{EventName} for {EntityId}: {ErrorMessage}",
                    failed.EventName,
                    failed.EntityId,
                    failed.ErrorMessage);
                break;
            case FuturesTickTradeDataInsertedEvent:
            case FuturesTickQuoteDataInsertedEvent:
            case FuturesSessionStatisticsUpdatedRealtimeEvent:
            case TickAggregationCompleteEvent:
                break;
            default:
                throw new InvalidOperationException(
                    $"Unable to resolve {ActorName} realtime event from {domainEvent.Subject}.");
        }
    }

    protected override async ValueTask OnExceptionAsync(
        IEventActorContext context,
        ActorThreadId threadId,
        IEvent domainEvent,
        Exception exception) =>
        await exception.SendErrorEventAsync<
            TomasAI.IFM.Shared.EventModelActor.Events.EventExceptionEvent,
            ActorEntityId>(ErrorType.EventService, context).ConfigureAwait(false);
}
