using Quartz;
using TomasAI.IFM.Application.ServerManager.Contracts;

namespace TomasAI.IFM.Application.ServerManager.SchedulerHost;

public sealed class SchedulerOperationsService(
    SchedulerHostOptions options,
    TaskCatalogProvider catalog,
    ScheduleValidationService validator,
    SchedulerStore store,
    QuartzScheduleReconciler reconciler,
    ISchedulerFactory schedulerFactory,
    ActiveRunRegistry activeRuns,
    SchedulerRetentionService retention)
{
    public ScheduleValidationResultDto Validate(ScheduleDefinitionInputDto input) => validator.Validate(input);

    public async Task<SchedulerOperationResultDto> CreateAsync(
        Guid requestId,
        string actor,
        ScheduleDefinitionInputDto input,
        CancellationToken cancellationToken)
    {
        var validation = RequireValid(input);
        var task = catalog.GetRequired(input.TaskKey);
        var result = await store.CreateScheduleAsync(
            requestId,
            actor,
            input,
            validation.Explanation,
            task.ManifestVersion,
            cancellationToken);
        await ReconcileAsync(cancellationToken);
        return result;
    }

    public async Task<SchedulerOperationResultDto> UpdateAsync(
        Guid requestId,
        string actor,
        long expectedVersion,
        ScheduleDefinitionInputDto input,
        CancellationToken cancellationToken)
    {
        var validation = RequireValid(input);
        var task = catalog.GetRequired(input.TaskKey);
        var result = await store.UpdateScheduleAsync(
            requestId,
            actor,
            expectedVersion,
            input,
            validation.Explanation,
            task.ManifestVersion,
            cancellationToken);
        await ReconcileAsync(cancellationToken);
        return result;
    }

    public async Task<SchedulerOperationResultDto> SetEnabledAsync(
        Guid requestId,
        string actor,
        long expectedVersion,
        SetScheduleEnabledDto input,
        string? reason,
        CancellationToken cancellationToken)
    {
        var requiredReason = RequireReason(reason);
        var schedule = await store.GetScheduleAsync(input.ScheduleDefinitionId, cancellationToken);
        var task = catalog.GetRequired(schedule.TaskKey);
        if (input.Enabled && !string.Equals(task.RequiredEnvironment, options.Environment, StringComparison.OrdinalIgnoreCase))
        {
            throw new SchedulerValidationException(
                $"Task requires environment '{task.RequiredEnvironment}', but Scheduler Host is '{options.Environment}'.");
        }

        if (input.Enabled && !task.IsExecutableAvailable(options))
        {
            throw new SchedulerValidationException("The approved executable must be deployed and hash-valid before enabling this schedule.");
        }

        var result = await store.SetScheduleEnabledAsync(
            requestId,
            actor,
            expectedVersion,
            input,
            requiredReason,
            cancellationToken);
        await ReconcileAsync(cancellationToken);
        return result;
    }

    public async Task<SchedulerOperationResultDto> DeleteAsync(
        Guid requestId,
        string actor,
        long expectedVersion,
        Guid scheduleId,
        string? reason,
        CancellationToken cancellationToken)
    {
        var result = await store.DeleteScheduleAsync(
            requestId,
            actor,
            expectedVersion,
            scheduleId,
            RequireReason(reason),
            cancellationToken);
        await ReconcileAsync(cancellationToken);
        return result;
    }

    public async Task<SchedulerOperationResultDto> RunNowAsync(
        Guid requestId,
        string actor,
        Guid scheduleId,
        string? reason,
        CancellationToken cancellationToken)
    {
        var schedule = await store.GetScheduleAsync(scheduleId, cancellationToken);
        _ = catalog.GetRequired(schedule.TaskKey);
        return await store.QueueRunRequestAsync(
            requestId,
            SchedulerProtocol.RunNowOperation,
            actor,
            schedule.TaskKey,
            scheduleId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            ScheduledRunOrigin.Manual,
            schedule.MaximumRuntimeSeconds,
            RequireReason(reason),
            cancellationToken);
    }

    public async Task<SchedulerOperationResultDto> RetryAsync(
        Guid requestId,
        string actor,
        Guid priorRunId,
        string? reason,
        CancellationToken cancellationToken)
    {
        var prior = await store.GetRunAsync(priorRunId, cancellationToken);
        if (prior.State is ScheduledRunState.Planned or ScheduledRunState.Starting or ScheduledRunState.Running
            or ScheduledRunState.Cancelling or ScheduledRunState.Succeeded or ScheduledRunState.Abandoned)
        {
            throw new SchedulerValidationException($"Run state '{prior.State}' is not eligible for explicit retry.");
        }

        return await store.QueueRunRequestAsync(
            requestId,
            SchedulerProtocol.RetryRunOperation,
            actor,
            prior.TaskKey,
            prior.ScheduleDefinitionId,
            Guid.NewGuid(),
            prior.OccurrenceId,
            Guid.NewGuid(),
            ScheduledRunOrigin.Retry,
            prior.ScheduleDefinitionId is null
                ? null
                : (await store.GetScheduleAsync(prior.ScheduleDefinitionId.Value, cancellationToken)).MaximumRuntimeSeconds,
            RequireReason(reason),
            cancellationToken);
    }

    public async Task<SchedulerOperationResultDto> CancelAsync(
        Guid requestId,
        string actor,
        Guid runId,
        string? reason,
        CancellationToken cancellationToken)
    {
        var run = await store.GetRunAsync(runId, cancellationToken);
        if (run.State is not (ScheduledRunState.Planned or ScheduledRunState.Starting or ScheduledRunState.Running))
        {
            throw new SchedulerValidationException($"Run state '{run.State}' cannot be cancelled.");
        }

        var result = await store.RecordControlOperationAsync(
            requestId,
            SchedulerProtocol.CancelRunOperation,
            actor,
            runId,
            "CancellationRequested",
            RequireReason(reason),
            "Cancellation requested.",
            cancellationToken);
        if (!result.Replayed && !activeRuns.RequestCancellation(runId))
        {
            throw new SchedulerConflictException("The run is no longer owned by this Scheduler Host instance.");
        }

        return result;
    }

    public async Task<SchedulerOperationResultDto> RunRetentionAsync(
        Guid requestId,
        string actor,
        string? reason,
        CancellationToken cancellationToken)
    {
        var requiredReason = RequireReason(reason);
        var result = await store.RecordControlOperationAsync(
            requestId,
            SchedulerProtocol.RunRetentionOperation,
            actor,
            Guid.Empty,
            "RetentionRequested",
            requiredReason,
            "Retention cleanup accepted.",
            cancellationToken);
        if (!result.Replayed)
        {
            await retention.RunAsync(actor, requiredReason, cancellationToken);
        }

        return result;
    }

    private ScheduleValidationResultDto RequireValid(ScheduleDefinitionInputDto input)
    {
        var result = validator.Validate(input);
        if (!result.IsValid)
        {
            throw new SchedulerValidationException(string.Join(" ", result.Errors));
        }

        return result;
    }

    private static string RequireReason(string? reason)
        => !string.IsNullOrWhiteSpace(reason) && reason.Trim().Length >= 4
            ? reason.Trim()
            : throw new SchedulerValidationException("An operator reason of at least four characters is required.");

    private async Task ReconcileAsync(CancellationToken cancellationToken)
    {
        var scheduler = await schedulerFactory.GetScheduler(cancellationToken);
        await reconciler.ReconcileAsync(scheduler, cancellationToken);
    }
}
