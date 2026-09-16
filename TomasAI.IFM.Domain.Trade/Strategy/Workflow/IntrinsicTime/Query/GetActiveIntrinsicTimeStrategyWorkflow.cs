using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Queries;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Query.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Query.Model;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Query;

/// <summary>Handles <see cref="GetActiveIntrinsicTimeStrategyWorkflowQuery"/>.</summary>
public static class GetActiveIntrinsicTimeStrategyWorkflow
{
    /// <summary>Executes the mapped workflow projection query and replies with its typed result.</summary>
    public static ValueTask ExecuteAsync(this GetActiveIntrinsicTimeStrategyWorkflowQuery query, IIntrinsicTimeStrategyWorkflowQueryContext services, IQueryActorContext<IntrinsicTimeStrategyWorkflowQueryActor> context, CancellationToken cancellationToken)
        => IntrinsicTimeStrategyWorkflowQueryModel.ExecuteAsync(services, context, query, cancellationToken);
}
