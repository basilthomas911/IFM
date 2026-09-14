using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Model.Position.Workflow.Query;

/// <summary>Provides shared storage and reply operations for position exit-workflow queries.</summary>
public static class PositionExitWorkflowQueryExecution
{
    /// <summary>Reads and replies with the current exit-workflow projection.</summary>
    /// <typeparam name="TActor">The query actor that owns the reply mailbox.</typeparam>
    /// <param name="context">The query actor context.</param>
    /// <param name="dbFactory">The database-context factory.</param>
    /// <param name="query">The current-workflow query.</param>
    /// <param name="cancellationToken">Cancels storage access.</param>
    /// <returns>A task that completes after the reply is sent.</returns>
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

    /// <summary>Reads and replies with one bounded exit-workflow history page.</summary>
    /// <typeparam name="TActor">The query actor that owns the reply mailbox.</typeparam>
    /// <param name="context">The query actor context.</param>
    /// <param name="dbFactory">The database-context factory.</param>
    /// <param name="query">The workflow-timeline query.</param>
    /// <param name="cancellationToken">Cancels storage access.</param>
    /// <returns>A task that completes after the reply is sent.</returns>
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
