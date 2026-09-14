using TomasAI.IFM.Domain.Trade.Futures.Position.Plan.Query.Actor;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Domain.Trade.Position.Query;

namespace TomasAI.IFM.Domain.Trade.Futures.Position.Plan.Query;

/// <summary>Handles the current outright Futures Trade Plan query.</summary>
public static class GetCurrentFuturesTradePlan
{
    /// <summary>Reads and replies with the current material plan.</summary>
    public static ValueTask ExecuteAsync(this GetCurrentFuturesTradePlanQuery query,
        IFuturesTradePlanQueryContext context, CancellationToken cancellationToken) =>
        StrategyTradePlanQueryExecution.ReplyCurrentAsync(context, context.DbFactory, query,
            query.PlanId.Position, TradeStrategyKind.FuturesOutright, query.PlanId.ValueDate, cancellationToken);
}
