using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Queries;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Query.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Query.Model;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Query;

/// <summary>Handles paged workflow history queries.</summary>
public static class GetIntrinsicTimeStrategyWorkflowHistoryPage
{
    /// <summary>Executes the mapped history query and replies with its typed page.</summary>
    public static ValueTask ExecuteAsync(this GetIntrinsicTimeStrategyWorkflowHistoryPageQuery query,
        IIntrinsicTimeStrategyWorkflowQueryContext services,
        IQueryActorContext<IntrinsicTimeStrategyWorkflowQueryActor> context,
        CancellationToken cancellationToken)
        => IntrinsicTimeStrategyWorkflowQueryModel.ExecuteAsync(services, context, query, cancellationToken);
}
