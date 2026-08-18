using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector.Realtime.Contracts;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Domain.MarketData.Feed.Event.Api;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Event;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Event.Extensions;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Realtime.Actor;

/// <summary>
/// Owns the rolling EOD branch of the live futures feed. It consumes routed
/// TickAggregation observations and publishes source/complete/fail over Core
/// NATS without durable replay.
/// </summary>
public class FuturesEodDataRealtimeActor(
    IActorSupervisor supervisor,
    IActorMarketDataFeedEventApiFactory eventApiFactory,
    IRealtimeProjector<FuturesEodDataRealtimeActor> projector,
    IMarketDataApi marketDataApi,
    IBlackboardService blackboardService,
    IStatusConsoleWriter statusConsoleWriter,
    ILogger<FuturesEodDataRealtimeActor> logger)
    : BaseEventActor<FuturesEodDataRealtimeActor>(
        supervisor,
        logger,
        new ActorMailboxId(ActorType.Realtime, ActorName))
{
    public const string ActorName = FuturesEodDataInsertedEvent.Actor;

    static readonly ActorTypeId TickTradeRoute = new(
        ActorType.Realtime,
        FuturesTickTradeDataInsertedEvent.Actor,
        FuturesTickTradeDataInsertedEvent.Verb);

    static readonly ActorTypeId MarketPriceRoute = new(
        ActorType.Realtime,
        FuturesMarketPriceUpdatedRealtimeEvent.Actor,
        FuturesMarketPriceUpdatedRealtimeEvent.Verb);

    static readonly ActorTypeId SessionStatisticsRoute = new(
        ActorType.Realtime,
        FuturesSessionStatisticsUpdatedRealtimeEvent.Actor,
        FuturesSessionStatisticsUpdatedRealtimeEvent.Verb);

    static readonly Dictionary<string, Func<IActorMessage, IEvent>> ParseMap = new()
    {
        [FuturesTickTradeDataInsertedEvent.Verb] =
            message => message.AsEvent<FuturesTickTradeDataInsertedEvent>()!,
        [FuturesMarketPriceUpdatedRealtimeEvent.Verb] =
            message => message.AsEvent<FuturesMarketPriceUpdatedRealtimeEvent>()!,
        [FuturesSessionStatisticsUpdatedRealtimeEvent.Verb] =
            message => message.AsEvent<FuturesSessionStatisticsUpdatedRealtimeEvent>()!,
        [FuturesEodSessionStatisticsUpdatedEvent.Verb] =
            message => message.AsEvent<FuturesEodSessionStatisticsUpdatedEvent>()!,
        [FuturesEodDataInsertedEvent.Verb] =
            message => message.AsEvent<FuturesEodDataInsertedEvent>()!,
        [FuturesEodDataInsertedCompleteEvent.Verb] =
            message => message.AsEvent<FuturesEodDataInsertedCompleteEvent>()!,
        [FuturesEodDataInsertedFailEvent.Verb] =
            message => message.AsEvent<FuturesEodDataInsertedFailEvent>()!,
        [VixFuturesEodDataInsertedEvent.Verb] =
            message => message.AsEvent<VixFuturesEodDataInsertedEvent>()!,
        [VixFuturesEodDataInsertedCompleteEvent.Verb] =
            message => message.AsEvent<VixFuturesEodDataInsertedCompleteEvent>()!,
        [VixFuturesEodDataInsertedFailEvent.Verb] =
            message => message.AsEvent<VixFuturesEodDataInsertedFailEvent>()!
    };

    readonly FuturesEodDataEventParameters _parameters = new(
        blackboardService,
        statusConsoleWriter,
        logger);
    IActorMarketDataFeedEventApi? _eventApi;

    protected override async ValueTask OnStartup(IEventActorContext context)
    {
        _eventApi = eventApiFactory.Create(context);
        await projector.StartAsync(context).ConfigureAwait(false);
        context.AddRealtimeRouter(TickTradeRoute, Id);
        context.AddRealtimeRouter(MarketPriceRoute, Id);
        context.AddRealtimeRouter(SessionStatisticsRoute, Id);
    }

    protected override async ValueTask OnShutdown(IEventActorContext context)
    {
        context.RemoveRealtimeRouter(TickTradeRoute, Id);
        context.RemoveRealtimeRouter(MarketPriceRoute, Id);
        context.RemoveRealtimeRouter(SessionStatisticsRoute, Id);
        await projector.StopAsync().ConfigureAwait(false);
    }

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
            case FuturesTickTradeDataInsertedEvent trade:
                _ = await trade.ExecuteAsync(
                        context,
                        marketDataApi,
                        blackboardService,
                        statusConsoleWriter,
                        projector,
                        logger)
                    .ConfigureAwait(false);
                break;
            case FuturesMarketPriceUpdatedRealtimeEvent priceUpdated:
                _ = await priceUpdated.ExecuteVxQuoteAsync(
                        marketDataApi,
                        projector,
                        statusConsoleWriter,
                        logger)
                    .ConfigureAwait(false);
                break;
            case FuturesSessionStatisticsUpdatedRealtimeEvent statisticsUpdated:
                _ = await statisticsUpdated.ExecuteAsync(context, projector, logger)
                    .ConfigureAwait(false);
                break;
            case FuturesEodDataInsertedEvent inserted:
                blackboardService.MarketDataFeed.FuturesEodData.Set(
                    inserted.FuturesEodData.ContractId,
                    inserted.FuturesEodData.ValueDate,
                    inserted.FuturesEodData);
                break;
            case FuturesEodDataInsertedCompleteEvent completed:
                _ = await completed.ExecuteAsync(GetEventApi(), _parameters)
                    .ConfigureAwait(false);
                break;
            case VixFuturesEodDataInsertedCompleteEvent vixCompleted:
                _ = await vixCompleted.ExecuteAsync(context, _parameters)
                    .ConfigureAwait(false);
                break;
            case FuturesEodDataInsertedFailEvent failed:
                LogProjectionFailure(failed);
                break;
            case VixFuturesEodDataInsertedFailEvent failed:
                LogProjectionFailure(failed);
                break;
            case VixFuturesEodDataInsertedEvent:
            case FuturesEodSessionStatisticsUpdatedEvent:
                break;
            default:
                throw new InvalidOperationException(
                    $"Unable to resolve {ActorName} realtime event from {domainEvent.Subject}.");
        }
    }

    IActorMarketDataFeedEventApi GetEventApi() => _eventApi
        ?? throw new InvalidOperationException($"{ActorName} has not started.");

    void LogProjectionFailure(IErrorEvent failed) => logger.LogErrorEvent(
        ActorName,
        "{EventName} for {EntityId}: {ErrorMessage}; no replay or retry will be attempted",
        failed.EventName,
        failed.Subject.EntityId,
        failed.ErrorMessage);

    protected override async ValueTask OnExceptionAsync(
        IEventActorContext context,
        ActorThreadId threadId,
        IEvent domainEvent,
        Exception exception) =>
        await exception.SendErrorEventAsync<
            TomasAI.IFM.Shared.EventModelActor.Events.EventExceptionEvent,
            ActorEntityId>(ErrorType.EventService, context).ConfigureAwait(false);
}
