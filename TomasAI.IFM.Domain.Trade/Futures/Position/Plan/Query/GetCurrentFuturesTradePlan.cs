using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Trade.Futures.Position.Plan.Query.Actor;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Domain.Trade.Model.Position.Query;

namespace TomasAI.IFM.Domain.Trade.Futures.Position.Plan.Query;
/// <summary>Handles the current outright Futures Trade Plan query.</summary>
public static class GetCurrentFuturesTradePlan
{

    extension(GetCurrentFuturesTradePlanQuery query)
    {
        /// <summary>Reads and returns the current material plan.</summary>
        public ValueTask<ServiceResult<StrategyTradePlanSnapshot?>> ExecuteAsync(IFuturesTradePlanQueryContext context, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(query);
            return StrategyTradePlanQueryExecution.ReadCurrentAsync(context.DbFactory, query.PlanId.Position, TradeStrategyKind.FuturesOutright, query.PlanId.ValueDate, cancellationToken);
        }
    }
}
