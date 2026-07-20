using System;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventChannel ;
using TomasAI.IFM.Framework.Messaging.Kafka;
using TomasAI.IFM.Framework.Serialization;

namespace TomasAI.IFM.UI.EventConsumer;

/// <summary>
/// Represents a consumer for market data UI events, utilizing Kafka for asynchronous event consumption.
/// </summary>
/// <remarks>This class is designed to handle market data events by subscribing to a Kafka topic and processing
/// events asynchronously. It requires a collection of events to consume and a delegate to handle each event.</remarks>
/// <param name="options"></param>
/// <param name="jsonSerializer"></param>
/// <param name="logger"></param>
public class MarketDataUIEventConsumer(IEventConsumerOptions options, IJsonSerializer jsonSerializer, ILogger<EventChannel> logger)
    : KafkaAsyncEventConsumer(options, jsonSerializer, logger), IMarketDataUIEventConsumer
{
    readonly Guid _siteId = Guid.NewGuid();
    ICollection<IEvent> _consumeEvents = [];
    Func<IEvent, ValueTask> _eventAction = _ => ValueTask.CompletedTask;
    /*
            new FuturesOptionContractAddedCompleteEvent { }.SetEventSource($"{EventTopic.MarketDataEvents}"),
            new FuturesOptionContractAddedFailEvent{ }.SetEventSource($"{EventTopic.MarketDataEvents}"),
            new FuturesOptionContractChangedCompleteEvent { }.SetEventSource($"{EventTopic.MarketDataEvents}"),
            new FuturesOptionContractChangedFailEvent{ }.SetEventSource($"{EventTopic.MarketDataEvents}"),
            new FuturesOptionContractRemovedCompleteEvent { }.SetEventSource($"{EventTopic.MarketDataEvents}"),
            new FuturesOptionContractRemovedFailEvent{ }.SetEventSource($"{EventTopic.MarketDataEvents}")
 
    */
    public async ValueTask StartAsync(ICollection<IEvent> consumeEvents, Func<IEvent, ValueTask> eventAction)
    {
        _consumeEvents = consumeEvents;
        _eventAction = eventAction;
        await base.StartAsync();
    }

    protected override void ConnectEvents() 
        => Subscribe($"{_siteId}", _consumeEvents, _eventAction);

}
