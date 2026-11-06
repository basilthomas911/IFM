using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Queries;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Query.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Query.Model;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Query;
/// <summary>Handles <see cref = "GetIntrinsicTimeStrategyWorkflowObservationQuery"/>.</summary>
public static class GetIntrinsicTimeStrategyWorkflowObservation
{

    extension(GetIntrinsicTimeStrategyWorkflowObservationQuery query)
    {
        /// <summary>Executes the mapped workflow projection query and returns its typed result.</summary>
        public ValueTask<ServiceResult<IntrinsicTimeStrategyWorkflowObservationReadModel>> ExecuteAsync(IIntrinsicTimeStrategyWorkflowQueryContext services, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(query);
            return IntrinsicTimeStrategyWorkflowQueryModel.ExecuteAsync(services, query, cancellationToken);
        }
    }
}
