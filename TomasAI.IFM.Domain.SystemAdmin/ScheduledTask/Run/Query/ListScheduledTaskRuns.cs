using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Run.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Run.Queries;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Queries;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Run.Query;
/// <summary>Handles ListScheduledTaskRuns without consulting command-state objects.</summary>
public static class ListScheduledTaskRuns
{

    extension(ListScheduledTaskRunsQuery query)
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3236", Justification = "Validation targets a member of the request, but ParamName intentionally identifies the containing method argument so callers can identify the rejected request.")]
        /// <summary>Reads persisted projections or previews the supplied timing, and returns a typed result.</summary>
        public async ValueTask<ServiceResult<ScheduledTaskRun[]>> ExecuteAsync(IScheduledTaskReadStore store, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(query);
            ArgumentNullException.ThrowIfNull(store);
            ArgumentException.ThrowIfNullOrWhiteSpace(query.Environment, nameof(query));
            ArgumentException.ThrowIfNullOrWhiteSpace(query.HostId, nameof(query));
            var result = await store.GetRunsAsync(query.Environment, query.HostId, query.ScheduleId, query.PageSize, cancellationToken).ConfigureAwait(false);
            return result is null ? new ServiceFailed<ScheduledTaskRun[]>(404, "ScheduledTask projection was not found.") : new ServiceOk<ScheduledTaskRun[]>(result);
        }
    }
}
