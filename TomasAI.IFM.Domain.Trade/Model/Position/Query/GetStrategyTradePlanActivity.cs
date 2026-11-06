using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Domain.Trade.Model.Position.Query.Actor;

namespace TomasAI.IFM.Domain.Trade.Model.Position.Query;
/// <summary>Handles the cross-strategy Trade Plan activity query.</summary>
public static class GetStrategyTradePlanActivity
{

    /// <param name="query">
    /// The value-date activity query.
    /// </param>
    extension(GetStrategyTradePlanActivityQuery query)
    {
        /// <summary>Reads the requested activity page and replies through the owning query actor.</summary>
        ///
        /// <param name = "context">The typed query-actor context.</param>
        /// <param name = "cancellationToken">Cancels storage access.</param>
        /// <returns>A task that completes after the reply is sent.</returns>
        public ValueTask<ServiceResult<StrategyTradePlanActivityPage>> ExecuteAsync(IStrategyTradePlanActivityQueryContext context, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(context);
            return StrategyTradePlanQueryExecution.ReadActivityAsync(context.DbFactory, query, cancellationToken);
        }
    }
}
