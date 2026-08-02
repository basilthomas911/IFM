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

public sealed class ActorMarketDataFeedEventApi(IEventActorContext context)
    : IActorMarketDataFeedEventApi
{
    readonly IEventActorContext _context = IsArgumentNull.Set(context);

    public ValueTask FuturesBarDataStreamingStartedCompleteAsync(FuturesBarDataStreamingStartedEvent e)
        => SendCompleteAsync<FuturesBarDataStreamingStartedEvent, FuturesBarDataStreamingStartedCompleteEvent, FuturesBarDataStreamingId>(e);

    public ValueTask FuturesBarDataStreamingStartedFailAsync(FuturesBarDataStreamingStartedEvent e, Exception ex)
        => SendFailAsync<FuturesBarDataStreamingStartedEvent, FuturesBarDataStreamingStartedFailEvent, FuturesBarDataStreamingId>(e, ex);

    public ValueTask FuturesBarDataStreamingStoppedCompleteAsync(FuturesBarDataStreamingStoppedEvent e)
        => SendCompleteAsync<FuturesBarDataStreamingStoppedEvent, FuturesBarDataStreamingStoppedCompleteEvent, FuturesBarDataStreamingId>(e);

    public ValueTask FuturesBarDataStreamingStoppedFailAsync(FuturesBarDataStreamingStoppedEvent e, Exception ex)
        => SendFailAsync<FuturesBarDataStreamingStoppedEvent, FuturesBarDataStreamingStoppedFailEvent, FuturesBarDataStreamingId>(e, ex);

    public ValueTask FuturesTickDataStreamingStartedCompleteAsync(FuturesTickDataStreamingStartedEvent e)
        => SendCompleteAsync<FuturesTickDataStreamingStartedEvent, FuturesTickDataStreamingStartedCompleteEvent, FuturesTickDataStreamingId>(e);

    public ValueTask FuturesTickDataStreamingStartedFailAsync(FuturesTickDataStreamingStartedEvent e, Exception ex)
        => SendFailAsync<FuturesTickDataStreamingStartedEvent, FuturesTickDataStreamingStartedFailEvent, FuturesTickDataStreamingId>(e, ex);

    public ValueTask FuturesTickDataStreamingStoppedCompleteAsync(FuturesTickDataStreamingStoppedEvent e)
        => SendCompleteAsync<FuturesTickDataStreamingStoppedEvent, FuturesTickDataStreamingStoppedCompleteEvent, FuturesTickDataStreamingId>(e);

    public ValueTask FuturesTickDataStreamingStoppedFailAsync(FuturesTickDataStreamingStoppedEvent e, Exception ex)
        => SendFailAsync<FuturesTickDataStreamingStoppedEvent, FuturesTickDataStreamingStoppedFailEvent, FuturesTickDataStreamingId>(e, ex);

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

    public ValueTask SendFuturesOptionTickDataStreamingStartedCompleteAsync(FuturesOptionTickDataStreamingStartedEvent e)
        => SendCompleteAsync<FuturesOptionTickDataStreamingStartedEvent, FuturesOptionTickDataStreamingStartedCompleteEvent, FuturesOptionTickEntityId>(e);

    public ValueTask SendFuturesOptionTickDataStreamingStartedFailAsync(FuturesOptionTickDataStreamingStartedEvent e, Exception ex)
        => SendFailAsync<FuturesOptionTickDataStreamingStartedEvent, FuturesOptionTickDataStreamingStartedFailEvent, FuturesOptionTickEntityId>(e, ex);

    public ValueTask SendFuturesOptionTickDataStreamingStoppedCompleteAsync(FuturesOptionTickDataStreamingStoppedEvent e)
        => SendCompleteAsync<FuturesOptionTickDataStreamingStoppedEvent, FuturesOptionTickDataStreamingStoppedCompleteEvent, FuturesOptionTickEntityId>(e);

    public ValueTask SendFuturesOptionTickDataStreamingStoppedFailAsync(FuturesOptionTickDataStreamingStoppedEvent e, Exception ex)
        => SendFailAsync<FuturesOptionTickDataStreamingStoppedEvent, FuturesOptionTickDataStreamingStoppedFailEvent, FuturesOptionTickEntityId>(e, ex);

    public ValueTask MarketDataFeedResetCompleteAsync(MarketDataFeedResetEvent e)
        => SendCompleteAsync<MarketDataFeedResetEvent, MarketDataFeedResetCompleteEvent, MarketDataFeedId>(e);

    public ValueTask MarketDataFeedResetFailAsync(MarketDataFeedResetEvent e, Exception ex)
        => SendFailAsync<MarketDataFeedResetEvent, MarketDataFeedResetFailEvent, MarketDataFeedId>(e, ex);

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

    public ValueTask SendMarketDataFeedStartedCompleteAsync(MarketDataFeedStartedEvent e)
        => SendCompleteAsync<MarketDataFeedStartedEvent, MarketDataFeedStartedCompleteEvent, MarketDataFeedId>(e);

    public ValueTask SendMarketDataFeedStartedFailAsync(MarketDataFeedStartedEvent e, Exception ex)
        => SendFailAsync<MarketDataFeedStartedEvent, MarketDataFeedStartedFailEvent, MarketDataFeedId>(e, ex);

    public ValueTask SendMarketDataFeedStoppedCompleteAsync(MarketDataFeedStoppedEvent e)
        => SendCompleteAsync<MarketDataFeedStoppedEvent, MarketDataFeedStoppedCompleteEvent, MarketDataFeedId>(e);

    public ValueTask SendMarketDataFeedStoppedFailAsync(MarketDataFeedStoppedEvent e, Exception ex)
        => SendFailAsync<MarketDataFeedStoppedEvent, MarketDataFeedStoppedFailEvent, MarketDataFeedId>(e, ex);

    public ValueTask SendTradeLiveFeedAddedFailEventAsync(TradeLiveFeedAddedEvent e, Exception ex)
        => SendFailAsync<TradeLiveFeedAddedEvent, TradeLiveFeedAddedFailEvent, TradeLiveFeedId>(e, ex);

    public ValueTask SendTradeLiveFeedRemovedFailEventAsync(TradeLiveFeedRemovedEvent e, Exception ex)
        => SendFailAsync<TradeLiveFeedRemovedEvent, TradeLiveFeedRemovedFailEvent, TradeLiveFeedId>(e, ex);

    public async ValueTask<bool> SendFuturesOptionQuoteDataUpdatedEventAsync(
        FuturesOptionQuoteDataInsertedCompleteEvent e)
    {
        FuturesOptionQuoteDataUpdatedEvent updatedEvent = new(e.OptionQuoteData)
        {
            Subject = new ActorSubject(
                ActorType.Event,
                FuturesOptionQuoteDataUpdatedEvent.Actor,
                FuturesOptionQuoteDataUpdatedEvent.Verb,
                e.EntityId.Format()),
            EntityId = e.EntityId,
            Id = Guid.NewGuid(),
            CommandId = e.CommandId,
            AggregateId = e.AggregateId,
            EventSource = e.EventSource,
            ReceivedOn = DateTime.UtcNow
        };
        await _context.SendAsync<FuturesOptionQuoteDataUpdatedEvent, QuoteId>(updatedEvent);
        return true;
    }

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

public sealed class ActorMarketDataFeedEventApiFactory : IActorMarketDataFeedEventApiFactory
{
    public IActorMarketDataFeedEventApi Create(IEventActorContext context)
        => new ActorMarketDataFeedEventApi(context);
}
