using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.ScyllaDb.TradeDb;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.Trade.ServiceApi;
using TomasAI.IFM.Shared.TradeAlgorithm.Events;

namespace TomasAI.IFM.Domain.Trade.Option.Algorithm;

/// <summary>
/// option trade event denormalizer constructor
/// </summary>
/// <param name="dbFactory"></param>
/// <param name="eventProducer"></param>
/// <param name="logger"></param>
internal class OptionTradeAlgorithmEventDenormalizer(
    IDbContextFactory dbFactory,
    ITradeEventProducer eventProducer,
    ILogger logger) : BaseEventQueueDenormalizer(eventProducer, logger), IEventDenormalizer<OptionTradeAlgorithmBoundedContextState>
{
    readonly ITradeDbWriteContext? _db = IsArgumentNull.Set(dbFactory.TradeDb as ITradeDbWriteContext);

    protected override async Task<bool> DenormalizeAsync(IEvent e)
         =>  e switch
        {
            LongIronCondorAlgorithmExecutedEvent o => await UpdateReadModelAsync(o, () => _db!.InsertTradePlanAsync(o.TradePlan)),
            ShortIronCondorAlgorithmExecutedEvent o => await UpdateReadModelAsync(o, () => _db!.InsertTradePlanAsync(o.TradePlan)),
            _ => false
        };
    
}
