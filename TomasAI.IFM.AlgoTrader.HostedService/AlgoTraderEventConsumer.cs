using System;
using System.Collections.Generic;
using System.Text;
using TomasAI.IFM.Framework.Messaging.Kafka;
using TomasAI.IFM.Shared.Trade.Events;
using TomasAI.IFM.Shared.AlgoTrader;
using TomasAI.IFM.Shared.EventSourcing;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Service.AlgoTrader.HostedService
{
    public class AlgoTraderEventConsumer : KafkaEventConsumer, IAlgoTraderEventConsumer
    {
        private readonly IAlgoTraderService _algoTraderService;
        private readonly Guid _siteId;

        public AlgoTraderEventConsumer(IAlgoTraderService algoTraderService, IEventConsumerOptions options, ILogger logger) :base(options, logger)
        {
            _algoTraderService = algoTraderService;
            _siteId = Guid.NewGuid();
        }

        protected override void ConnectEvents() 
            => Subscribe($"{_siteId}", 
                new IEvent[] { new OptionTradeDistributionStatisticsUpdatedEvent { }.SetEventSource($"{EventTopic.TradeEvents}") }, 
                async e => await _algoTraderService.UpdateTradePlanAsync(e as OptionTradeDistributionStatisticsUpdatedEvent));
    }
}
