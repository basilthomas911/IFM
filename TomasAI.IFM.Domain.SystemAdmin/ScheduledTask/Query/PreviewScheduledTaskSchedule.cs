using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Queries;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Query;
/// <summary>Handles PreviewScheduledTaskSchedule without consulting command-state objects.</summary>
public static class PreviewScheduledTaskSchedule
{

    extension(PreviewScheduledTaskScheduleQuery query)
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3236", Justification = "Validation targets a member of the request, but ParamName intentionally identifies the containing method argument so callers can identify the rejected request.")]
        /// <summary>Reads persisted projections or previews the supplied timing, and returns a typed result.</summary>
        public ValueTask<ServiceResult<ScheduledTaskSchedulePreview>> ExecuteAsync(TimeProvider clock, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(clock);
            ArgumentNullException.ThrowIfNull(query);
            cancellationToken.ThrowIfCancellationRequested();
            ArgumentException.ThrowIfNullOrWhiteSpace(query.Environment, nameof(query));
            ArgumentException.ThrowIfNullOrWhiteSpace(query.HostId, nameof(query));
            var result = ScheduledTaskTimingRules.Preview(query.Schedule, clock.GetUtcNow());
            return ValueTask.FromResult<ServiceResult<ScheduledTaskSchedulePreview>>(result is null ? new ServiceFailed<ScheduledTaskSchedulePreview>(404, "ScheduledTask projection was not found.") : new ServiceOk<ScheduledTaskSchedulePreview>(result));
        }
    }
}
