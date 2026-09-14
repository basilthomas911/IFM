using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Query.Actor;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Domain.Trade.Position.Query;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Query;

/// <summary>Handles the Iron Condor Trade Plan history query.</summary>
public static class GetIronCondorTradePlanHistory
{
    /// <summary>Reads and replies with one bounded history page.</summary>
    public static ValueTask ExecuteAsync(this GetIronCondorTradePlanHistoryQuery query,
        IIronCondorTradePlanQueryContext context, CancellationToken cancellationToken) =>
        StrategyTradePlanQueryExecution.ReplyHistoryAsync(context, context.DbFactory, query,
            query.PlanId.Position, TradeStrategyKind.IronCondor, query.PlanId.ValueDate,
            query.PageSize, query.PagingState, cancellationToken);
}
