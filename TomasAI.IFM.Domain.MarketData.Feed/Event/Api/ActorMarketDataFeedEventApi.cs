using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Feed.Event.Api;

/// <summary>
/// Sends Market Data Feed lifecycle, streaming, quote, tick, and EOD events from a running event actor.
/// </summary>
/// <remarks>
/// Standard complete and fail operations derive correlated events from the source event. Custom update
/// operations explicitly populate actor subjects and correlation fields before sending through the captured
/// <see cref="IEventActorContext"/>. Create instances through
/// <see cref="ActorMarketDataFeedEventApiFactory"/> and do not share them between actors.
/// </remarks>
public sealed class ActorMarketDataFeedEventApi(IEventActorContext context)
    : IActorMarketDataFeedEventApi
{
    readonly IEventActorContext _context = IsArgumentNull.Set(context);

    /// <summary>
    /// Sends the futures bar data streaming started complete event.
    /// </summary>
    /// <param name="e">The source domain event.</param>
    /// <returns>A value task that completes when the event has been sent.</returns>
    public ValueTask FuturesBarDataStreamingStartedCompleteAsync(FuturesBarDataStreamingStartedEvent e)
        => SendCompleteAsync<FuturesBarDataStreamingStartedEvent, FuturesBarDataStreamingStartedCompleteEvent, FuturesBarDataStreamingId>(e);

    /// <summary>
    /// Sends the futures bar data streaming started fail event.
    /// </summary>
    /// <param name="e">The source domain event.</param>
    /// <param name="ex">The exception that caused the failure.</param>
    /// <returns>A value task that completes when the event has been sent.</returns>
    public ValueTask FuturesBarDataStreamingStartedFailAsync(FuturesBarDataStreamingStartedEvent e, Exception ex)
        => SendFailAsync<FuturesBarDataStreamingStartedEvent, FuturesBarDataStreamingStartedFailEvent, FuturesBarDataStreamingId>(e, ex);

    /// <summary>
    /// Sends the futures bar data streaming stopped complete event.
    /// </summary>
    /// <param name="e">The source domain event.</param>
    /// <returns>A value task that completes when the event has been sent.</returns>
    public ValueTask FuturesBarDataStreamingStoppedCompleteAsync(FuturesBarDataStreamingStoppedEvent e)
        => SendCompleteAsync<FuturesBarDataStreamingStoppedEvent, FuturesBarDataStreamingStoppedCompleteEvent, FuturesBarDataStreamingId>(e);

    /// <summary>
    /// Sends the futures bar data streaming stopped fail event.
    /// </summary>
    /// <param name="e">The source domain event.</param>
    /// <param name="ex">The exception that caused the failure.</param>
    /// <returns>A value task that completes when the event has been sent.</returns>
    public ValueTask FuturesBarDataStreamingStoppedFailAsync(FuturesBarDataStreamingStoppedEvent e, Exception ex)
        => SendFailAsync<FuturesBarDataStreamingStoppedEvent, FuturesBarDataStreamingStoppedFailEvent, FuturesBarDataStreamingId>(e, ex);

    /// <summary>
    /// Sends the futures tick data streaming started complete event.
    /// </summary>
    /// <param name="e">The source domain event.</param>
    /// <returns>A value task that completes when the event has been sent.</returns>
    public ValueTask FuturesTickDataStreamingStartedCompleteAsync(FuturesTickDataStreamingStartedEvent e)
        => SendCompleteAsync<FuturesTickDataStreamingStartedEvent, FuturesTickDataStreamingStartedCompleteEvent, FuturesTickDataStreamingId>(e);

    /// <summary>
    /// Sends the futures tick data streaming started fail event.
    /// </summary>
    /// <param name="e">The source domain event.</param>
    /// <param name="ex">The exception that caused the failure.</param>
    /// <returns>A value task that completes when the event has been sent.</returns>
    public ValueTask FuturesTickDataStreamingStartedFailAsync(FuturesTickDataStreamingStartedEvent e, Exception ex)
        => SendFailAsync<FuturesTickDataStreamingStartedEvent, FuturesTickDataStreamingStartedFailEvent, FuturesTickDataStreamingId>(e, ex);

    /// <summary>
    /// Sends the futures tick data streaming stopped complete event.
    /// </summary>
    /// <param name="e">The source domain event.</param>
    /// <returns>A value task that completes when the event has been sent.</returns>
    public ValueTask FuturesTickDataStreamingStoppedCompleteAsync(FuturesTickDataStreamingStoppedEvent e)
        => SendCompleteAsync<FuturesTickDataStreamingStoppedEvent, FuturesTickDataStreamingStoppedCompleteEvent, FuturesTickDataStreamingId>(e);

    /// <summary>
    /// Sends the futures tick data streaming stopped fail event.
    /// </summary>
    /// <param name="e">The source domain event.</param>
    /// <param name="ex">The exception that caused the failure.</param>
    /// <returns>A value task that completes when the event has been sent.</returns>
    public ValueTask FuturesTickDataStreamingStoppedFailAsync(FuturesTickDataStreamingStoppedEvent e, Exception ex)
        => SendFailAsync<FuturesTickDataStreamingStoppedEvent, FuturesTickDataStreamingStoppedFailEvent, FuturesTickDataStreamingId>(e, ex);

    /// <summary>
    /// Sends the option trade tick price data updated event.
    /// </summary>
    /// <param name="e">The source domain event.</param>
    /// <returns>A value task that completes when the event has been sent.</returns>
    public async ValueTask SendOptionTradeTickPriceDataUpdatedEventAsync(FuturesOptionTickDataInsertedEvent e)
    {
        var entityId = new FuturesOptionTickEntityId(e.TickData.ContractId, e.TickData.ValueDate);
        OptionTradeTickPriceDataUpdatedEvent updatedEvent = new(e.TickData)
        {
            Subject = new ActorSubject(
                ActorType.Event,
                OptionTradeTickPriceDataUpdatedEvent.Actor,
                OptionTradeTickPriceDataUpdatedEvent.Verb,
                entityId.Format()),
            EntityId = entityId,
            Id = Guid.NewGuid(),
            CommandId = e.CommandId,
            AggregateId = e.AggregateId,
            ReceivedOn = DateTime.UtcNow
        };
        await _context.SendAsync<OptionTradeTickPriceDataUpdatedEvent, FuturesOptionTickEntityId>(updatedEvent);
    }

    /// <summary>
    /// Sends the futures option tick data streaming started complete event.
    /// </summary>
    /// <param name="e">The source domain event.</param>
    /// <returns>A value task that completes when the event has been sent.</returns>
    public ValueTask SendFuturesOptionTickDataStreamingStartedCompleteAsync(FuturesOptionTickDataStreamingStartedEvent e)
        => SendCompleteAsync<FuturesOptionTickDataStreamingStartedEvent, FuturesOptionTickDataStreamingStartedCompleteEvent, FuturesOptionTickEntityId>(e);

    /// <summary>
    /// Sends the futures option tick data streaming started fail event.
    /// </summary>
    /// <param name="e">The source domain event.</param>
    /// <param name="ex">The exception that caused the failure.</param>
    /// <returns>A value task that completes when the event has been sent.</returns>
    public ValueTask SendFuturesOptionTickDataStreamingStartedFailAsync(FuturesOptionTickDataStreamingStartedEvent e, Exception ex)
        => SendFailAsync<FuturesOptionTickDataStreamingStartedEvent, FuturesOptionTickDataStreamingStartedFailEvent, FuturesOptionTickEntityId>(e, ex);

    /// <summary>
    /// Sends the futures option tick data streaming stopped complete event.
    /// </summary>
    /// <param name="e">The source domain event.</param>
    /// <returns>A value task that completes when the event has been sent.</returns>
    public ValueTask SendFuturesOptionTickDataStreamingStoppedCompleteAsync(FuturesOptionTickDataStreamingStoppedEvent e)
        => SendCompleteAsync<FuturesOptionTickDataStreamingStoppedEvent, FuturesOptionTickDataStreamingStoppedCompleteEvent, FuturesOptionTickEntityId>(e);

    /// <summary>
    /// Sends the futures option tick data streaming stopped fail event.
    /// </summary>
    /// <param name="e">The source domain event.</param>
    /// <param name="ex">The exception that caused the failure.</param>
    /// <returns>A value task that completes when the event has been sent.</returns>
    public ValueTask SendFuturesOptionTickDataStreamingStoppedFailAsync(FuturesOptionTickDataStreamingStoppedEvent e, Exception ex)
        => SendFailAsync<FuturesOptionTickDataStreamingStoppedEvent, FuturesOptionTickDataStreamingStoppedFailEvent, FuturesOptionTickEntityId>(e, ex);

    /// <summary>
    /// Sends the market data feed reset complete event.
    /// </summary>
    /// <param name="e">The source domain event.</param>
    /// <returns>A value task that completes when the event has been sent.</returns>
    public ValueTask MarketDataFeedResetCompleteAsync(MarketDataFeedResetEvent e)
        => SendCompleteAsync<MarketDataFeedResetEvent, MarketDataFeedResetCompleteEvent, MarketDataFeedId>(e);

    /// <summary>
    /// Sends the market data feed reset fail event.
    /// </summary>
    /// <param name="e">The source domain event.</param>
    /// <param name="ex">The exception that caused the failure.</param>
    /// <returns>A value task that completes when the event has been sent.</returns>
    public ValueTask MarketDataFeedResetFailAsync(MarketDataFeedResetEvent e, Exception ex)
        => SendFailAsync<MarketDataFeedResetEvent, MarketDataFeedResetFailEvent, MarketDataFeedId>(e, ex);

    /// <summary>
    /// Sends the reset streaming event.
    /// </summary>
    /// <param name="e">The source domain event.</param>
    /// <returns>A value task that completes when the event has been sent.</returns>
    public async ValueTask SendResetStreamingEventAsync(MarketDataFeedResetCompleteEvent e)
    {
        MarketDataFeedResetStreamingEvent resetStreamingEvent = new()
        {
            Subject = new ActorSubject(
                ActorType.Event,
                MarketDataFeedResetStreamingEvent.Actor,
                MarketDataFeedResetStreamingEvent.Verb,
                e.EntityId.Format()),
            EntityId = e.EntityId,
            CommandId = e.CommandId
        };
        await _context.SendAsync<MarketDataFeedResetStreamingEvent, MarketDataFeedId>(resetStreamingEvent);
    }

    /// <summary>
    /// Sends the market data feed started complete event.
    /// </summary>
    /// <param name="e">The source domain event.</param>
    /// <returns>A value task that completes when the event has been sent.</returns>
    public ValueTask SendMarketDataFeedStartedCompleteAsync(MarketDataFeedStartedEvent e)
        => SendCompleteAsync<MarketDataFeedStartedEvent, MarketDataFeedStartedCompleteEvent, MarketDataFeedId>(e);

    /// <summary>
    /// Sends the market data feed started fail event.
    /// </summary>
    /// <param name="e">The source domain event.</param>
    /// <param name="ex">The exception that caused the failure.</param>
    /// <returns>A value task that completes when the event has been sent.</returns>
    public ValueTask SendMarketDataFeedStartedFailAsync(MarketDataFeedStartedEvent e, Exception ex)
        => SendFailAsync<MarketDataFeedStartedEvent, MarketDataFeedStartedFailEvent, MarketDataFeedId>(e, ex);

    /// <summary>
    /// Sends the market data feed stopped complete event.
    /// </summary>
    /// <param name="e">The source domain event.</param>
    /// <returns>A value task that completes when the event has been sent.</returns>
    public ValueTask SendMarketDataFeedStoppedCompleteAsync(MarketDataFeedStoppedEvent e)
        => SendCompleteAsync<MarketDataFeedStoppedEvent, MarketDataFeedStoppedCompleteEvent, MarketDataFeedId>(e);

    /// <summary>
    /// Sends the market data feed stopped fail event.
    /// </summary>
    /// <param name="e">The source domain event.</param>
    /// <param name="ex">The exception that caused the failure.</param>
    /// <returns>A value task that completes when the event has been sent.</returns>
    public ValueTask SendMarketDataFeedStoppedFailAsync(MarketDataFeedStoppedEvent e, Exception ex)
        => SendFailAsync<MarketDataFeedStoppedEvent, MarketDataFeedStoppedFailEvent, MarketDataFeedId>(e, ex);

    /// <summary>
    /// Sends the trade live feed added fail event.
    /// </summary>
    /// <param name="e">The source domain event.</param>
    /// <param name="ex">The exception that caused the failure.</param>
    /// <returns>A value task that completes when the event has been sent.</returns>
    public ValueTask SendTradeLiveFeedAddedFailEventAsync(TradeLiveFeedAddedEvent e, Exception ex)
        => SendFailAsync<TradeLiveFeedAddedEvent, TradeLiveFeedAddedFailEvent, TradeLiveFeedId>(e, ex);

    /// <summary>
    /// Sends the trade live feed removed fail event.
    /// </summary>
    /// <param name="e">The source domain event.</param>
    /// <param name="ex">The exception that caused the failure.</param>
    /// <returns>A value task that completes when the event has been sent.</returns>
    public ValueTask SendTradeLiveFeedRemovedFailEventAsync(TradeLiveFeedRemovedEvent e, Exception ex)
        => SendFailAsync<TradeLiveFeedRemovedEvent, TradeLiveFeedRemovedFailEvent, TradeLiveFeedId>(e, ex);

    /// <summary>
    /// Sends the futures EOD data updated event.
    /// </summary>
    /// <param name="e">The source domain event.</param>
    /// <returns>A value task that completes when the event has been sent.</returns>
    public async ValueTask SendFuturesEodDataUpdatedEventAsync(FuturesEodDataInsertedEvent e)
    {
        var entityId = e.EntityId;
        FuturesEodDataUpdatedEvent updatedEvent = new()
        {
            Subject = new ActorSubject(
                ActorType.Event,
                FuturesEodDataUpdatedEvent.Actor,
                FuturesEodDataUpdatedEvent.Verb,
                entityId.Format()),
            EntityId = entityId,
            CommandId = e.CommandId,
            AggregateId = e.AggregateId,
            EventSource = e.EventSource,
            ReceivedOn = e.ReceivedOn,
            FuturesEodData = e.FuturesEodData,
            UpdatedOn = DateTime.Now,
            UpdatedBy = e.UserName
        };
        await _context.SendAsync<FuturesEodDataUpdatedEvent, FuturesEodDataId>(updatedEvent);
    }

    async ValueTask SendCompleteAsync<TEvent, TCompleteEvent, TEntityId>(TEvent e)
        where TEvent : class, IEvent<TEntityId>
        where TCompleteEvent : class, ICompleteEvent<TEntityId>, new()
        where TEntityId : IActorEntityId
    {
        var completeEvent = e.ToCompleteEvent<TCompleteEvent, TEntityId>() as TCompleteEvent;
        await _context.SendAsync<TCompleteEvent, TEntityId>(completeEvent!);
    }

    async ValueTask SendFailAsync<TEvent, TFailEvent, TEntityId>(TEvent e, Exception ex)
        where TEvent : class, IEvent<TEntityId>
        where TFailEvent : class, IErrorEvent<TEntityId>, new()
        where TEntityId : IActorEntityId
    {
        var failEvent = e.ToFailEvent<TFailEvent, TEntityId>(ex) as TFailEvent;
        await _context.SendAsync<TFailEvent, TEntityId>(failEvent!);
    }
}

/// <summary>
/// Creates Market Data Feed event APIs bound to a running event actor.
/// </summary>
public sealed class ActorMarketDataFeedEventApiFactory : IActorMarketDataFeedEventApiFactory
{
    /// <summary>
    /// Creates an event API that sends through the supplied actor context.
    /// </summary>
    /// <param name="context">The actor context used to send Market Data Feed events.</param>
    /// <returns>A context-bound Market Data Feed event API.</returns>
    public IActorMarketDataFeedEventApi Create(IEventActorContext context)
        => new ActorMarketDataFeedEventApi(context);
}
