using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Queries;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Query.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Query.Model;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Query;
/// <summary>Handles <see cref = "GetRecentIntrinsicTimeStrategyWorkflowsQuery"/>.</summary>
public static class GetRecentIntrinsicTimeStrategyWorkflows
{

    extension(GetRecentIntrinsicTimeStrategyWorkflowsQuery query)
    {
        /// <summary>Executes the mapped workflow projection query and replies with its typed result.</summary>
        public ValueTask ExecuteAsync(IIntrinsicTimeStrategyWorkflowQueryContext services, IQueryActorContext<IntrinsicTimeStrategyWorkflowQueryActor> context, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(query);
            ArgumentNullException.ThrowIfNull(services);
            return IntrinsicTimeStrategyWorkflowQueryModel.ExecuteAsync(services, context, query, cancellationToken);
        }
    }
}
