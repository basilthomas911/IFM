using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Framework.Messaging.Kafka;
using TomasAI.IFM.Shared.OptionPricer.Events;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Application.OptionPricer.HostedService
{
    public class OptionPricerEventConsumer : KafkaEventConsumer, IOptionPricerEventConsumer
    {
        readonly IOptionPricerJobService _optionPricerJobService;
        readonly Guid _siteId;
        readonly ILogger _logger;

        public OptionPricerEventConsumer(
            IOptionPricerJobService optionPricerJobService, 
            IEventConsumerOptions options, 
            ILogger logger) :base(options, logger)
        {
            _optionPricerJobService = optionPricerJobService;
            _logger = logger;
            _siteId = Guid.NewGuid();
        }

        protected override void ConnectEvents() 
            => Subscribe($"{_siteId}", 
                new IEvent[] { new SpreadDistributionJobSubmittedEvent { }.SetEventSource($"{EventTopic.OptionPricerEvents}") }, 
                async e => await RunOptionPricerJobAsync((SpreadDistributionJobSubmittedEvent)e));

        async Task RunOptionPricerJobAsync(SpreadDistributionJobSubmittedEvent e)
        {
            try
            {
                if (e is not null)
                    await _optionPricerJobService.RunOptionPricerJobAsync(e);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"OptionPricerEventConsumer.RunOptionPricerJobAsync:  {ex.GetErrorMessage()}");
            }
        }
    }
}
