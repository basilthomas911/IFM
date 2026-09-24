using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Queries;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Query.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Query.Model;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Query;

/// <summary>Handles batched workflow projection queries by identifier.</summary>
public static class GetIntrinsicTimeStrategyWorkflowsByIds
{
    /// <summary>Executes the mapped batch query and replies with its typed result.</summary>
    public static ValueTask ExecuteAsync(this GetIntrinsicTimeStrategyWorkflowsByIdsQuery query,
        IIntrinsicTimeStrategyWorkflowQueryContext services,
        IQueryActorContext<IntrinsicTimeStrategyWorkflowQueryActor> context,
        CancellationToken cancellationToken)
        => IntrinsicTimeStrategyWorkflowQueryModel.ExecuteAsync(services, context, query, cancellationToken);
}
