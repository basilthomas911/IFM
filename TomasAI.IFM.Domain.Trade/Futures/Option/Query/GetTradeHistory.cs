using TomasAI.IFM.Domain.Trade.Futures.Option.Query.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Option.Query.Model;
using TomasAI.IFM.Domain.Trade.Shared.Queries;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Query;

public static class GetTradeHistory
{

    extension(GetTradeHistoryQuery query)
    {
        /// <summary>Reads the ordered position history for a legacy option-trade order.</summary>
        public async ValueTask<ServiceResult<TradeHistoryReadModel[]>> ExecuteAsync(IFuturesOptionTradeQueryContext context, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(query);
            TradeHistoryReadModel[] result = [.. await context.DbFactory.GetTradeHistoryAsync(query.OrderId, cancellationToken).ConfigureAwait(false)];
            return new ServiceResult<TradeHistoryReadModel[]>(result);
        }
    }
}
