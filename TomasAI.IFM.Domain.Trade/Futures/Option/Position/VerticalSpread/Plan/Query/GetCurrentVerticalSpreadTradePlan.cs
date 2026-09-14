using TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Plan.Query.Actor;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Domain.Trade.Model.Position.Query;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Plan.Query;

/// <summary>Handles the current Vertical Spread Trade Plan query.</summary>
public static class GetCurrentVerticalSpreadTradePlan
{
    /// <summary>Reads and replies with the current material plan.</summary>
    public static ValueTask ExecuteAsync(this GetCurrentVerticalSpreadTradePlanQuery query,
        IVerticalSpreadTradePlanQueryContext context, CancellationToken cancellationToken) =>
        StrategyTradePlanQueryExecution.ReplyCurrentAsync(context, context.DbFactory, query,
            query.PlanId.Position, TradeStrategyKind.VerticalSpread, query.PlanId.ValueDate, cancellationToken);
}
