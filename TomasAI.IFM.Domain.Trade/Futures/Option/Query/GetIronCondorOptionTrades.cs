using TomasAI.IFM.Domain.Trade.Futures.Option.Query.Actor;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Query;

public static class GetIronCondorOptionTrades
{

    /// <param name="query">
    /// The query containing ownership, date-range, and paging criteria.
    /// </param>
    extension(GetIronCondorOptionTradesQuery query)
    {
        /// <summary>
        /// Reads the requested page of Iron Condor trades for one portfolio and fund.
        /// </summary>
        ///
        /// <param name = "context">The typed query context that provides projection storage.</param>
        /// <param name = "cancellationToken">The token that cancels the storage operation.</param>
        /// <returns>The typed projected result.</returns>
        public async ValueTask<ServiceResult<EstablishedTradeDefinition[]>> ExecuteAsync(IFuturesOptionTradeQueryContext context, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(query);
            var page = await context.DbFactory.TradeDb.GetEstablishedTradesAsync(query.PortfolioId, query.FundId, TradeStrategyKind.IronCondor, query.FromUtc, query.ToUtc, query.PageSize, query.PagingState, cancellationToken).ConfigureAwait(false);
            return new ServiceResult<EstablishedTradeDefinition[]>(page.Items);
        }
    }
}
