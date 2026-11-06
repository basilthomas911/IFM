using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Queries;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Query.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Query.Model;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Query;
/// <summary>Handles <see cref = "GetActiveIntrinsicTimeStrategyWorkflowQuery"/>.</summary>
public static class GetActiveIntrinsicTimeStrategyWorkflow
{

    extension(GetActiveIntrinsicTimeStrategyWorkflowQuery query)
    {
        /// <summary>Executes the mapped workflow projection query and returns its typed result.</summary>
        public ValueTask<ServiceResult<ActiveIntrinsicTimeStrategyWorkflowReadModel>> ExecuteAsync(IIntrinsicTimeStrategyWorkflowQueryContext services, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(query);
            ArgumentNullException.ThrowIfNull(services);
            return IntrinsicTimeStrategyWorkflowQueryModel.ExecuteAsync(services, query, cancellationToken);
        }
    }
}
