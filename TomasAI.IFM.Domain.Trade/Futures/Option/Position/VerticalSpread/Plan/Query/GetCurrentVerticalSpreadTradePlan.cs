using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Plan.Query.Actor;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Domain.Trade.Model.Position.Query;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Plan.Query;
/// <summary>Handles the current Vertical Spread Trade Plan query.</summary>
public static class GetCurrentVerticalSpreadTradePlan
{

    extension(GetCurrentVerticalSpreadTradePlanQuery query)
    {
        /// <summary>Reads and returns the current material plan.</summary>
        public ValueTask<ServiceResult<StrategyTradePlanSnapshot?>> ExecuteAsync(IVerticalSpreadTradePlanQueryContext context, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(query);
            return StrategyTradePlanQueryExecution.ReadCurrentAsync(context.DbFactory, query.PlanId.Position, TradeStrategyKind.VerticalSpread, query.PlanId.ValueDate, cancellationToken);
        }
    }
}
