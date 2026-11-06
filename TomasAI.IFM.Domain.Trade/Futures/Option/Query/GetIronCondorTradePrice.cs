using TomasAI.IFM.Domain.Trade.Futures.Option.Query.Actor;
using TomasAI.IFM.Domain.Trade.Shared.Queries;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Query;

public static class GetIronCondorTradePrice
{

    extension(GetIronCondorTradePriceQuery query)
    {
        /// <summary>Reads the legacy Iron Condor price projection.</summary>
        public async ValueTask<ServiceResult<TradePriceReadModel?>> ExecuteAsync(IFuturesOptionTradeQueryContext context, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(query);
            var result = await context.DbFactory.TradeDb.GetIronCondorTradePriceAsync(query.TradeId, query.ValueDate, cancellationToken).ConfigureAwait(false);
            return new ServiceResult<TradePriceReadModel?>(result);
        }
    }
}
