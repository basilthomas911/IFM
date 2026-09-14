using TomasAI.IFM.Domain.Trade.Futures.Position.Plan.Query.Actor;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Domain.Trade.Position.Query;

namespace TomasAI.IFM.Domain.Trade.Futures.Position.Plan.Query;

/// <summary>Handles the outright Futures Trade Plan history query.</summary>
public static class GetFuturesTradePlanHistory
{
    /// <summary>Reads and replies with one bounded history page.</summary>
    public static ValueTask ExecuteAsync(this GetFuturesTradePlanHistoryQuery query,
        IFuturesTradePlanQueryContext context, CancellationToken cancellationToken) =>
        StrategyTradePlanQueryExecution.ReplyHistoryAsync(context, context.DbFactory, query,
            query.PlanId.Position, TradeStrategyKind.FuturesOutright, query.PlanId.ValueDate,
            query.PageSize, query.PagingState, cancellationToken);
}
