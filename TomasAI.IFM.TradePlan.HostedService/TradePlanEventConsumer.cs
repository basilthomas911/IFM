using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.Trade.Events;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Framework.Messaging.Kafka;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.TradePlan.HostedService
{
    public class TradePlanEventConsumer : KafkaEventConsumer, ITradePlanEventConsumer
    {
        private readonly ITradePlanService _tradePlanService;
        private readonly Guid _siteId;

        public TradePlanEventConsumer(ITradePlanService tradePlanService, IEventConsumerOptions options, ILogger<TradePlanEventConsumer> logger):base(options, logger)
        {
            _tradePlanService = tradePlanService;
            _siteId = Guid.NewGuid();
        }

        protected override void ConnectEvents() 
            => Subscribe($"{_siteId}", 
                new IEvent[] { new TradePlanUpdatedEvent { }.SetEventSource($"{EventTopic.TradeEvents}"),
                               new TradePlanForwardLossLimitWarningUpdatedEvent { }.SetEventSource($"{EventTopic.TradeEvents}"),
                               new TradePlanForwardLossLimitReachedUpdatedEvent { }.SetEventSource($"{EventTopic.TradeEvents}"),
                               new TradePlanForwardLossLimitClearedEvent { }.SetEventSource($"{EventTopic.TradeEvents}") } , 
                async e => await _tradePlanService.ExecuteAsync(e));
    }
}
