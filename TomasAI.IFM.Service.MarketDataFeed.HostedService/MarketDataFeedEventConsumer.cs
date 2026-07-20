using TomasAI.IFM.Shared.MarketDataFeed.Events;
using TomasAI.IFM.Shared.MarketDataFeed.Events.Api;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventQueue;
using TomasAI.IFM.Framework.Messaging.Kafka;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Service.MarketDataFeed.HostedService;

/// <summary>
/// market data feed event consumer
/// </summary>
public class MarketDataFeedEventConsumer : KafkaEventConsumer, IMarketDataFeedEventConsumer
{
    readonly IMarketDataFeedService _marketDataFeedService;
    readonly Guid _siteId;
    readonly ConcurrentEventQueue<FuturesTickPriceDataEvent> _futuresTickPriceDataQueue;
    readonly ConcurrentEventQueue<FuturesOptionTickPriceDataEvent> _futuresOptionTickPriceDataQueue;
    readonly ConcurrentEventQueue<IEvent> _eventQueue;

    /// <summary>
    /// market data feed event consumer constructor
    /// </summary>
    /// <param name="marketDataFeedService"></param>
    /// <param name="options"></param>
    /// <param name="logger"></param>
    public MarketDataFeedEventConsumer(
        IMarketDataFeedService marketDataFeedService, 
        IEventConsumerOptions options, 
        ILogger logger) :base(options, logger)
    {
        _marketDataFeedService = IsArgumentNull.Set(marketDataFeedService);
        _siteId = Guid.NewGuid();
        _futuresTickPriceDataQueue = new ConcurrentEventQueue<FuturesTickPriceDataEvent>( e =>  _marketDataFeedService.ExecuteAsync(e));
        _futuresTickPriceDataQueue.Start();
        _futuresOptionTickPriceDataQueue = new ConcurrentEventQueue<FuturesOptionTickPriceDataEvent>(_marketDataFeedService.ExecuteAsync);
        _futuresOptionTickPriceDataQueue.Start();
        _eventQueue = new ConcurrentEventQueue<IEvent>( _marketDataFeedService.ExecuteAsync);
        _eventQueue.Start();
    }

    /// <summary>
    /// execute event action for market data feed events from event broker
    /// </summary>
    protected override void ConnectEvents()
    {
        var @events = new List<IEvent>
        {
            new MarketDataFeedStartedEvent{ },
            new MarketDataFeedStartedCompleteEvent{ },
            new MarketDataFeedStartedCompleteApiEvent{ },
            new MarketDataFeedStoppedEvent{ },
            new MarketDataFeedStoppedCompleteEvent{ },
            new MarketDataFeedResetEvent{ },
            new MarketDataFeedResetCompleteEvent{ },
            new FuturesTickDataStreamingStartedEvent{ },
            new FuturesTickDataStreamingStoppedEvent{ },
            new FuturesOptionTickDataStreamingStartedEvent{ },
            new FuturesOptionTickDataStreamingStoppedEvent{ },
            new FuturesEodDataInsertedEvent{ },
            new FuturesTickDataInsertedEvent{ },
            new FuturesOptionTickDataInsertedEvent{ },
            new FuturesTickPriceDataEvent{ },
            new FuturesOptionTickPriceDataEvent{ },
            new VixFuturesEodDataInsertedCompleteEvent{ },
            new FuturesBarDataStreamingStartedEvent{ },
            new FuturesBarDataStreamingStoppedEvent{ },
            new FuturesOptionQuoteDataStreamingStartedCompleteEvent{ },
            new FuturesOptionQuoteDataStreamingStoppedCompleteEvent{ },
            new FuturesOptionQuoteDataInsertedCompleteEvent{ },
            new FuturesOptionQuoteStreamingDataEvent{ },
            new FuturesOptionQuoteDataUpdatedEvent{ },
        };
        @events.ForEach(e => e.SetEventSource($"{EventTopic.MarketDataFeedEvents}"));
        Subscribe($"{_siteId}", @events,  e => OnMarketeDataFeedServiceEvent(e));
    }

    private void OnMarketeDataFeedServiceEvent(IEvent @event)
    {
        switch(@event)
        {
            case FuturesTickPriceDataEvent o:
                _futuresTickPriceDataQueue.EnqueueForSignal(o);
                _futuresTickPriceDataQueue.Signal();
                break;
            case FuturesOptionTickPriceDataEvent o:
                _futuresOptionTickPriceDataQueue.EnqueueForSignal(o);
                _futuresOptionTickPriceDataQueue.Signal();
                break;
            default:
                _eventQueue.EnqueueForSignal(@event);
                _eventQueue.Signal();
                break;
        }
    }
}
