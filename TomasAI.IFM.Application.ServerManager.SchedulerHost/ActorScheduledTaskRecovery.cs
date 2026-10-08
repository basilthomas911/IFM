using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.ServiceApi;

namespace TomasAI.IFM.Application.ServerManager.SchedulerHost;

/// <summary>Reconciles persisted reservations after owner restart without replaying task business work.</summary>
public sealed class ActorScheduledTaskRecovery(IScheduledTaskCommandApi commands, IScheduledTaskQueryApi queries,
    ActiveRunRegistry activeRuns, ILogger<ActorScheduledTaskRecovery> logger)
{
    /// <summary>Releases confirmed terminal runs and marks interrupted runs uncertain, preserving their reservation for review.</summary>
    /// <param name="definition">The persisted definition with its admitted run and fire time.</param>
    /// <param name="cancellationToken">The bounded reconciliation deadline.</param>
    public async Task ReconcileAsync(ScheduledTaskDefinition definition, CancellationToken cancellationToken)
    {
        if (definition.ActiveRunId is not { } runId || activeRuns.Contains(runId)) return;
        if (definition.LastAdmittedFireUtc is not { } fire)
            throw new InvalidOperationException("Admitted schedule has no persisted fire time; operator review is required.");
        var receipt = await queries.GetScheduledTaskRunAsync(new() { EntityId = new(runId), ScheduleId = definition.Id,
            HostId = definition.Schedule.HostId, Environment = definition.Schedule.Environment, IntendedFireTimeUtc = fire }, cancellationToken);
        ActorScheduleRuntime.Require(receipt);
        var run = receipt.Value!;
        if (activeRuns.Contains(runId)) return;
        if (run.Status is ScheduledTaskRunStatus.Succeeded or ScheduledTaskRunStatus.Failed or ScheduledTaskRunStatus.Rejected)
            ActorScheduleRuntime.Require(await commands.RecordScheduledTaskRunCompletionAsync(new() { CommandId = Guid.NewGuid(), EntityId = definition.Id,
                RunId = runId, Operator = "QuartzHost", OperationCommandId = run.OperationCommandId }, cancellationToken));
        else if (run.Status != ScheduledTaskRunStatus.Uncertain)
        {
            ActorScheduleRuntime.Require(await commands.RecordScheduledTaskRunUncertainAsync(new() { CommandId = Guid.NewGuid(), EntityId = run.Id,
                FinishedAtUtc = DateTimeOffset.UtcNow, Operator = "QuartzHost", OperationCommandId = run.OperationCommandId,
                Detail = "Owning scheduler restarted or lost the process receipt. Business work will not be relaunched; review and resolve this run." }, cancellationToken));
            logger.LogWarning("{Component}.{Method} Interrupted task requires review {ScheduleId} {RunId} {Stage}",
                nameof(ActorScheduledTaskRecovery), nameof(ReconcileAsync), definition.Id.Format(), run.Id.Format(), run.Stage);
        }
    }
}
