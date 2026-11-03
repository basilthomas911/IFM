using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Queries;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Query.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Query.Model;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Query;
/// <summary>Handles <see cref = "GetIntrinsicTimeStrategyWorkflowObservationQuery"/>.</summary>
public static class GetIntrinsicTimeStrategyWorkflowObservation
{

    extension(GetIntrinsicTimeStrategyWorkflowObservationQuery query)
    {
        /// <summary>Executes the mapped workflow projection query and replies with its typed result.</summary>
        public ValueTask ExecuteAsync(IIntrinsicTimeStrategyWorkflowQueryContext services, IQueryActorContext<IntrinsicTimeStrategyWorkflowQueryActor> context, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(query);
            return IntrinsicTimeStrategyWorkflowQueryModel.ExecuteAsync(services, context, query, cancellationToken);
        }
    }
}
