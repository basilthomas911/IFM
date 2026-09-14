using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Domain.Trade.Position.Query.Actor;

namespace TomasAI.IFM.Domain.Trade.Position.Query;

public static class GetStrategyTradePlanActivity
{
    public static ValueTask ExecuteAsync(
        this GetStrategyTradePlanActivityQuery query,
        IStrategyTradePlanActivityQueryContext context,
        CancellationToken cancellationToken) =>
        StrategyTradePlanQueryExecution.ReplyActivityAsync(
            context, context.DbFactory, query, cancellationToken);
}
