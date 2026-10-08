using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Commands;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.ServiceApi;
using TomasAI.IFM.UI.EventConsumer;
using TomasAI.IFM.UI.Net.Models.SystemAdmin;
using TomasAI.IFM.UI.Net.Services.Operations;
using TomasAI.IFM.UI.Net.Services.Subscriptions;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.UI.Net.Services.SystemAdmin;
/// <summary>Maps the actor command/query API and public notifications into UI-owned models.</summary>
public sealed class ScheduledTaskService(IScheduledTaskCommandApi commands, IScheduledTaskQueryApi queries,
    IScheduledTaskUIEventConsumer eventConsumer) : IScheduledTaskService
{
    /// <inheritdoc />
    public async ValueTask<UiOperationResult<ScheduledTaskRunPageUiModel>> LoadRunHistoryAsync(string environment, string hostId, Guid scheduleId, byte[]? pagingState, CancellationToken cancellationToken = default)
    {
        var result = await queries.GetScheduledTaskRunHistoryAsync(new() { Environment = environment, HostId = hostId, ScheduleId = new(scheduleId), PageSize = 100, PagingState = pagingState }, cancellationToken);
        return result.Success && result.Value is { } page
            ? UiOperationResult<ScheduledTaskRunPageUiModel>.Success(new(page.Runs.Select(MapRun).ToArray(), page.PagingState))
            : UiOperationResult<ScheduledTaskRunPageUiModel>.Failure(result.ErrorCode, result.ErrorMessage);
    }
    /// <inheritdoc />
    public async ValueTask<UiOperationResult<ScheduledTaskOutputPageUiModel>> LoadOutputAsync(string environment, string hostId, Guid scheduleId, ScheduledTaskRunUiModel run, long offset, CancellationToken cancellationToken = default)
    {
        var result = await queries.GetScheduledTaskOutputAsync(new() { EntityId = new(run.Id), Environment = environment, HostId = hostId, ScheduleId = new(scheduleId), IntendedFireTimeUtc = run.IntendedFireTimeUtc, Offset = offset }, cancellationToken);
        return result.Success && result.Value is { } page
            ? UiOperationResult<ScheduledTaskOutputPageUiModel>.Success(new(page.Text, page.NextOffset, page.EndOfOutput, page.Available))
            : UiOperationResult<ScheduledTaskOutputPageUiModel>.Failure(result.ErrorCode, result.ErrorMessage);
    }
    /// <summary>Maps one persisted run receipt without changing its business outcome.</summary>
    private static ScheduledTaskRunUiModel MapRun(ScheduledTaskRun r) => new(r.Id.Value, r.IntendedFireTimeUtc, r.Status.ToString(), r.Stage, r.Detail, r.StartedAtUtc, r.FinishedAtUtc, r.ExitCode, r.ValueDate, r.Revision, r.OperationCommandId, r.ProcessId, r.StandardOutputTail, r.StandardErrorTail, r.OutputDirectory);
    /// <inheritdoc />
    public async ValueTask<UiOperationResult<ScheduledTaskDashboardUiModel>> LoadAsync(string environment, string hostId, Guid? selectedSchedule, CancellationToken cancellationToken = default)
    {
        var result = await queries.GetScheduledTasksDashboardAsync(new() { Environment = environment, HostId = hostId, SelectedScheduleId = new(selectedSchedule ?? Guid.Empty), PageSize = 100 }, cancellationToken).ConfigureAwait(false);
        if (!result.Success || result.Value is null) return UiOperationResult<ScheduledTaskDashboardUiModel>.Failure(result.ErrorCode, result.ErrorMessage);
        var dashboard = result.Value;
        return UiOperationResult<ScheduledTaskDashboardUiModel>.Success(new(dashboard.Schedules.Select(d => new ScheduledTaskDefinitionUiModel(d.Id.Value, d.Revision, d.DesiredRevision, Map(d.Schedule), d.Enabled, d.Removed, d.InstallationStatus.ToString(), d.AppliedRevision, d.InstallationDetail, d.ActiveRunId)).ToArray(),
            dashboard.Catalog?.Projects.Select(p => new ScheduledTaskProjectUiModel(p.TaskKey, p.DisplayName, p.Available, p.Platform, p.ManifestVersion)).ToArray() ?? [],
            dashboard.RecentRuns.Select(r => new ScheduledTaskRunUiModel(r.Id.Value, r.IntendedFireTimeUtc, r.Status.ToString(), r.Stage, r.Detail, r.StartedAtUtc, r.FinishedAtUtc, r.ExitCode, r.ValueDate, r.Revision, r.OperationCommandId, r.ProcessId, r.StandardOutputTail, r.StandardErrorTail, r.OutputDirectory)).ToArray(),
            dashboard.Catalog?.HostCapability is { } h ? $"{h.Platform}: {(h.Ready ? "Ready" : "Unavailable")} - {h.Detail}" : "Host has not reported capability.", dashboard.Catalog?.HostCapability?.ObservedAtUtc));
    }
    /// <inheritdoc />
    public async ValueTask<UiOperationResult> SaveAsync(Guid? id, long expectedRevision, ScheduledTaskScheduleUiModel schedule, CancellationToken cancellationToken = default)
    {
        var result = id is { } existing
            ? await commands.ChangeScheduledTaskScheduleAsync(new() { CommandId = Guid.NewGuid(), EntityId = new(existing), ExpectedRevision = expectedRevision, Schedule = Map(schedule), Operator = Environment.UserName }, cancellationToken).ConfigureAwait(false)
            : await commands.CreateScheduledTaskAsync(new() { CommandId = Guid.NewGuid(), EntityId = new(Guid.NewGuid()), ExpectedRevision = 0, Schedule = Map(schedule), Operator = Environment.UserName }, cancellationToken).ConfigureAwait(false);
        return Map(result);
    }
    /// <inheritdoc />
    public async ValueTask<UiOperationResult> SetEnabledAsync(Guid id, long expectedRevision, bool enabled, CancellationToken cancellationToken = default) => Map(enabled
        ? await commands.EnableScheduledTaskAsync(new() { CommandId = Guid.NewGuid(), EntityId = new(id), ExpectedRevision = expectedRevision, Operator = Environment.UserName }, cancellationToken).ConfigureAwait(false)
        : await commands.DisableScheduledTaskAsync(new() { CommandId = Guid.NewGuid(), EntityId = new(id), ExpectedRevision = expectedRevision, Operator = Environment.UserName }, cancellationToken).ConfigureAwait(false));
    /// <inheritdoc />
    public async ValueTask<UiOperationResult> RemoveAsync(Guid id, long expectedRevision, CancellationToken cancellationToken = default) => Map(await commands.RemoveScheduledTaskAsync(new() { CommandId = Guid.NewGuid(), EntityId = new(id), ExpectedRevision = expectedRevision, Operator = Environment.UserName }, cancellationToken).ConfigureAwait(false));
    /// <inheritdoc />
    public async ValueTask<UiOperationResult> RunNowAsync(ScheduledTaskDefinitionUiModel definition, CancellationToken cancellationToken = default) => Map(await commands.RequestScheduledTaskRunAsync(new()
    {
        CommandId = Guid.NewGuid(), EntityId = new(Guid.NewGuid()), ScheduleId = new(definition.Id), DefinitionRevision = definition.DesiredRevision,
        IntendedFireTimeUtc = DateTimeOffset.UtcNow, Manual = true, TaskKey = definition.Schedule.TaskKey,
        Environment = definition.Schedule.Environment, HostId = definition.Schedule.HostId, Operator = Environment.UserName, Reason = "Manual System Admin request."
    }, cancellationToken).ConfigureAwait(false));
    /// <inheritdoc />
    public async ValueTask<UiOperationResult> ResolveUncertainAsync(ScheduledTaskRunUiModel run, string reason, CancellationToken cancellationToken = default)
        => Map(await commands.FailScheduledTaskRunAsync(new() { CommandId = Guid.NewGuid(), EntityId = new(run.Id), ExpectedRevision = run.Revision,
            Operator = Environment.UserName, Reason = reason, Stage = "OperatorResolved", Detail = "Operator reviewed the interrupted run; no automatic relaunch.", FinishedAtUtc = DateTimeOffset.UtcNow,
            OperationCommandId = run.OperationCommandId }, cancellationToken));
    /// <inheritdoc />
    public async ValueTask<UiOperationResult<ScheduledTaskPreviewUiModel>> PreviewAsync(ScheduledTaskScheduleUiModel schedule, CancellationToken cancellationToken = default)
    {
        var result = await queries.PreviewScheduledTaskScheduleAsync(new() { Schedule = Map(schedule), Environment = schedule.Environment, HostId = schedule.HostId }, cancellationToken).ConfigureAwait(false);
        return result.Success && result.Value is { } preview ? UiOperationResult<ScheduledTaskPreviewUiModel>.Success(new(preview.Valid, preview.Errors, preview.NextFireTimesUtc)) : UiOperationResult<ScheduledTaskPreviewUiModel>.Failure(result.ErrorCode, result.ErrorMessage);
    }
    /// <inheritdoc />
    public IUiEventSubscription CreateNotificationSubscription(Func<Guid, ValueTask> handler) => new OwnedUiEventSubscription(token => eventConsumer.StartScheduledTasksAsync(handler, token), eventConsumer.StopAsync);
    /// <summary>Maps command acceptance into a transport-neutral UI result.</summary>
    private static UiOperationResult Map(ServiceResult<GuidResult> result) => result.Success ? UiOperationResult.Success() : UiOperationResult.Failure(result.ErrorCode, result.ErrorMessage);
    /// <summary>Maps persisted task timing into UI editing values.</summary>
    private static ScheduledTaskScheduleUiModel Map(ScheduledTaskSchedule s) => new(s.TaskKey, s.Name, s.Environment, s.HostId, s.Timing == ScheduledTaskTiming.OneTime, s.Expression, s.TimeZoneId, s.StartsAtUtc, s.EndsAtUtc, s.MaximumRuntimeSeconds, s.DispatchToleranceSeconds, s.Description);
    /// <summary>Maps UI editing values into the concrete schedule contract.</summary>
    private static ScheduledTaskSchedule Map(ScheduledTaskScheduleUiModel s) => new() { TaskKey = s.TaskKey, Name = s.Name, Environment = s.Environment, HostId = s.HostId, Timing = s.OneTime ? ScheduledTaskTiming.OneTime : ScheduledTaskTiming.Cron, Expression = s.Expression, TimeZoneId = s.TimeZoneId, StartsAtUtc = s.StartsAtUtc, EndsAtUtc = s.EndsAtUtc, MaximumRuntimeSeconds = s.MaximumRuntimeSeconds, DispatchToleranceSeconds = s.DispatchToleranceSeconds, Description = s.Description };
}
