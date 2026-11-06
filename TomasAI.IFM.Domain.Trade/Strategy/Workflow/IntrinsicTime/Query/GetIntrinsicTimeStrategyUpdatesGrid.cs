using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Queries;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Query.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Query.Model;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Query;
/// <summary>Handles Strategy Updates grid queries.</summary>
public static class GetIntrinsicTimeStrategyUpdatesGrid
{

    extension(GetIntrinsicTimeStrategyUpdatesGridQuery query)
    {
        /// <summary>Executes the mapped grid query and returns its typed page.</summary>
        public ValueTask<ServiceResult<IntrinsicTimeStrategyUpdatesGridPage>> ExecuteAsync(IIntrinsicTimeStrategyWorkflowQueryContext services, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(query);
            ArgumentNullException.ThrowIfNull(services);
            return IntrinsicTimeStrategyWorkflowQueryModel.ExecuteAsync(services, query, cancellationToken);
        }
    }
}
