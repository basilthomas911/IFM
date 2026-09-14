using TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Plan.Query.Actor;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Domain.Trade.Position.Query;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Plan.Query;

/// <summary>Handles the Vertical Spread Trade Plan history query.</summary>
public static class GetVerticalSpreadTradePlanHistory
{
    /// <summary>Reads and replies with one bounded history page.</summary>
    public static ValueTask ExecuteAsync(this GetVerticalSpreadTradePlanHistoryQuery query,
        IVerticalSpreadTradePlanQueryContext context, CancellationToken cancellationToken) =>
        StrategyTradePlanQueryExecution.ReplyHistoryAsync(context, context.DbFactory, query,
            query.PlanId.Position, TradeStrategyKind.VerticalSpread, query.PlanId.ValueDate,
            query.PageSize, query.PagingState, cancellationToken);
}
