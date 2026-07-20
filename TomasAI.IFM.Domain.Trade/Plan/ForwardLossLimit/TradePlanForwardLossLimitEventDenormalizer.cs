using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.ScyllaDb.TradeDb;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.Trade.ServiceApi;
using TomasAI.IFM.Shared.Trade.Events;

namespace TomasAI.IFM.Domain.Trade.Plan.ForwardLossLimit;

/// <summary>
/// Processes and denormalizes events related to trade plan forward loss limits, updating the read model accordingly.
/// </summary>
/// <remarks>This class listens for specific events related to trade plan forward loss limits, such as warnings,
/// limit reached,  or limit cleared, and updates the corresponding read model in the database. It uses the provided
/// database context  and event producer to perform these operations.</remarks>
/// <param name="db"></param>
/// <param name="eventProducer"></param>
/// <param name="logger"></param>
public class TradePlanForwardLossLimitEventDenormalizer(
    ITradeDbContext db,
    ITradeEventProducer eventProducer,
    ILogger logger)
    : BaseEventQueueDenormalizer(eventProducer, logger), IEventDenormalizer<TradePlanForwardLossLimitBoundedContextState>
{
    protected override async Task<bool> DenormalizeAsync(IEvent e)
        => e switch
            {
                TradePlanForwardLossLimitWarningUpdatedEvent o => await UpdateReadModelAsync(o, () => db.InsertTradePlanForwardLossLimitAsync(o.TradePlanForwardLossLimit)),
                TradePlanForwardLossLimitReachedUpdatedEvent o => await UpdateReadModelAsync(o, () => db.InsertTradePlanForwardLossLimitAsync(o.TradePlanForwardLossLimit)),
                TradePlanForwardLossLimitClearedEvent o => await UpdateReadModelAsync(o, () => db.DeleteTradePlanForwardLossLimitAsync(o.ForwardLossLimitId)),
                _ => false
            };

}
