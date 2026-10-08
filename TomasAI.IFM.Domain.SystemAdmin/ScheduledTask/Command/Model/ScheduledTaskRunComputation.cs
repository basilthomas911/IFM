using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Commands;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Command.Model;
/// <summary>Calculates monotonic run receipts and business completion outcomes.</summary>
public static class ScheduledTaskRunComputation
{
    /// <summary>Derives a stable scheduled-occurrence identity independent of delivery retries and configuration revisions.</summary>
    public static ScheduledTaskId OccurrenceId(ScheduledTaskId scheduleId, DateTimeOffset intendedFireTimeUtc) => ScheduledTaskIdentities.Occurrence(scheduleId, intendedFireTimeUtc);
    /// <summary>Computes one run transition without relaunching an uncertain or terminal occurrence.</summary>
    public static ScheduledTaskRunChange Compute(ICommand<ScheduledTaskId> command, ScheduledTaskRun? run, DateTimeOffset now)
    {
        if (!command.EntityId.IsValid || command.CommandId == Guid.Empty) return Reject("IDENTITY.INVALID", "run and command identities are required");
        if (command is RequestScheduledTaskRunCommand request)
        {
            if (run is not null) return Reject("ALREADY_REQUESTED", "occurrence already exists; read its persisted outcome instead of launching again");
            if (!request.ScheduleId.IsValid || request.DefinitionRevision < 1 || string.IsNullOrWhiteSpace(request.TaskKey) || string.IsNullOrWhiteSpace(request.HostId) || string.IsNullOrWhiteSpace(request.Environment) || request.IntendedFireTimeUtc > now || !request.Manual && OccurrenceId(request.ScheduleId, request.IntendedFireTimeUtc) != request.EntityId) return Reject("REQUEST.INVALID", "scheduled occurrence identity and target must match the requested fire instant");
            return new(new() { Id = request.EntityId, ScheduleId = request.ScheduleId, Revision = 1, DefinitionRevision = request.DefinitionRevision, TaskKey = request.TaskKey, HostId = request.HostId, Environment = request.Environment, IntendedFireTimeUtc = request.IntendedFireTimeUtc, Manual = request.Manual, Reason = request.Reason, OperationCommandId = request.OperationCommandId == Guid.Empty ? request.CommandId : request.OperationCommandId });
        }
        if (run is null || run.Id != command.EntityId) return Reject("NOT_FOUND", "run has not been requested");
        if (command is FailScheduledTaskRunCommand resolution && run.Status == ScheduledTaskRunStatus.Uncertain)
            return resolution.Stage == "OperatorResolved" && resolution.ExpectedRevision == run.Revision &&
                !string.IsNullOrWhiteSpace(resolution.Operator) && !string.IsNullOrWhiteSpace(resolution.Reason) &&
                ValidFinish(run, resolution.FinishedAtUtc, now)
                ? new(run with { Revision = run.Revision + 1, Status = ScheduledTaskRunStatus.Failed,
                    Stage = resolution.Stage, Detail = resolution.Detail, Reason = resolution.Reason, FinishedAtUtc = resolution.FinishedAtUtc })
                : Reject("RESOLUTION.INVALID", "uncertain run requires explicit operator review, reason and current revision");
        if (run.Status is ScheduledTaskRunStatus.Succeeded or ScheduledTaskRunStatus.Failed or ScheduledTaskRunStatus.Uncertain or ScheduledTaskRunStatus.Rejected) return Reject("TERMINAL", "terminal or uncertain run cannot advance or relaunch");
        var next = run with { Revision = run.Revision + 1 };
        return command switch
        {
            RecordScheduledTaskRunAdmissionCommand c when run.Status != ScheduledTaskRunStatus.Requested => Reject("ADMISSION.OUT_OF_ORDER", "admission requires a requested run"),
            RecordScheduledTaskRunAdmissionCommand c => new(next with { Status = c.Accepted ? ScheduledTaskRunStatus.Admitted : ScheduledTaskRunStatus.Rejected, Detail = c.Detail, FinishedAtUtc = c.Accepted ? null : now }),
            RecordScheduledTaskRunStartedCommand c when run.Status != ScheduledTaskRunStatus.Admitted || c.ProcessId <= 0 || c.StartedAtUtc < run.IntendedFireTimeUtc || c.StartedAtUtc > now => Reject("START.INVALID", "process start must follow admission with a valid process and timestamp"),
            RecordScheduledTaskRunStartedCommand c => new(next with { Status = ScheduledTaskRunStatus.Running, ProcessId = c.ProcessId, StartedAtUtc = c.StartedAtUtc }),
            RecordScheduledTaskRunStageCommand c when c.Stage == "PositionsFinalized" &&
                (run.TaskKey != ScheduledTaskKeys.FuturesMarketClose || run.Stage != "FeedsStopped" || c.ValueDate == default ||
                 c.ValueDate.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday ||
                 TomasAI.IFM.Domain.MarketData.Shared.FuturesTradingValueDate.GetSessionEndUtc(c.ValueDate) > now)
                => Reject("EOD.OUT_OF_ORDER", "EOD finalization requires the market-close task, confirmed feed stop and an ended weekday session"),
            RecordScheduledTaskRunStageCommand c when run.Status != ScheduledTaskRunStatus.Running || c.ValueDate == default || string.IsNullOrWhiteSpace(c.Stage) || run.ValueDate is { } establishedDate && establishedDate != c.ValueDate => Reject("STAGE.INVALID", "business stages require a running task and one consistent value date"),
            RecordScheduledTaskRunStageCommand c => new(next with { Stage = c.Stage, ValueDate = c.ValueDate, Detail = c.Detail, CompletedEndOfDayValueDate = run.TaskKey == ScheduledTaskKeys.FuturesMarketClose && c.Stage == "PositionsFinalized" ? c.ValueDate : run.CompletedEndOfDayValueDate }),
            CompleteScheduledTaskRunCommand c when run.Status != ScheduledTaskRunStatus.Running || c.ExitCode != 0 || !ValidFinish(run, c.FinishedAtUtc, now) || string.IsNullOrWhiteSpace(c.Stage) => Reject("COMPLETION.INVALID", "business completion requires a running process, successful exit and completion stage"),
            CompleteScheduledTaskRunCommand c => new(next with { Status = ScheduledTaskRunStatus.Succeeded, FinishedAtUtc = c.FinishedAtUtc, ExitCode = c.ExitCode, ValueDate = c.ValueDate ?? run.ValueDate, Stage = c.Stage, Detail = c.Detail, StandardOutputTail = Limit(c.StandardOutputTail), StandardErrorTail = Limit(c.StandardErrorTail), OutputDirectory = c.OutputDirectory }),
            FailScheduledTaskRunCommand c when !ValidFinish(run, c.FinishedAtUtc, now) => Reject("FAILURE.INVALID", "failure timestamp precedes the run or is in the future"),
            FailScheduledTaskRunCommand c => new(next with { Status = ScheduledTaskRunStatus.Failed, FinishedAtUtc = c.FinishedAtUtc, ExitCode = c.ExitCode, Stage = c.Stage, Detail = c.Detail, StandardOutputTail = Limit(c.StandardOutputTail), StandardErrorTail = Limit(c.StandardErrorTail), OutputDirectory = c.OutputDirectory }),
            RecordScheduledTaskRunUncertainCommand c when !ValidFinish(run, c.FinishedAtUtc, now) => Reject("UNCERTAIN.INVALID", "uncertain outcome timestamp is invalid"),
            RecordScheduledTaskRunUncertainCommand c => new(next with { Status = ScheduledTaskRunStatus.Uncertain, FinishedAtUtc = c.FinishedAtUtc, Detail = c.Detail, StandardOutputTail = Limit(c.StandardOutputTail), StandardErrorTail = Limit(c.StandardErrorTail), OutputDirectory = c.OutputDirectory }),
            _ => Reject("COMMAND.UNSUPPORTED", "unsupported run operation")
        };
    }
    /// <summary>Bounds persisted diagnostics to the final 16 KiB of text per stream.</summary>
    private static string Limit(string value) => value.Length <= 16384 ? value : value[^16384..];
    /// <summary>Requires completion to follow process start, or request when no process start was confirmed.</summary>
    private static bool ValidFinish(ScheduledTaskRun run, DateTimeOffset finish, DateTimeOffset now) => finish >= (run.StartedAtUtc ?? run.IntendedFireTimeUtc) && finish <= now;
    /// <summary>Creates a readable run-specific rejection.</summary>
    private static ScheduledTaskRunChange Reject(string code, string detail) => new(null, $"ScheduledTaskRun.{code};{detail}");
}
