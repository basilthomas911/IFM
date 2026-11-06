using TomasAI.IFM.Domain.Trade.Futures.Option.Query.Actor;
using TomasAI.IFM.Domain.Trade.Shared.Queries;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Query;

public static class GetOptionTradeSpreadBarData
{

    extension(GetOptionTradeSpreadBarDataQuery query)
    {
        /// <summary>Reads legacy option spread bars for the requested date range.</summary>
        public async ValueTask<ServiceResult<OptionTradeSpreadBarsDataModel[]>> ExecuteAsync(IFuturesOptionTradeQueryContext context, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(query);
            OptionTradeSpreadBarsDataModel[] result = [.. await context.DbFactory.TradeDb.GetOptionTradeSpreadBarDataAsync(query.OrderId, query.TradeId, query.ValueDate, query.TradeType, query.StartDate, query.EndDate, cancellationToken).ConfigureAwait(false)];
            return new ServiceResult<OptionTradeSpreadBarsDataModel[]>(result);
        }
    }
}
