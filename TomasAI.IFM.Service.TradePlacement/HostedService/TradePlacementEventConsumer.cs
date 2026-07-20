using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.Trade.Events;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Framework.Messaging.Kafka;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Service.TradePlacement.HostedService;

/// <summary>
/// Consumes trade placement events from a Kafka topic and processes them using the provided trade placement service.
/// </summary>
/// <remarks>This class subscribes to a set of trade placement-related events, such as start, stop, set, wait, and
/// clear events, and processes them asynchronously using the <see cref="ITradePlacementEventService"/>. Each event is
/// associated with a specific event source derived from the <see cref="EventTopic.TradePlacementEvents"/>
/// topic.</remarks>
/// <param name="tradePlacementService">The service responsible for executing trade placement operations based on the consumed events.</param>
/// <param name="options">Configuration options for the event consumer, such as Kafka connection settings and subscription details.</param>
/// <param name="logger">The logger used to log diagnostic and operational information for the event consumer.</param>
public class TradePlacementEventConsumer(ITradePlacementEventService tradePlacementService, IEventConsumerOptions options, ILogger<TradePlacementEventConsumer> logger) 
    : KafkaEventConsumer(options, logger), ITradePlacementEventConsumer
{
    readonly Guid _siteId = Guid.NewGuid();

    protected override void ConnectEvents()
        => Subscribe($"{_siteId}",  [
                new TradePlacementStartedEvent { }.SetEventSource($"{EventTopic.TradePlacementEvents}"),
                new TradePlacementStoppedEvent { }.SetEventSource($"{EventTopic.TradePlacementEvents}") ,
                new TradePlacementSetEvent { }.SetEventSource($"{EventTopic.TradePlacementEvents}") ,
                new TradePlacementWaitEvent { }.SetEventSource($"{EventTopic.TradePlacementEvents}") ,
                new TradePlacementClearedEvent { }.SetEventSource($"{EventTopic.TradePlacementEvents}") ,
            ], 
            async e => await tradePlacementService.ExecuteAsync(e, tradePlacementService));
}

public interface ITradePlacementEventConsumer : IEventConsumer
{
}
