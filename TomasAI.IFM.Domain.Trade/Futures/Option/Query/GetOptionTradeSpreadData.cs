using TomasAI.IFM.Domain.Trade.Futures.Option.Query.Actor;
using TomasAI.IFM.Domain.Trade.Shared.Queries;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Query;

public static class GetOptionTradeSpreadData
{

    extension(GetOptionTradeSpreadDataQuery query)
    {
        /// <summary>Reads the latest legacy spread projection for an option trade.</summary>
        public async ValueTask<ServiceResult<OptionTradeSpreadsDataModel?>> ExecuteAsync(IFuturesOptionTradeQueryContext context, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(query);
            var result = await context.DbFactory.TradeDb.GetOptionTradeSpreadDataAsync(query.OrderId, query.TradeId, query.ValueDate, query.TradeType, cancellationToken).ConfigureAwait(false);
            return new ServiceResult<OptionTradeSpreadsDataModel?>(result);
        }
    }
}
