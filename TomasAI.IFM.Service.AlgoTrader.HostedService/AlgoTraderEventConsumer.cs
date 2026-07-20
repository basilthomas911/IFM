using TomasAI.IFM.Framework.Messaging.Kafka;
using TomasAI.IFM.Shared.Trade.Events;
using TomasAI.IFM.Shared.AlgoTrader;
using TomasAI.IFM.Shared.EventSourcing;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Service.AlgoTrader.HostedService;

public class AlgoTraderEventConsumer(IAlgoTraderService algoTraderService, IEventConsumerOptions options, ILogger logger) : KafkaEventConsumer(options, logger), IAlgoTraderEventConsumer
{
    readonly IAlgoTraderService _algoTraderService = algoTraderService;
    readonly Guid _siteId = Guid.NewGuid();

    protected override void ConnectEvents() 
        => Subscribe($"{_siteId}", 
            [new OptionTradeSpreadDistributionStatisticsUpdatedEvent { }.SetEventSource($"{EventTopic.TradeEvents}") ], 
            async e => await _algoTraderService.UpdateTradePlanAsync(e as OptionTradeSpreadDistributionStatisticsUpdatedEvent));
}
