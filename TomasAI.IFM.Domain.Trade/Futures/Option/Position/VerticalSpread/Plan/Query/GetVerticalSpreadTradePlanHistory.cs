using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Plan.Query.Actor;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Domain.Trade.Model.Position.Query;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Plan.Query;
/// <summary>Handles the Vertical Spread Trade Plan history query.</summary>
public static class GetVerticalSpreadTradePlanHistory
{

    extension(GetVerticalSpreadTradePlanHistoryQuery query)
    {
        /// <summary>Reads and returns one bounded history page.</summary>
        public ValueTask<ServiceResult<StrategyTradePlanHistoryPage>> ExecuteAsync(IVerticalSpreadTradePlanQueryContext context, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(query);
            return StrategyTradePlanQueryExecution.ReadHistoryAsync(context.DbFactory, query.PlanId.Position, TradeStrategyKind.VerticalSpread, query.PlanId.ValueDate, query.PageSize, query.PagingState, cancellationToken);
        }
    }
}
