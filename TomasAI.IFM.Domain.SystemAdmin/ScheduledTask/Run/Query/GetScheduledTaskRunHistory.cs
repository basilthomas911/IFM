using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Run.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Run.Queries;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Queries;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Run.Query;
/// <summary>Reads scheduled-task logs through persisted identities without consulting command state.</summary>
public static class GetScheduledTaskRunHistory
{

    extension(GetScheduledTaskRunHistoryQuery query)
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3236", Justification = "Validation targets a member of the request, but ParamName intentionally identifies the containing method argument so callers can identify the rejected request.")]
        /// <summary>Reads a bounded page and returns a typed result.</summary>
        public async ValueTask<ServiceResult<ScheduledTaskRunPage>> ExecuteAsync(IScheduledTaskReadStore store, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(query);
            ArgumentNullException.ThrowIfNull(store);
            ArgumentException.ThrowIfNullOrWhiteSpace(query.Environment, nameof(query));
            ArgumentException.ThrowIfNullOrWhiteSpace(query.HostId, nameof(query));
            var result = await (store.GetRunHistoryAsync(query.Environment, query.HostId, query.ScheduleId, query.PageSize, query.PagingState, cancellationToken)).ConfigureAwait(false);
            return new ServiceOk<ScheduledTaskRunPage>(result);
        }
    }
}
