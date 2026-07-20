using Microsoft.Extensions.Logging;
using TomasAI.IFM.Framework.Messaging.Kafka;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.StatusConsole.Events;
using TomasAI.IFM.Shared.Trade.Events;
using TomasAI.IFM.Shared.Trade.ServiceApi;

namespace TomasAI.IFM.Shared.EventProducers;

/// <summary>
/// Produces trade placement events and publishes them to the event broker using Kafka.
/// </summary>
/// <remarks>This class is typically used to emit events related to trade placement operations, such as starting,
/// stopping, setting, clearing, waiting, and handling rangebound scenarios. It supports integration with event-driven
/// architectures by publishing domain events to a Kafka topic dedicated to trade placement. Thread safety and
/// reliability depend on the underlying Kafka producer implementation. For testing purposes, a parameterless
/// constructor is provided.</remarks>
public class TradePlacementEventProducer : KafkaEventProducer,  ITradePlacementEventProducer
{

    /// <summary>
    /// trade placement event producer
    /// </summary>
    /// <param name="options"></param>
    /// <param name="logger"></param>
    public TradePlacementEventProducer(IEventProducerOptions options, ILogger<TradePlacementEventProducer> logger) 
        : base(options, logger)
    {
    }

    /// for BBD Testing only...
    public TradePlacementEventProducer()
    {
    }

    /// <summary>
    /// post event to trade placement event to event broker
    /// </summary>
    /// <param name="event"></param>
    /// <returns></returns>
    public override async Task PostEventAsync(IEvent @event)
    {
        @event.SetEventSource($"{EventTopic.TradePlacementEvents}");
        await (@event switch
        {
            TradePlacementStartedEvent e => SendEventAsync(e.TradePlacementId, e),
            TradePlacementStoppedEvent e => SendEventAsync(e.TradePlacementId, e),
            TradePlacementSetEvent e => SendEventAsync(e.TradePlacementId, e),
            TradePlacementSetCompleteEvent e => SendEventAsync(e.CommandId, e),
            TradePlacementSetFailEvent e => SendEventAsync(e.CommandId, e),
            TradePlacementClearedEvent e => SendEventAsync(e.TradePlacementId, e),
            TradePlacementClearedCompleteEvent e => SendEventAsync(e.CommandId, e),
            TradePlacementClearedFailEvent e => SendEventAsync(e.CommandId, e),
            TradePlacementWaitEvent e => SendEventAsync(e.TradePlacementId, e),
            TradePlacementWaitCompleteEvent e => SendEventAsync(e.CommandId, e),
            TradePlacementWaitFailEvent e => SendEventAsync(e.CommandId, e),
            TradePlacementRangeboundEvent e => SendEventAsync(e.TradePlacementId, e),
            TradePlacementRangeboundCompleteEvent e => SendEventAsync(e.CommandId, e),
            TradePlacementRangeboundFailEvent e => SendEventAsync(e.CommandId, e),
            StatusConsoleLoggedEvent e => SendEventAsync(e.CommandId, e),
            CommandExceptionEvent e => SendEventAsync(e.CommandId, e),
            _ => Task.CompletedTask
        });
    }

}
