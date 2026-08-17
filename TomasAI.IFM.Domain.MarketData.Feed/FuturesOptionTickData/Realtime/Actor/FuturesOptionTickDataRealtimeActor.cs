using Microsoft.Extensions.Logging;
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
public class FuturesOptionTickDataRealtimeActor(
    IActorSupervisor supervisor,
    IActorMarketDataFeedEventApiFactory eventApiFactory,
    IMarketDataApi marketDataApi,
    IStatusConsoleWriter statusConsoleWriter,
    ILogger<FuturesOptionTickDataRealtimeActor> logger)
    : BaseEventActor<FuturesOptionTickDataRealtimeActor>(
        supervisor,
        logger,
        new ActorMailboxId(ActorType.Realtime, ActorName))
{
    public const string ActorName = "FuturesOptionTickDataRealtime";

    static readonly ActorTypeId TickTradeRoute = new(
        ActorType.Realtime,
        FuturesTickTradeDataInsertedEvent.Actor,
        FuturesTickTradeDataInsertedEvent.Verb);

    IActorMarketDataFeedEventApi? _eventApi;

    protected override ValueTask OnStartup(IEventActorContext context)
    {
        _eventApi = eventApiFactory.Create(context);
        context.AddRealtimeRouter(TickTradeRoute, Id);
        return ValueTask.CompletedTask;
    }

    protected override ValueTask OnShutdown(IEventActorContext context)
    {
        context.RemoveRealtimeRouter(TickTradeRoute, Id);
        return ValueTask.CompletedTask;
    }

    protected override IEvent ParseMessage(
        IEventActorContext context,
        IActorMessage message)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(message);
        if (message.Subject is not
            {
                ActorType: ActorType.Realtime,
                Name: ActorName,
                Verb: FuturesTickTradeDataInsertedEvent.Verb
            })
            return default!;

        var domainEvent = message.AsEvent<FuturesTickTradeDataInsertedEvent>();
        ArgumentNullException.ThrowIfNull(domainEvent);
        domainEvent.CheckForEmptyCommandId();
        return domainEvent;
    }

    protected override async ValueTask ReceiveAsync(
        IEventActorContext context,
        IEvent domainEvent)
    {
        if (domainEvent is not FuturesTickTradeDataInsertedEvent trade)
        {
            throw new InvalidOperationException(
                $"Unable to resolve {ActorName} realtime event from {domainEvent.Subject}.");
        }

        _ = await trade.ExecuteAsync(
                _eventApi ?? throw new InvalidOperationException($"{ActorName} has not started."),
                marketDataApi,
                statusConsoleWriter,
                logger)
            .ConfigureAwait(false);
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
