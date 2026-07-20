using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Trade.ServiceApi;
using TomasAI.IFM.Shared.Trade.Events;
using TomasAI.IFM.Application.Storage.ScyllaDb.TradeDb;

namespace TomasAI.IFM.Domain.Trade.Plan;

/// <summary>
/// trade plan event denormalizer constructor
/// </summary>
/// <param name="db"></param>
/// <param name="eventProducer"></param>
/// <param name="logger"></param>
public class TradePlanEventDenormalizer(
    ITradeDbContext db,
    ITradeEventProducer eventProducer,
    ILogger logger) 
    : BaseEventQueueDenormalizer(eventProducer, logger), IEventDenormalizer<TradePlanBoundedContextState>
{
    /// <summary>
    /// denormalize event
    /// </summary>
    /// <param name="e"></param>
    /// <returns></returns>
    protected override async Task<bool> DenormalizeAsync(IEvent e)
        =>e switch
            {
                TradePlanUpdatedEvent o => await UpdateReadModelAsync(o, () => db.InsertTradePlanAsync(o.TradePlan)),
               _ => false,
            };
}
