using System;
using System.Collections.Generic;
using System.Text;
using TomasAI.IFM.Framework.Messaging.Kafka;
using TomasAI.IFM.Shared.Fund.Events;
using TomasAI.IFM.Shared.Fund;
using TomasAI.IFM.Shared.EventSourcing;
using Microsoft.Extensions.Logging;

namespace TomasAI.IFM.Service.Fund.HostedService
{
    public class FundEventConsumer : KafkaEventConsumer, IFundEventConsumer
    {
        private readonly IFundEventService _fundEventService;
        private readonly Guid _siteId;

        public FundEventConsumer(IFundEventService fundEventService, IEventConsumerOptions options, ILogger logger) :base(options, logger)
        {
            _fundEventService = fundEventService;
            _siteId = Guid.NewGuid();
        }

        protected override void ConnectEvents() 
            => Subscribe($"{_siteId}", 
                new IEvent[] { new FundMaxProfitGeneratedEvent { } }, 
                async e => await _fundEventService.ExecuteAsync(e as FundMaxProfitGeneratedEvent));
    }
}
