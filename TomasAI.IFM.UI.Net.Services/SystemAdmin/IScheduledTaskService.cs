using TomasAI.IFM.UI.Net.Models.SystemAdmin;
using TomasAI.IFM.UI.Net.Services.Operations;
using TomasAI.IFM.UI.Net.Services.Subscriptions;
namespace TomasAI.IFM.UI.Net.Services.SystemAdmin;
/// <summary>Defines typed scheduled-task operations at the presentation boundary.</summary>
public interface IScheduledTaskService
{
    /// <summary>Reads the next persisted occurrence-history page.</summary>
    ValueTask<UiOperationResult<ScheduledTaskRunPageUiModel>> LoadRunHistoryAsync(string environment, string hostId, Guid scheduleId, byte[]? pagingState, CancellationToken cancellationToken = default);
    /// <summary>Reads a retained stdout page by persisted occurrence identity.</summary>
    ValueTask<UiOperationResult<ScheduledTaskOutputPageUiModel>> LoadOutputAsync(string environment, string hostId, Guid scheduleId, ScheduledTaskRunUiModel run, long offset, CancellationToken cancellationToken = default);
    /// <summary>Loads persisted dashboard and optional selected-task runs.</summary>
    ValueTask<UiOperationResult<ScheduledTaskDashboardUiModel>> LoadAsync(string environment, string hostId, Guid? selectedSchedule, CancellationToken cancellationToken = default);
    /// <summary>Creates or changes a disabled/desired schedule using optimistic source revision.</summary>
    ValueTask<UiOperationResult> SaveAsync(Guid? id, long expectedRevision, ScheduledTaskScheduleUiModel schedule, CancellationToken cancellationToken = default);
    /// <summary>Requests enable or disable without claiming runtime installation.</summary>
    ValueTask<UiOperationResult> SetEnabledAsync(Guid id, long expectedRevision, bool enabled, CancellationToken cancellationToken = default);
    /// <summary>Removes a disabled schedule while retaining its source audit history.</summary>
    ValueTask<UiOperationResult> RemoveAsync(Guid id, long expectedRevision, CancellationToken cancellationToken = default);
    /// <summary>Requests a manually identified occurrence through the same actor admission workflow.</summary>
    ValueTask<UiOperationResult> RunNowAsync(ScheduledTaskDefinitionUiModel definition, CancellationToken cancellationToken = default);
    /// <summary>Records an explicitly reviewed uncertain occurrence as failed, without relaunching it.</summary>
    ValueTask<UiOperationResult> ResolveUncertainAsync(ScheduledTaskRunUiModel run, string reason, CancellationToken cancellationToken = default);
    /// <summary>Previews portable Quartz timing without updating state.</summary>
    ValueTask<UiOperationResult<ScheduledTaskPreviewUiModel>> PreviewAsync(ScheduledTaskScheduleUiModel schedule, CancellationToken cancellationToken = default);
    /// <summary>Creates an independently owned public-event subscription.</summary>
    IUiEventSubscription CreateNotificationSubscription(Func<Guid, ValueTask> handler);
}
