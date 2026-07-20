using TomasAI.IFM.Shared.MarketDataFeed.Events;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Framework.Messaging.Kafka;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.MarketDataAnalytics.Events;
using TomasAI.IFM.Shared.PredictiveModel.FuturesItiTrend.Events;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Service.MarketDataAnalytics.HostedService;

/// <summary>
/// market data analytics event consumer constructor
/// </summary>
/// <param name="marketDataAnalyticsService"></param>
/// <param name="options"></param>
/// <param name="logger"></param>
public class MarketDataAnalyticsEventConsumer(
    IMarketDataAnalyticsService marketDataAnalyticsService,
    IEventConsumerOptions options,
    ILogger logger)
    : KafkaEventConsumer(options, logger), IMarketDataAnalyticsEventConsumer
{
    readonly Guid _siteId = Guid.NewGuid();

    /// <summary>
    /// execute event action for market data analytics events from event broker
    /// </summary>
    protected override void ConnectEvents()
    {
        var @events = new List<IEvent>
        {
            new FuturesEodDataInsertedCompleteEvent{ },
            new FuturesRsiSignalStartedEvent{ },
            new FuturesRsiSignalStoppedEvent{ },
            new FuturesRsiSignalsGeneratedEvent{ },
            new FuturesTradeSignalLLMStartedEvent{ },
            new FuturesTradeSignalLLMStoppedEvent{ },
            new FuturesItiSignalHoldTradeChangedEvent{ },
            new FuturesItiTrendModelLoadedCompleteEvent{ },
            new FuturesItiSignalGeneratedCompleteEvent{ },
        };
        @events.ForEach(e => e.SetEventSource($"{EventTopic.MarketDataAnalyticEvents}"));
        Subscribe($"{_siteId}", @events,  async e => await marketDataAnalyticsService.ExecuteAsync(e));
    }

}
