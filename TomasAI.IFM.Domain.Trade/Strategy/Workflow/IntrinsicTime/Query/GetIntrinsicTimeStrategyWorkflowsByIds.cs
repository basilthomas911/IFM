using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Queries;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Query.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Query.Model;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Query;
/// <summary>Handles batched workflow projection queries by identifier.</summary>
public static class GetIntrinsicTimeStrategyWorkflowsByIds
{

    extension(GetIntrinsicTimeStrategyWorkflowsByIdsQuery query)
    {
        /// <summary>Executes the mapped batch query and returns its typed result.</summary>
        public ValueTask<ServiceResult<IntrinsicTimeStrategyWorkflowReadModel[]>> ExecuteAsync(IIntrinsicTimeStrategyWorkflowQueryContext services, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(query);
            return IntrinsicTimeStrategyWorkflowQueryModel.ExecuteAsync(services, query, cancellationToken);
        }
    }
}
