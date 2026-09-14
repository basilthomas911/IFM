using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;
using TomasAI.IFM.Domain.Trade.Position.Workflow.Query.Actor;

namespace TomasAI.IFM.Domain.Trade.Position.Workflow.Query;

public static class GetPositionExitWorkflowTimeline
{
    public static ValueTask ExecuteAsync(
        this GetPositionExitWorkflowTimelineQuery query,
        IPositionExitWorkflowQueryContext context,
        CancellationToken cancellationToken) =>
        PositionExitWorkflowQueryExecution.ReplyTimelineAsync(
            context, context.DbFactory, query, cancellationToken);
}
