using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared.ServiceApi;

/// <summary>
/// Defines NATS-backed Market Data Feed events intended for use by domain event actors.
/// </summary>
public interface IActorMarketDataFeedEventApi
{
    ValueTask FuturesBarDataStreamingStartedCompleteAsync(FuturesBarDataStreamingStartedEvent e);
    ValueTask FuturesBarDataStreamingStartedFailAsync(FuturesBarDataStreamingStartedEvent e, Exception ex);
    ValueTask FuturesBarDataStreamingStoppedCompleteAsync(FuturesBarDataStreamingStoppedEvent e);
    ValueTask FuturesBarDataStreamingStoppedFailAsync(FuturesBarDataStreamingStoppedEvent e, Exception ex);

    ValueTask FuturesTickDataStreamingStartedCompleteAsync(FuturesTickDataStreamingStartedEvent e);
    ValueTask FuturesTickDataStreamingStartedFailAsync(FuturesTickDataStreamingStartedEvent e, Exception ex);
    ValueTask FuturesTickDataStreamingStoppedCompleteAsync(FuturesTickDataStreamingStoppedEvent e);
    ValueTask FuturesTickDataStreamingStoppedFailAsync(FuturesTickDataStreamingStoppedEvent e, Exception ex);

    ValueTask SendOptionTradeTickPriceDataUpdatedEventAsync(FuturesOptionTickDataInsertedEvent e);
    ValueTask SendFuturesOptionTickDataStreamingStartedCompleteAsync(FuturesOptionTickDataStreamingStartedEvent e);
    ValueTask SendFuturesOptionTickDataStreamingStartedFailAsync(FuturesOptionTickDataStreamingStartedEvent e, Exception ex);
    ValueTask SendFuturesOptionTickDataStreamingStoppedCompleteAsync(FuturesOptionTickDataStreamingStoppedEvent e);
    ValueTask SendFuturesOptionTickDataStreamingStoppedFailAsync(FuturesOptionTickDataStreamingStoppedEvent e, Exception ex);

    ValueTask MarketDataFeedResetCompleteAsync(MarketDataFeedResetEvent e);
    ValueTask MarketDataFeedResetFailAsync(MarketDataFeedResetEvent e, Exception ex);
    ValueTask SendResetStreamingEventAsync(MarketDataFeedResetCompleteEvent e);
    ValueTask SendMarketDataFeedStartedCompleteAsync(MarketDataFeedStartedEvent e);
    ValueTask SendMarketDataFeedStartedFailAsync(MarketDataFeedStartedEvent e, Exception ex);
    ValueTask SendMarketDataFeedStoppedCompleteAsync(MarketDataFeedStoppedEvent e);
    ValueTask SendMarketDataFeedStoppedFailAsync(MarketDataFeedStoppedEvent e, Exception ex);
    ValueTask SendTradeLiveFeedAddedFailEventAsync(TradeLiveFeedAddedEvent e, Exception ex);
    ValueTask SendTradeLiveFeedRemovedFailEventAsync(TradeLiveFeedRemovedEvent e, Exception ex);

    ValueTask<bool> SendFuturesOptionQuoteDataUpdatedEventAsync(FuturesOptionQuoteDataInsertedCompleteEvent e);
    ValueTask SendFuturesEodDataUpdatedEventAsync(FuturesEodDataInsertedEvent e);
}

public interface IActorMarketDataFeedEventApiFactory
{
    IActorMarketDataFeedEventApi Create(IEventActorContext context);
}
