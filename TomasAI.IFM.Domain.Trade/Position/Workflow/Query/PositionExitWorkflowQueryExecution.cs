using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Position.Workflow.Query;

public static class PositionExitWorkflowQueryExecution
{
    public static async ValueTask ReplyCurrentAsync<TActor>(
        IQueryActorContext<TActor> context,
        IDbContextFactory dbFactory,
        GetPositionExitWorkflowQuery query,
        CancellationToken cancellationToken)
        where TActor : IActor
    {
        var result = await dbFactory.TradePlanDb.DbReader.GetCurrentExitWorkflowAsync(
            query.PositionId, query.ValueDate, cancellationToken).ConfigureAwait(false);
        await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb,
            new ServiceResult<ExitPositionWorkflowProjection?>(result)).ConfigureAwait(false);
    }

    public static async ValueTask ReplyTimelineAsync<TActor>(
        IQueryActorContext<TActor> context,
        IDbContextFactory dbFactory,
        GetPositionExitWorkflowTimelineQuery query,
        CancellationToken cancellationToken)
        where TActor : IActor
    {
        var result = await dbFactory.TradePlanDb.DbReader.GetExitWorkflowTimelineAsync(
            query.PositionId, query.ValueDate, query.PageSize, query.PagingState,
            cancellationToken).ConfigureAwait(false);
        PositionExitWorkflowHistoryPage page = new(result.Items, result.PagingState);
        await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb,
            new ServiceResult<PositionExitWorkflowHistoryPage>(page)).ConfigureAwait(false);
    }
}
