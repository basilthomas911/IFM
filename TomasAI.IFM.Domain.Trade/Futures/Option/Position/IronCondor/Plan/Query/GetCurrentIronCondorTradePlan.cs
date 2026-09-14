using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Query.Actor;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Domain.Trade.Model.Position.Query;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Query;

/// <summary>Handles the current Iron Condor Trade Plan query.</summary>
public static class GetCurrentIronCondorTradePlan
{
    /// <summary>Reads and replies with the current material plan.</summary>
    public static ValueTask ExecuteAsync(this GetCurrentIronCondorTradePlanQuery query,
        IIronCondorTradePlanQueryContext context, CancellationToken cancellationToken) =>
        StrategyTradePlanQueryExecution.ReplyCurrentAsync(context, context.DbFactory, query,
            query.PlanId.Position, TradeStrategyKind.IronCondor, query.PlanId.ValueDate, cancellationToken);
}
