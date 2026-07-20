using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Trade.ServiceApi;
using TomasAI.IFM.Shared.TradeOrder.Events;

namespace TomasAI.IFM.Domain.Trade.Order;

/// <summary>
/// trade order event denormalizer constructor
/// </summary>
/// <param name="eventProducer"></param>
/// <param name="logger"></param>
public class TradeOrderEventDenormalizer(
    ITradeOrderEventProducer eventProducer,
    ILogger logger) 
    : BaseEventQueueDenormalizer(eventProducer, logger), IEventDenormalizer<TradeOrderBoundedContextState>
{
    protected override async Task<bool> DenormalizeAsync(IEvent e)
        => e switch {
            TradeOrderPlacedEvent o => await PostEventAsync(o),
            TradeOrderOpenedEvent o => await PostEventAsync(o),
            TradeOrderFilledEvent o => await PostEventAsync(o),
            TradeOrderUpdatedEvent o => await PostEventAsync(o),
            TradeOrderCancelledEvent o => await PostEventAsync(o),
            TradeOrderClosedEvent o => await PostEventAsync(o),
            UpdateOrderExecutedEvent o => await PostEventAsync(o),
            CancelOrderExecutedEvent o => await PostEventAsync(o),
            _ => false
        };

}
