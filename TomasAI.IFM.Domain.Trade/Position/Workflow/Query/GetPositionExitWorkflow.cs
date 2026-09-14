using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;
using TomasAI.IFM.Domain.Trade.Position.Workflow.Query.Actor;

namespace TomasAI.IFM.Domain.Trade.Position.Workflow.Query;

public static class GetPositionExitWorkflow
{
    public static ValueTask ExecuteAsync(
        this GetPositionExitWorkflowQuery query,
        IPositionExitWorkflowQueryContext context,
        CancellationToken cancellationToken) =>
        PositionExitWorkflowQueryExecution.ReplyCurrentAsync(
            context, context.DbFactory, query, cancellationToken);
}
