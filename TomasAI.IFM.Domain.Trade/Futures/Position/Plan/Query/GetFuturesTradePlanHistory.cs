using TomasAI.IFM.Domain.Trade.Futures.Position.Plan.Query.Actor;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Domain.Trade.Model.Position.Query;

namespace TomasAI.IFM.Domain.Trade.Futures.Position.Plan.Query;
/// <summary>Handles the outright Futures Trade Plan history query.</summary>
public static class GetFuturesTradePlanHistory
{

    extension(GetFuturesTradePlanHistoryQuery query)
    {
        /// <summary>Reads and replies with one bounded history page.</summary>
        public ValueTask ExecuteAsync(IFuturesTradePlanQueryContext context, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(query);
            return StrategyTradePlanQueryExecution.ReplyHistoryAsync(context, context.DbFactory, query, query.PlanId.Position, TradeStrategyKind.FuturesOutright, query.PlanId.ValueDate, query.PageSize, query.PagingState, cancellationToken);
        }
    }
}
