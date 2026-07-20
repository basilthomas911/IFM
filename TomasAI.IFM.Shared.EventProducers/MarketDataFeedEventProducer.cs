using Microsoft.Extensions.Logging;
using TomasAI.IFM.Framework.Messaging.Kafka;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.MarketDataFeed.Events;
using TomasAI.IFM.Shared.MarketDataFeed.ServiceApi;
using TomasAI.IFM.Shared.StatusConsole.Events;
using TomasAI.IFM.Shared.Trade.Events;

namespace TomasAI.IFM.Shared.EventProducers;

/// <summary>
/// Produces and publishes market data feed events to an event broker using Kafka. Implements event production for
/// various market data scenarios, including streaming, insertion, and update events.
/// </summary>
/// <remarks>Use this class to send market data feed events such as tick data, bar data, option quotes, and feed
/// status updates to the event broker. The producer supports a wide range of event types relevant to futures and
/// options trading workflows. Thread safety and reliability depend on the underlying KafkaEventProducer implementation.
/// For test scenarios, a parameterless constructor is available; production usage should use the constructor accepting
/// options and logger for proper configuration.</remarks>
public class MarketDataFeedEventProducer : KafkaEventProducer, IMarketDataFeedEventProducer
{
    /// <summary>
    /// market data event producer
    /// </summary>
    /// <param name="options"></param>
    /// <param name="logger"></param>
    public MarketDataFeedEventProducer(IEventProducerOptions options, ILogger<MarketDataFeedEventProducer> logger) 
        : base(options, logger)
    {
    }

    /// <summary>
    /// for BDD Test usage only
    /// </summary>
    public MarketDataFeedEventProducer()
    {
    }

    /// <summary>
    /// send market data feed events to event broker
    /// </summary>
    /// <param name="event"></param>
    /// <returns></returns>
    public override async Task PostEventAsync(IEvent @event)
    {
        @event.SetEventSource($"{EventTopic.MarketDataFeedEvents}");
        await (@event switch
        {
            // start/stop data streaming events...
            FuturesTickDataStreamingStartedEvent e => SendEventAsync(e.CommandId, e),
            FuturesTickDataStreamingStoppedEvent e => SendEventAsync(e.CommandId, e),
            FuturesOptionTickDataStreamingStartedEvent e => SendEventAsync(e.CommandId, e),
            FuturesOptionTickDataStreamingStartedCompleteEvent e => SendEventAsync(e.CommandId, e),
            FuturesOptionTickDataStreamingStartedFailEvent e => SendEventAsync(e.CommandId, e),
            FuturesOptionTickDataStreamingStoppedEvent e => SendEventAsync(e.CommandId, e),
            FuturesBarDataStreamingStartedEvent e => SendEventAsync(e.CommandId, e),
            FuturesBarDataStreamingStoppedEvent e => SendEventAsync(e.CommandId, e),

            // futures tick data events...
            FuturesTickDataInsertedEvent e => StreamEventAsync(e.TickData.ContractId, e),
            FuturesTickDataInsertedCompleteEvent e => StreamEventAsync(e.CommandId, e),
            FuturesTickDataInsertedFailEvent e => SendEventAsync(e.CommandId, e),

            // futures option tick data events...
            FuturesOptionTickDataInsertedEvent e => StreamEventAsync(e.TickData.ContractId, e),
            FuturesOptionTickDataInsertedCompleteEvent e => StreamEventAsync(e.CommandId, e),
            FuturesOptionTickDataInsertedFailEvent e => SendEventAsync(e.CommandId, e),

            // futues option quote events...
            FuturesOptionQuoteDataStreamingStartedEvent e => SendEventAsync(e.QuoteId, e),
            FuturesOptionQuoteDataStreamingStartedCompleteEvent e => SendEventAsync(e.CommandId, e),
            FuturesOptionQuoteDataStreamingStartedFailEvent e => SendEventAsync(e.CommandId, e),
            FuturesOptionQuoteDataStreamingStoppedEvent e => SendEventAsync(e.QuoteId, e),
            FuturesOptionQuoteDataStreamingStoppedCompleteEvent e => SendEventAsync(e.CommandId, e),
            FuturesOptionQuoteDataStreamingStoppedFailEvent e => SendEventAsync(e.CommandId, e),
            FuturesOptionQuoteDataInsertedEvent e => StreamEventAsync(e.QuoteId, e),
            FuturesOptionQuoteDataInsertedCompleteEvent e => SendEventAsync(e.CommandId, e),
            FuturesOptionQuoteDataInsertedFailEvent e => SendEventAsync(e.CommandId, e),
            FuturesOptionQuoteStreamingDataEvent e => StreamEventAsync(e.QuoteId,e),

            // futures bar data events...
            FuturesBarDataInsertedEvent e => StreamEventAsync(e.FuturesBarData.ContractId, e),
            FuturesBarDataInsertedCompleteEvent e => StreamEventAsync(e.CommandId, e),
            FuturesBarDataInsertedFailEvent e => SendEventAsync(e.CommandId, e),
            FuturesBarDataDeletedEvent e => StreamEventAsync(e.CommandId, e),
            FuturesBarDataDeletedCompleteEvent e => StreamEventAsync(e.CommandId, e),
            FuturesBarDataDeletedFailEvent e => SendEventAsync(e.CommandId, e),

            // futures eod data events...
            FuturesEodDataInsertedEvent e => StreamEventAsync(e.FuturesEodData.ContractId, e),
            FuturesEodDataInsertedCompleteEvent e => StreamEventAsync(e.CommandId, e),
            FuturesEodDataInsertedFailEvent e => SendEventAsync(e.CommandId, e),

            // futures closing price data events...
            FuturesClosingPriceInsertedEvent e => StreamEventAsync(e.FuturesClosingPriceId, e),
            FuturesClosingPriceInsertedCompleteEvent e => StreamEventAsync(e.CommandId, e),
            FuturesClosingPriceInsertedFailEvent e => SendEventAsync(e.CommandId, e),

            // vix futures eod events...
            VixFuturesEodDataInsertedEvent e => StreamEventAsync(e.VixFuturesTickData.ContractId, e),
            VixFuturesEodDataInsertedCompleteEvent e => StreamEventAsync(e.CommandId, e),
            VixFuturesEodDataInsertedFailEvent e => SendEventAsync(e.CommandId, e),

            // trade live feed events...
            TradeLiveFeedAddedEvent e => SendEventAsync(e.CommandId, e),
            TradeLiveFeedAddedCompleteEvent e => SendEventAsync(e.CommandId, e),
            TradeLiveFeedAddedFailEvent e => SendEventAsync(e.CommandId, e),
            TradeLiveFeedRemovedEvent e => SendEventAsync(e.CommandId, e),
            TradeLiveFeedRemovedCompleteEvent e => SendEventAsync(e.CommandId, e),
            TradeLiveFeedRemovedFailEvent e => SendEventAsync(e.CommandId, e),

            // ui update events...
            FuturesEodDataUpdatedEvent e => StreamEventAsync(e.FuturesEodData.ContractId, e),
            OptionTradeTickPriceDataUpdatedEvent e => StreamEventAsync(e.OptionTickData.ContractId, e),

            // tick price data events...
            FuturesTickBidAskEvent e => StreamEventAsync(e.RequestId, e),
            FuturesOptionTickBidAskEvent e => StreamEventAsync(e.RequestId, e),

            // market data feed broker api events...
            MarketDataFeedStartedEvent e => SendEventAsync(e.CommandId, e),
            MarketDataFeedStartedCompleteEvent e => SendEventAsync(e.CommandId, e),
            MarketDataFeedStartedFailEvent e => SendEventAsync(e.CommandId, e),
            MarketDataFeedStoppedEvent e => SendEventAsync(e.CommandId, e),
            MarketDataFeedStoppedCompleteEvent e => SendEventAsync(e.CommandId, e),
            MarketDataFeedStoppedFailEvent e => SendEventAsync(e.CommandId, e),
            MarketDataFeedResetEvent e => SendEventAsync(e.CommandId, e),
            MarketDataFeedResetCompleteEvent e => SendEventAsync(e.CommandId, e),
            MarketDataFeedResetFailEvent e => SendEventAsync(e.CommandId, e),
            MarketDataFeedResetStreamingEvent e => SendEventAsync(e.CommandId, e),

            // denormalizer events...
            // DenormalizerCompletedEvent e => SendEventAsync(e.CommandId, e),
            // DenormalizerExceptionEvent e => SendEventAsync(e.CommandId, e),
             StatusConsoleLoggedEvent e => SendEventAsync(e.CommandId, e),
             CommandExceptionEvent e => SendEventAsync(e.CommandId, e),
             _ => Task.CompletedTask
        });
    }
}
