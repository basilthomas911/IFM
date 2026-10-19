using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Query.Actor;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Domain.Trade.Model.Position.Query;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Query;
/// <summary>Handles the Iron Condor Trade Plan history query.</summary>
public static class GetIronCondorTradePlanHistory
{
    /// <summary>Reads and replies with one bounded history page.</summary>
    public static async ValueTask ExecuteAsync(this GetIronCondorTradePlanHistoryQuery query, IIronCondorTradePlanQueryContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(query);
        if (query.Offset is { } offset)
        {
            var page = await context.DbFactory.TradePlanDb.DbReader.GetOffsetHistoryAsync(query.PlanId.Position,
                TradeStrategyKind.IronCondor, query.PlanId.ValueDate, offset, query.PageSize, query.MaximumRevision, cancellationToken).ConfigureAwait(false);
            await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb,
                new TomasAI.IFM.Shared.EventSourcing.ServiceResult<StrategyTradePlanHistoryPage>(page)).ConfigureAwait(false);
            return;
        }
        await StrategyTradePlanQueryExecution.ReplyHistoryAsync(context, context.DbFactory, query, query.PlanId.Position, TradeStrategyKind.IronCondor, query.PlanId.ValueDate, query.PageSize, query.PagingState, cancellationToken).ConfigureAwait(false);
    }
}
