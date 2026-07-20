using TomasAI.IFM.Shared.Trade.Events;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Framework.Messaging.Kafka;
using Microsoft.Extensions.Logging;

namespace TomasAI.IFM.Service.TradePosition.HostedService;

/// <summary>
/// Consumes trade position-related events from the event broker and processes them using the provided trade position
/// service.
/// </summary>
/// <remarks>This class subscribes to trade position events and delegates event handling to an implementation of
/// <see cref="ITradePositionService"/>. It is typically used to integrate trade position event processing into systems
/// that utilize Kafka-based event brokers. Inherits from <see cref="KafkaEventConsumer"/> and implements <see
/// cref="ITradePositionEventConsumer"/>.</remarks>
public class TradePositionEventConsumer : KafkaEventConsumer, ITradePositionEventConsumer
{
    private readonly ITradePositionService _tradePositionService;
    private readonly Guid _siteId;

    /// <summary>
    /// trade position event consumer constructor
    /// </summary>
    /// <param name="tradePositionService"></param>
    /// <param name="options"></param>
    /// <param name="logger"></param>
    public TradePositionEventConsumer(ITradePositionService tradePositionService, IEventConsumerOptions options, ILogger<TradePositionEventConsumer> logger):base(options, logger)
    {
        _tradePositionService = tradePositionService;
        _siteId = Guid.NewGuid();
    }

    /// <summary>
    /// consume only trade position events from event broker
    /// </summary>
    protected override void ConnectEvents()
    {
        var @events = new List<IEvent>
        {
            new TradePositionChangedEvent{ },
            new OptionTradeLegDataChangedEvent{ },
            new OptionTradeEndOfDayProcessedEvent{ },
            new OptionTradeSpreadDistributionStatisticsChangedEvent{ },
            new OptionTradeOrderPlacedEvent{ },
            new OptionTradePositionOpenedEvent{ },
            new OptionTradePositionClosedEvent{ },
        };
        @events.ForEach(e => e.SetEventSource($"{EventTopic.TradeEvents}"));
        Subscribe($"{_siteId}", @events, async e => await _tradePositionService.ExecuteAsync((dynamic )e));
    }
}
