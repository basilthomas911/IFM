using System;
using System.Collections.Generic;
using TomasAI.IFM.Shared.TradeOrder.Events;
using TomasAI.IFM.Framework.Messaging.Kafka;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using Microsoft.Extensions.Logging;

namespace TomasAI.IFM.Service.OrderExecution.HostedService;

/// <summary>
/// order execution event consumer
/// </summary>
/// <remarks>
/// order execution event consumer constructor
/// </remarks>
/// <param name="orderExecutionService"></param>
/// <param name="options"></param>
/// <param name="logger"></param>
public class OrderExecutionEventConsumer(
    IOrderExecutionService orderExecutionService,
    IEventConsumerOptions options,
    ILogger logger) : KafkaEventConsumer(options, logger), IOrderExecutionEventConsumer
{
    readonly IOrderExecutionService _orderExecutionService = orderExecutionService;
    readonly Guid _siteId = Guid.NewGuid();

    /// <summary>
    /// consume trade order events from event broker
    /// </summary>
    protected override void ConnectEvents()
    {
        var consumeEvents = new List<IEvent> {
            new TradeOrderPlacedCompleteEvent{ },
            new TradeOrderOpenedCompleteEvent{ },
            new TradeOrderFilledCompleteEvent{ },
            new TradeOrderClosedCompleteEvent{ },
            new TradeOrderUpdatedCompleteEvent{ },
            new TradeOrderCancelledCompleteEvent{ },
            new BrokerOrderOpenedEvent{ },
            new BrokerOrderFilledEvent{ },
            new BrokerOrderClosedEvent{ },
        };
        consumeEvents.ForEach(e => e.SetEventSource($"{EventTopic.TradeOrderEvents}"));
        Subscribe($"{_siteId}", consumeEvents,  async e => await _orderExecutionService.ExecuteAsync(e));
    }
}
