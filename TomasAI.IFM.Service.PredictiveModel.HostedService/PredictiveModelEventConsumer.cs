using Microsoft.Extensions.Logging;
using TomasAI.IFM.Framework.Messaging.Kafka;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.PredictiveModel.FuturesItiTrend.Events;

namespace TomasAI.IFM.Service.PredictiveModel.HostedService
{
    public class PredictiveModelEventConsumer : KafkaEventConsumer, IPredictiveModelEventConsumer
    {
        readonly IPredictiveModelService _predictiveModelService;
        readonly Guid _siteId;

        /// <summary>
        /// predictive model event consumer 
        /// </summary>
        /// <param name="predictiveModelService"></param>
        /// <param name="options"></param>
        /// <param name="logger"></param>
        public PredictiveModelEventConsumer(
            IPredictiveModelService predictiveModelService, 
            IEventConsumerOptions options, 
            ILogger logger) :base(options, logger)
        {
            _predictiveModelService = IsArgumentNull.Set(predictiveModelService);
            _siteId = Guid.NewGuid();
        }

        /// <summary>
        /// execute event service action for predicitive model events from event broker
        /// </summary>
        protected override void ConnectEvents()
        {
            var @events = new List<IEvent>
            {
                new FuturesItiTrendModelBuildStartedEvent{ },
                new FuturesItiTrendDeltaModelDataLoadedCompleteEvent{ },
                new FuturesItiTrendDeltaModelTrainedCompleteEvent{ },
                new FuturesItiTrendClassModelDataLoadedCompleteEvent{ },
                new FuturesItiTrendClassModelTrainedCompleteEvent{ },
            };
            @events.ForEach(e => e.SetEventSource($"{EventTopic.PredictiveModelEvents}"));
            Subscribe($"{_siteId}", @events, async e => await _predictiveModelService.ExecuteAsync(e));
        }

    }
}
