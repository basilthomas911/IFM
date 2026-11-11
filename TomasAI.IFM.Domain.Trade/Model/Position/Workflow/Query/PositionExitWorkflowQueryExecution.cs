using TomasAI.IFM.Domain.Trade.Model.Position.Query;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Model.Position.Workflow.Query;
/// <summary>Provides shared storage operations for position exit-workflow queries.</summary>
public static class PositionExitWorkflowQueryExecution
{
    /// <summary>Reads and returns the current exit-workflow projection.</summary>
    /// <param name = "dbFactory">The database-context factory.</param>
    /// <param name = "query">The current-workflow query.</param>
    /// <param name = "cancellationToken">Cancels storage access.</param>
    /// <returns>The typed projected result.</returns>
    public static async ValueTask<ServiceResult<ExitPositionWorkflowProjection?>> ReadCurrentAsync(IDbContextFactory dbFactory, GetPositionExitWorkflowQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dbFactory);
        ArgumentNullException.ThrowIfNull(query);
        TradePlanRequestQualification.RequirePositionDate(query.PositionId, query.ValueDate);
        var result = await dbFactory.TradePlanDb.DbReader.GetCurrentExitWorkflowAsync(query.PositionId, query.ValueDate, cancellationToken).ConfigureAwait(false);
        return new ServiceResult<ExitPositionWorkflowProjection?>(result);
    }

    /// <summary>Reads and returns one bounded exit-workflow history page.</summary>
    /// <param name = "dbFactory">The database-context factory.</param>
    /// <param name = "query">The workflow-timeline query.</param>
    /// <param name = "cancellationToken">Cancels storage access.</param>
    /// <returns>The typed projected result.</returns>
    public static async ValueTask<ServiceResult<PositionExitWorkflowHistoryPage>> ReadTimelineAsync(IDbContextFactory dbFactory, GetPositionExitWorkflowTimelineQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dbFactory);
        ArgumentNullException.ThrowIfNull(query);
        TradePlanRequestQualification.RequirePositionDate(query.PositionId, query.ValueDate);
        TradePlanRequestQualification.RequirePageSize(query.PageSize);
        var result = await dbFactory.TradePlanDb.DbReader.GetExitWorkflowTimelineAsync(query.PositionId, query.ValueDate, query.PageSize, query.PagingState, cancellationToken).ConfigureAwait(false);
        PositionExitWorkflowHistoryPage page = new(result.Items, result.PagingState);
        return new ServiceResult<PositionExitWorkflowHistoryPage>(page);
    }
}
