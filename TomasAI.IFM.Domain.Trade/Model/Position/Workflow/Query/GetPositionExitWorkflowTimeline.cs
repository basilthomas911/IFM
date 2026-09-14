using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;
using TomasAI.IFM.Domain.Trade.Model.Position.Workflow.Query.Actor;

namespace TomasAI.IFM.Domain.Trade.Model.Position.Workflow.Query;

/// <summary>Handles the position exit-workflow timeline query.</summary>
public static class GetPositionExitWorkflowTimeline
{
    /// <summary>Reads a workflow timeline page and replies through the owning query actor.</summary>
    /// <param name="query">The workflow-timeline query.</param>
    /// <param name="context">The typed query-actor context.</param>
    /// <param name="cancellationToken">Cancels storage access.</param>
    /// <returns>A task that completes after the reply is sent.</returns>
    public static ValueTask ExecuteAsync(
        this GetPositionExitWorkflowTimelineQuery query,
        IPositionExitWorkflowQueryContext context,
        CancellationToken cancellationToken) =>
        PositionExitWorkflowQueryExecution.ReplyTimelineAsync(
            context, context.DbFactory, query, cancellationToken);
}
