using Microsoft.Extensions.Logging;
using TomasAI.IFM.Framework.Messaging.Kafka;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.PredictiveModel.FuturesItiTrend.Events;

namespace TomasAI.IFM.Application.PredictiveModel.Server.HostedService;

/// <summary>
/// Consumes predictive model server events and processes them using the provided predictive model service.
/// </summary>
/// <remarks>This class subscribes to predictive model-related events and delegates their processing to the <see
/// cref="IPredictiveModelService"/> implementation. It is designed to handle events such as model training updates and
/// execute the appropriate logic asynchronously.</remarks>
/// <param name="predictiveModelService"></param>
/// <param name="options"></param>
/// <param name="logger"></param>
public class PredictiveModelServerEventConsumer(
    IPredictiveModelService predictiveModelService,
    IEventConsumerOptions options,
    ILogger logger)
        : KafkaEventConsumer(options, logger), IPredictiveModelServerEventConsumer
{
    readonly IPredictiveModelService _predictiveModelService = IsArgumentNull.Set(predictiveModelService);
    readonly Guid _siteId = Guid.NewGuid();

    /// <summary>
    /// Subscribes to predictive model training events and connects them to the execution service.
    /// </summary>
    /// <remarks>This method establishes event subscriptions for specific predictive model training events 
    /// and routes them to the predictive model service for processing. The events are sourced  from the predictive
    /// model event topic.</remarks>
    protected override void ConnectEvents() 
        => Subscribe($"{_siteId}",
            [new FuturesItiTrendDeltaModelTrainedEvent { }.SetEventSource($"{EventTopic.PredictiveModelEvents}") ,
              new FuturesItiTrendClassModelTrainedEvent { }.SetEventSource($"{EventTopic.PredictiveModelEvents}") ],
            async e => await _predictiveModelService.ExecuteAsync(e));

}
