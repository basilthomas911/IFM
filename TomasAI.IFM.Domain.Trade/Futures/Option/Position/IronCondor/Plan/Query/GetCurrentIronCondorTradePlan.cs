using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Query.Actor;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Domain.Trade.Model.Position.Query;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Query;
/// <summary>Handles the current Iron Condor Trade Plan query.</summary>
public static class GetCurrentIronCondorTradePlan
{

    extension(GetCurrentIronCondorTradePlanQuery query)
    {
        /// <summary>Reads and returns the current material plan.</summary>
        public ValueTask<ServiceResult<StrategyTradePlanSnapshot?>> ExecuteAsync(IIronCondorTradePlanQueryContext context, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(query);
            return StrategyTradePlanQueryExecution.ReadCurrentAsync(context.DbFactory, query.PlanId.Position, TradeStrategyKind.IronCondor, query.PlanId.ValueDate, cancellationToken);
        }
    }
}
