using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;
using TomasAI.IFM.Domain.Trade.Model.Position.Workflow.Query.Actor;

namespace TomasAI.IFM.Domain.Trade.Model.Position.Workflow.Query;

/// <summary>Handles the current position exit-workflow query.</summary>
public static class GetPositionExitWorkflow
{
    /// <summary>Reads the current workflow projection and replies through the owning query actor.</summary>
    /// <param name="query">The current-workflow query.</param>
    /// <param name="context">The typed query-actor context.</param>
    /// <param name="cancellationToken">Cancels storage access.</param>
    /// <returns>A task that completes after the reply is sent.</returns>
    public static ValueTask ExecuteAsync(
        this GetPositionExitWorkflowQuery query,
        IPositionExitWorkflowQueryContext context,
        CancellationToken cancellationToken) =>
        PositionExitWorkflowQueryExecution.ReplyCurrentAsync(
            context, context.DbFactory, query, cancellationToken);
}
