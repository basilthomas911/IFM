using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Feed.Event.Extensions;

/// <summary>
/// Sends Market Data Feed lifecycle, streaming, quote, tick, and EOD events from a running event actor.
/// </summary>
/// <remarks>
/// Standard complete and fail operations derive correlated events from the source event. Custom update
/// operations explicitly populate actor subjects and correlation fields before sending through the captured
/// <see cref="IEventActorContext"/>.
/// </remarks>
public static class MarketDataFeedEventApiExtensions
{
    extension(IEventActorContext context)
    {

    /// <summary>
    /// Sends the futures bar data streaming started complete event.
    /// </summary>
    /// <param name="e">The source domain event.</param>
    /// <returns>A value task that completes when the event has been sent.</returns>
    public ValueTask FuturesBarDataStreamingStartedCompleteAsync(FuturesBarDataStreamingStartedEvent e)
        => context.SendCompleteAsync<FuturesBarDataStreamingStartedEvent, FuturesBarDataStreamingStartedCompleteEvent, FuturesBarDataStreamingId>(e);

    /// <summary>
    /// Sends the futures bar data streaming started fail event.
    /// </summary>
    /// <param name="e">The source domain event.</param>
    /// <param name="ex">The exception that caused the failure.</param>
    /// <returns>A value task that completes when the event has been sent.</returns>
    public ValueTask FuturesBarDataStreamingStartedFailAsync(FuturesBarDataStreamingStartedEvent e, Exception ex)
        => context.SendFailAsync<FuturesBarDataStreamingStartedEvent, FuturesBarDataStreamingStartedFailEvent, FuturesBarDataStreamingId>(e, ex);

    /// <summary>
    /// Sends the futures bar data streaming stopped complete event.
    /// </summary>
    /// <param name="e">The source domain event.</param>
    /// <returns>A value task that completes when the event has been sent.</returns>
    public ValueTask FuturesBarDataStreamingStoppedCompleteAsync(FuturesBarDataStreamingStoppedEvent e)
        => context.SendCompleteAsync<FuturesBarDataStreamingStoppedEvent, FuturesBarDataStreamingStoppedCompleteEvent, FuturesBarDataStreamingId>(e);

    /// <summary>
    /// Sends the futures bar data streaming stopped fail event.
    /// </summary>
    /// <param name="e">The source domain event.</param>
    /// <param name="ex">The exception that caused the failure.</param>
    /// <returns>A value task that completes when the event has been sent.</returns>
    public ValueTask FuturesBarDataStreamingStoppedFailAsync(FuturesBarDataStreamingStoppedEvent e, Exception ex)
        => context.SendFailAsync<FuturesBarDataStreamingStoppedEvent, FuturesBarDataStreamingStoppedFailEvent, FuturesBarDataStreamingId>(e, ex);

    /// <summary>
    /// Sends the futures tick data streaming started complete event.
    /// </summary>
    /// <param name="e">The source domain event.</param>
    /// <returns>A value task that completes when the event has been sent.</returns>
    public ValueTask FuturesTickDataStreamingStartedCompleteAsync(FuturesTickDataStreamingStartedEvent e)
        => context.SendCompleteAsync<FuturesTickDataStreamingStartedEvent, FuturesTickDataStreamingStartedCompleteEvent, FuturesTickDataStreamingId>(e);

    /// <summary>
    /// Sends the futures tick data streaming started fail event.
    /// </summary>
    /// <param name="e">The source domain event.</param>
    /// <param name="ex">The exception that caused the failure.</param>
    /// <returns>A value task that completes when the event has been sent.</returns>
    public ValueTask FuturesTickDataStreamingStartedFailAsync(FuturesTickDataStreamingStartedEvent e, Exception ex)
        => context.SendFailAsync<FuturesTickDataStreamingStartedEvent, FuturesTickDataStreamingStartedFailEvent, FuturesTickDataStreamingId>(e, ex);

    /// <summary>
    /// Sends the futures tick data streaming stopped complete event.
    /// </summary>
    /// <param name="e">The source domain event.</param>
    /// <returns>A value task that completes when the event has been sent.</returns>
    public ValueTask FuturesTickDataStreamingStoppedCompleteAsync(FuturesTickDataStreamingStoppedEvent e)
        => context.SendCompleteAsync<FuturesTickDataStreamingStoppedEvent, FuturesTickDataStreamingStoppedCompleteEvent, FuturesTickDataStreamingId>(e);

    /// <summary>
    /// Sends the futures tick data streaming stopped fail event.
    /// </summary>
    /// <param name="e">The source domain event.</param>
    /// <param name="ex">The exception that caused the failure.</param>
    /// <returns>A value task that completes when the event has been sent.</returns>
    public ValueTask FuturesTickDataStreamingStoppedFailAsync(FuturesTickDataStreamingStoppedEvent e, Exception ex)
        => context.SendFailAsync<FuturesTickDataStreamingStoppedEvent, FuturesTickDataStreamingStoppedFailEvent, FuturesTickDataStreamingId>(e, ex);

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
                ActorType.Notify,
                OptionTradeTickPriceDataUpdatedEvent.Actor,
                OptionTradeTickPriceDataUpdatedEvent.Verb,
                entityId.Format()),
            EntityId = entityId,
            Id = Guid.NewGuid(),
            CommandId = e.CommandId,
            AggregateId = e.AggregateId,
            ReceivedOn = DateTime.UtcNow
        };
        await context.SendAsync<OptionTradeTickPriceDataUpdatedEvent, FuturesOptionTickEntityId>(updatedEvent);
    }

    /// <summary>
    /// Publishes the option-domain price update translated from a realtime TickAggregation trade event.
    /// </summary>
    public async ValueTask SendOptionTradeTickPriceDataUpdatedEventAsync(
        FuturesTickTradeDataInsertedEvent e,
        FuturesOptionTickDataV2ReadModel tickData)
    {
        ArgumentNullException.ThrowIfNull(e);
        ArgumentNullException.ThrowIfNull(tickData);
        var entityId = new FuturesOptionTickEntityId(
            tickData.ContractId,
            tickData.ValueDate);
        OptionTradeTickPriceDataUpdatedEvent updatedEvent = new(tickData)
        {
            Subject = new ActorSubject(
                ActorType.Notify,
                OptionTradeTickPriceDataUpdatedEvent.Actor,
                OptionTradeTickPriceDataUpdatedEvent.Verb,
                entityId.Format()),
            EntityId = entityId,
            Id = Guid.NewGuid(),
            CommandId = e.CommandId,
            AggregateId = e.AggregateId,
            EventSource = nameof(FuturesTickTradeDataInsertedEvent),
            ReceivedOn = DateTime.UtcNow
        };
        await context.SendAsync<OptionTradeTickPriceDataUpdatedEvent, FuturesOptionTickEntityId>(
            updatedEvent).ConfigureAwait(false);
    }

    /// <summary>
    /// Sends the futures option tick data streaming started complete event.
    /// </summary>
    /// <param name="e">The source domain event.</param>
    /// <returns>A value task that completes when the event has been sent.</returns>
    public ValueTask SendFuturesOptionTickDataStreamingStartedCompleteAsync(FuturesOptionTickDataStreamingStartedEvent e)
        => context.SendCompleteAsync<FuturesOptionTickDataStreamingStartedEvent, FuturesOptionTickDataStreamingStartedCompleteEvent, FuturesOptionTickEntityId>(e);

    /// <summary>
    /// Sends the futures option tick data streaming started fail event.
    /// </summary>
    /// <param name="e">The source domain event.</param>
    /// <param name="ex">The exception that caused the failure.</param>
    /// <returns>A value task that completes when the event has been sent.</returns>
    public ValueTask SendFuturesOptionTickDataStreamingStartedFailAsync(FuturesOptionTickDataStreamingStartedEvent e, Exception ex)
        => context.SendFailAsync<FuturesOptionTickDataStreamingStartedEvent, FuturesOptionTickDataStreamingStartedFailEvent, FuturesOptionTickEntityId>(e, ex);

    /// <summary>
    /// Sends the futures option tick data streaming stopped complete event.
    /// </summary>
    /// <param name="e">The source domain event.</param>
    /// <returns>A value task that completes when the event has been sent.</returns>
    public ValueTask SendFuturesOptionTickDataStreamingStoppedCompleteAsync(FuturesOptionTickDataStreamingStoppedEvent e)
        => context.SendCompleteAsync<FuturesOptionTickDataStreamingStoppedEvent, FuturesOptionTickDataStreamingStoppedCompleteEvent, FuturesOptionTickEntityId>(e);

    /// <summary>
    /// Sends the futures option tick data streaming stopped fail event.
    /// </summary>
    /// <param name="e">The source domain event.</param>
    /// <param name="ex">The exception that caused the failure.</param>
    /// <returns>A value task that completes when the event has been sent.</returns>
    public ValueTask SendFuturesOptionTickDataStreamingStoppedFailAsync(FuturesOptionTickDataStreamingStoppedEvent e, Exception ex)
        => context.SendFailAsync<FuturesOptionTickDataStreamingStoppedEvent, FuturesOptionTickDataStreamingStoppedFailEvent, FuturesOptionTickEntityId>(e, ex);

    /// <summary>
    /// Sends the market data feed reset complete event.
    /// </summary>
    /// <param name="e">The source domain event.</param>
    /// <returns>A value task that completes when the event has been sent.</returns>
    public ValueTask MarketDataFeedResetCompleteAsync(MarketDataFeedResetEvent e)
        => context.SendCompleteAsync<MarketDataFeedResetEvent, MarketDataFeedResetCompleteEvent, MarketDataFeedId>(e);

    /// <summary>
    /// Sends the market data feed reset fail event.
    /// </summary>
    /// <param name="e">The source domain event.</param>
    /// <param name="ex">The exception that caused the failure.</param>
    /// <returns>A value task that completes when the event has been sent.</returns>
    public ValueTask MarketDataFeedResetFailAsync(MarketDataFeedResetEvent e, Exception ex)
        => context.SendFailAsync<MarketDataFeedResetEvent, MarketDataFeedResetFailEvent, MarketDataFeedId>(e, ex);

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
        await context.SendAsync<MarketDataFeedResetStreamingEvent, MarketDataFeedId>(resetStreamingEvent);
    }

    /// <summary>
    /// Sends the market data feed started complete event.
    /// </summary>
    /// <param name="e">The source domain event.</param>
    /// <returns>A value task that completes when the event has been sent.</returns>
    public ValueTask SendMarketDataFeedStartedCompleteAsync(MarketDataFeedStartedEvent e)
        => context.SendCompleteAsync<MarketDataFeedStartedEvent, MarketDataFeedStartedCompleteEvent, MarketDataFeedId>(e);

    /// <summary>
    /// Sends the market data feed started fail event.
    /// </summary>
    /// <param name="e">The source domain event.</param>
    /// <param name="ex">The exception that caused the failure.</param>
    /// <returns>A value task that completes when the event has been sent.</returns>
    public ValueTask SendMarketDataFeedStartedFailAsync(MarketDataFeedStartedEvent e, Exception ex)
        => context.SendFailAsync<MarketDataFeedStartedEvent, MarketDataFeedStartedFailEvent, MarketDataFeedId>(e, ex);

    /// <summary>
    /// Sends the market data feed stopped complete event.
    /// </summary>
    /// <param name="e">The source domain event.</param>
    /// <returns>A value task that completes when the event has been sent.</returns>
    public ValueTask SendMarketDataFeedStoppedCompleteAsync(MarketDataFeedStoppedEvent e)
        => context.SendCompleteAsync<MarketDataFeedStoppedEvent, MarketDataFeedStoppedCompleteEvent, MarketDataFeedId>(e);

    /// <summary>
    /// Sends the market data feed stopped fail event.
    /// </summary>
    /// <param name="e">The source domain event.</param>
    /// <param name="ex">The exception that caused the failure.</param>
    /// <returns>A value task that completes when the event has been sent.</returns>
    public ValueTask SendMarketDataFeedStoppedFailAsync(MarketDataFeedStoppedEvent e, Exception ex)
        => context.SendFailAsync<MarketDataFeedStoppedEvent, MarketDataFeedStoppedFailEvent, MarketDataFeedId>(e, ex);

    /// <summary>
    /// Sends the trade live feed added fail event.
    /// </summary>
    /// <param name="e">The source domain event.</param>
    /// <param name="ex">The exception that caused the failure.</param>
    /// <returns>A value task that completes when the event has been sent.</returns>
    public ValueTask SendTradeLiveFeedAddedFailEventAsync(TradeLiveFeedAddedEvent e, Exception ex)
        => context.SendFailAsync<TradeLiveFeedAddedEvent, TradeLiveFeedAddedFailEvent, TradeLiveFeedId>(e, ex);

    /// <summary>
    /// Sends the trade live feed removed fail event.
    /// </summary>
    /// <param name="e">The source domain event.</param>
    /// <param name="ex">The exception that caused the failure.</param>
    /// <returns>A value task that completes when the event has been sent.</returns>
    public ValueTask SendTradeLiveFeedRemovedFailEventAsync(TradeLiveFeedRemovedEvent e, Exception ex)
        => context.SendFailAsync<TradeLiveFeedRemovedEvent, TradeLiveFeedRemovedFailEvent, TradeLiveFeedId>(e, ex);

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
        await context.SendAsync<FuturesEodDataUpdatedEvent, FuturesEodDataId>(updatedEvent);
    }

    /// <summary>
    /// Publishes a best-effort external notification after the futures EOD projection has completed.
    /// </summary>
    public async ValueTask SendFuturesEodDataUpdatedNotifyEventAsync(
        FuturesEodDataInsertedCompleteEvent e)
    {
        ArgumentNullException.ThrowIfNull(e);
        var notifyEvent = new FuturesEodDataUpdatedNotifyEvent
        {
            Subject = new ActorSubject(
                ActorType.Notify,
                FuturesEodDataUpdatedNotifyEvent.Actor,
                FuturesEodDataUpdatedNotifyEvent.Verb,
                e.EntityId.Format()),
            Id = Guid.NewGuid(),
            EntityId = e.EntityId,
            CommandId = e.CommandId,
            AggregateId = e.AggregateId ?? string.Empty,
            EventSource = nameof(FuturesEodDataInsertedCompleteEvent),
            ReceivedOn = DateTime.UtcNow,
            FuturesEodData = e.FuturesEodData
        };
        await context.SendAsync<FuturesEodDataUpdatedNotifyEvent, FuturesEodDataId>(
            notifyEvent).ConfigureAwait(false);
    }

    async ValueTask SendCompleteAsync<TEvent, TCompleteEvent, TEntityId>(TEvent e)
        where TEvent : class, IEvent<TEntityId>
        where TCompleteEvent : class, ICompleteEvent<TEntityId>, new()
        where TEntityId : IActorEntityId
    {
        var completeEvent = e.ToCompleteEvent<TCompleteEvent, TEntityId>() as TCompleteEvent;
        await context.SendAsync<TCompleteEvent, TEntityId>(completeEvent!);
    }

    async ValueTask SendFailAsync<TEvent, TFailEvent, TEntityId>(TEvent e, Exception ex)
        where TEvent : class, IEvent<TEntityId>
        where TFailEvent : class, IErrorEvent<TEntityId>, new()
        where TEntityId : IActorEntityId
    {
        var failEvent = e.ToFailEvent<TFailEvent, TEntityId>(ex) as TFailEvent;
        await context.SendAsync<TFailEvent, TEntityId>(failEvent!);
    }
    }
}
