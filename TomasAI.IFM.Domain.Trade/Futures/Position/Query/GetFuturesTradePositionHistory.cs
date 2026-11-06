using TomasAI.IFM.Domain.Trade.Futures.Position.Query.Actor;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Position;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Position.Query;

public static class GetFuturesTradePositionHistory
{

    /// <param name="query">
    /// The query containing the position identity, date range, and paging criteria.
    /// </param>
    extension(GetFuturesTradePositionHistoryQuery query)
    {
        /// <summary>
        /// Reads the requested history page and returns only futures outright position snapshots.
        /// </summary>
        ///
        /// <param name = "context">The typed query context that provides projection storage.</param>
        /// <param name = "cancellationToken">The token that cancels the storage operation.</param>
        /// <returns>The typed projected result.</returns>
        public async ValueTask<ServiceResult<StrategyPositionSnapshot[]>> ExecuteAsync(IFuturesPositionQueryContext context, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(query);
            var page = await context.DbFactory.TradeDb.GetStrategyPositionHistoryAsync(query.PositionId, query.FromUtc, query.ToUtc, query.PageSize, query.PagingState, cancellationToken).ConfigureAwait(false);
            var positions = page.Items.Where(static position => position.StrategyKind == TradeStrategyKind.FuturesOutright).ToArray();
            return new ServiceResult<StrategyPositionSnapshot[]>(positions);
        }
    }
}
