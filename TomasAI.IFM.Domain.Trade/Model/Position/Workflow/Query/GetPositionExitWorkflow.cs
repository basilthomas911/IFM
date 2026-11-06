using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;
using TomasAI.IFM.Domain.Trade.Model.Position.Workflow.Query.Actor;

namespace TomasAI.IFM.Domain.Trade.Model.Position.Workflow.Query;
/// <summary>Handles the current position exit-workflow query.</summary>
public static class GetPositionExitWorkflow
{

    /// <param name="query">
    /// The current-workflow query.
    /// </param>
    extension(GetPositionExitWorkflowQuery query)
    {
        /// <summary>Reads the current workflow projection and replies through the owning query actor.</summary>
        ///
        /// <param name = "context">The typed query-actor context.</param>
        /// <param name = "cancellationToken">Cancels storage access.</param>
        /// <returns>A task that completes after the reply is sent.</returns>
        public ValueTask<ServiceResult<ExitPositionWorkflowProjection?>> ExecuteAsync(IPositionExitWorkflowQueryContext context, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(context);
            return PositionExitWorkflowQueryExecution.ReadCurrentAsync(context.DbFactory, query, cancellationToken);
        }
    }
}
