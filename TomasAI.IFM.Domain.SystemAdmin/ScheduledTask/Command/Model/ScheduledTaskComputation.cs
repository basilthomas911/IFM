using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Commands;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Command.Model;
/// <summary>Calculates desired configuration, installation acknowledgements and exclusive run admission.</summary>
public static class ScheduledTaskComputation
{
    /// <summary>Computes one immutable definition change using explicit clock and persisted catalog inputs.</summary>
    public static ScheduledTaskChange Compute(ICommand<ScheduledTaskId> command, ScheduledTaskDefinition? definition,
        DateTimeOffset now, ScheduledTaskCatalog? catalog)
    {
        if (!command.EntityId.IsValid || command.CommandId == Guid.Empty) return Reject("IDENTITY.INVALID", "command and schedule identities are required");
        if (command is CreateScheduledTaskCommand create)
        {
            if (definition is not null) return Reject("ALREADY_EXISTS", "schedule already exists");
            var validation = ScheduledTaskTimingRules.Preview(create.Schedule, now);
            if (!validation.Valid) return Reject("SCHEDULE.INVALID", string.Join(";", validation.Errors));
            if (create.ExpectedRevision != 0) return Reject("REVISION.CONFLICT", "new schedule must expect revision zero");
            return new(new() { Id = create.EntityId, Revision = 1, DesiredRevision = 1, Schedule = create.Schedule,
                UpdatedBy = create.Operator, UpdatedAtUtc = now, OperationCommandId = Operation(create), InstallationStatus = ScheduledTaskInstallationStatus.Pending });
        }
        if (definition is null || definition.Id != command.EntityId) return Reject("NOT_FOUND", "schedule does not exist");
        var expected = command switch
        {
            ChangeScheduledTaskScheduleCommand c => c.ExpectedRevision,
            EnableScheduledTaskCommand c => c.ExpectedRevision,
            DisableScheduledTaskCommand c => c.ExpectedRevision,
            RemoveScheduledTaskCommand c => c.ExpectedRevision,
            _ => definition.Revision
        };
        if (expected != definition.Revision) return Reject("REVISION.CONFLICT", "reload the changed schedule before editing");
        if (definition.Removed && command is not (RecordScheduledTaskInstallationCommand or RecordScheduledTaskInstallationFailureCommand or RecordScheduledTaskRunCompletionCommand)) return Reject("REMOVED", "schedule has been removed");
        var next = definition with { Revision = definition.Revision + 1, UpdatedAtUtc = now };
        return command switch
        {
            ChangeScheduledTaskScheduleCommand c => Change(c, next, now),
            EnableScheduledTaskCommand c => Enable(c, next, catalog, now),
            DisableScheduledTaskCommand c => new(Desired(next with { Enabled = false }, c.Operator, Operation(c))),
            RemoveScheduledTaskCommand c when definition.Enabled || definition.ActiveRunId is not null => Reject("REMOVE.ACTIVE", "disable schedule and resolve its active run before removal"),
            RemoveScheduledTaskCommand c => new(Desired(next with { Removed = true }, c.Operator, Operation(c))),
            RecordScheduledTaskInstallationCommand c when c.DesiredRevision != definition.DesiredRevision || c.AppliedEnabled != (definition.Enabled && !definition.Removed) || string.IsNullOrWhiteSpace(c.Fingerprint) => Reject("INSTALLATION.STALE", "installation receipt does not match desired revision and enabled state"),
            RecordScheduledTaskInstallationCommand c => new(next with { AppliedRevision = c.DesiredRevision, AppliedEnabled = c.AppliedEnabled, InstallationStatus = ScheduledTaskInstallationStatus.Applied, InstallationDetail = c.Detail, InstallationFingerprint = c.Fingerprint }),
            RecordScheduledTaskInstallationFailureCommand c when c.DesiredRevision != definition.DesiredRevision => Reject("INSTALLATION.STALE", "failure receipt belongs to an older desired revision"),
            RecordScheduledTaskInstallationFailureCommand c => new(next with { InstallationStatus = ScheduledTaskInstallationStatus.Failed, InstallationDetail = c.Detail }),
            AdmitScheduledTaskRunCommand c => Admit(c, definition, next, catalog, now),
            RecordScheduledTaskRunCompletionCommand c when definition.ActiveRunId != c.RunId => Reject("RUN.OWNER_MISMATCH", "completion does not own the admitted run"),
            RecordScheduledTaskRunCompletionCommand => new(next with { ActiveRunId = null }),
            _ => Reject("COMMAND.UNSUPPORTED", "unsupported schedule operation")
        };
    }
    /// <summary>Validates timing before incrementing the desired installation revision.</summary>
    private static ScheduledTaskChange Change(ChangeScheduledTaskScheduleCommand command, ScheduledTaskDefinition next, DateTimeOffset now)
    {
        if (command.Schedule.Environment != next.Schedule.Environment || command.Schedule.HostId != next.Schedule.HostId || command.Schedule.TaskKey != next.Schedule.TaskKey) return Reject("TARGET.IMMUTABLE", "create a new schedule to change its owning host, environment or deployed task");
        var validation = ScheduledTaskTimingRules.Preview(command.Schedule, now);
        return validation.Valid ? new(Desired(next with { Schedule = command.Schedule }, command.Operator, Operation(command))) : Reject("SCHEDULE.INVALID", string.Join(";", validation.Errors));
    }
    /// <summary>Enables only a deployed compatible artifact on a currently ready persisted host.</summary>
    private static ScheduledTaskChange Enable(EnableScheduledTaskCommand command, ScheduledTaskDefinition next, ScheduledTaskCatalog? catalog, DateTimeOffset now)
    {
        var timing = ScheduledTaskTimingRules.Preview(next.Schedule, now);
        if (!timing.Valid) return Reject("SCHEDULE.INVALID", string.Join(";", timing.Errors));
        var rejection = ValidateCatalog(next, catalog, now, out var project);
        return rejection is not null ? rejection : new(Desired(next with { Enabled = true, ManifestVersion = project!.ManifestVersion, ReviewedArtifactDigest = project.ArtifactDigest }, command.Operator, Operation(command)));
    }
    /// <summary>Reserves one occurrence only after installation, freshness and overlap guards pass.</summary>
    private static ScheduledTaskChange Admit(AdmitScheduledTaskRunCommand command, ScheduledTaskDefinition current, ScheduledTaskDefinition next, ScheduledTaskCatalog? catalog, DateTimeOffset now)
    {
        if (!current.Enabled || !current.AppliedEnabled || current.InstallationStatus != ScheduledTaskInstallationStatus.Applied || current.AppliedRevision != current.DesiredRevision) return Reject("RUN.NOT_INSTALLED", "enabled desired revision is not confirmed installed");
        if (command.DefinitionRevision != current.DesiredRevision || command.HostId != current.Schedule.HostId || command.Environment != current.Schedule.Environment || command.RunId == Guid.Empty) return Reject("RUN.TARGET_MISMATCH", "run does not match installed definition and host");
        if (!command.Manual && !ScheduledTaskTimingRules.IsOccurrence(current.Schedule, command.IntendedFireTimeUtc)) return Reject("RUN.NOT_SCHEDULED", "intended fire instant does not match configured timing");
        if (current.ActiveRunId is not null) return Reject("RUN.OVERLAP", "an admitted run already owns this schedule");
        if (command.IntendedFireTimeUtc > now || now - command.IntendedFireTimeUtc > TimeSpan.FromSeconds(current.Schedule.DispatchToleranceSeconds)) return Reject("RUN.LATE", "occurrence is outside its dispatch tolerance");
        if (current.Schedule.StartsAtUtc is { } start && command.IntendedFireTimeUtc < start || current.Schedule.EndsAtUtc is { } end && command.IntendedFireTimeUtc > end) return Reject("RUN.OUTSIDE_BOUNDS", "occurrence is outside configured date bounds");
        if (!command.Manual && current.LastAdmittedFireUtc is { } previous && command.IntendedFireTimeUtc <= previous) return Reject("RUN.DUPLICATE", "occurrence has already been admitted or superseded");
        var rejection = ValidateCatalog(current, catalog, now, out var project);
        if (rejection is not null) return rejection;
        if (project!.ManifestVersion != current.ManifestVersion || project.ArtifactDigest != current.ReviewedArtifactDigest) return Reject("RUN.ARTIFACT_CHANGED", "review and enable the current deployed artifact before running");
        return new(next with { ActiveRunId = command.RunId, LastAdmittedFireUtc = command.IntendedFireTimeUtc });
    }
    /// <summary>Checks persisted host freshness and reviewed artifact availability.</summary>
    private static ScheduledTaskChange? ValidateCatalog(ScheduledTaskDefinition definition, ScheduledTaskCatalog? catalog, DateTimeOffset now, out ScheduledTaskProject? project)
    {
        project = catalog?.Projects.SingleOrDefault(p => p.TaskKey == definition.Schedule.TaskKey);
        if (catalog is null || catalog.HostId != definition.Schedule.HostId || catalog.Environment != definition.Schedule.Environment || catalog.HostCapability is not { Ready: true } capability || capability.HostId != catalog.HostId || capability.Environment != catalog.Environment || capability.ObservedAtUtc > now || now - capability.ObservedAtUtc > TimeSpan.FromMinutes(2)) return Reject("HOST.UNAVAILABLE", "host capability is missing, stale or not ready");
        if (project is null || !project.Available || string.IsNullOrWhiteSpace(project.ArtifactDigest) || string.IsNullOrWhiteSpace(project.ManifestVersion) || project.Platform != catalog.HostCapability.Platform || definition.Schedule.MaximumRuntimeSeconds > project.MaximumRuntimeSeconds) return Reject("ARTIFACT.UNAVAILABLE", "deployed artifact is unavailable, incompatible or exceeds its runtime bound");
        return null;
    }
    /// <summary>Marks desired configuration pending without claiming runtime installation.</summary>
    private static ScheduledTaskDefinition Desired(ScheduledTaskDefinition definition, string operatorName, Guid operation) => definition with { DesiredRevision = definition.DesiredRevision + 1, InstallationStatus = ScheduledTaskInstallationStatus.Pending, UpdatedBy = operatorName, OperationCommandId = operation };
    /// <summary>Preserves initiating operation identity independently of each receipt command.</summary>
    private static Guid Operation(ICommand<ScheduledTaskId> command) => command switch
    {
        CreateScheduledTaskCommand c => c.OperationCommandId == Guid.Empty ? c.CommandId : c.OperationCommandId,
        ChangeScheduledTaskScheduleCommand c => c.OperationCommandId == Guid.Empty ? c.CommandId : c.OperationCommandId,
        EnableScheduledTaskCommand c => c.OperationCommandId == Guid.Empty ? c.CommandId : c.OperationCommandId,
        DisableScheduledTaskCommand c => c.OperationCommandId == Guid.Empty ? c.CommandId : c.OperationCommandId,
        RemoveScheduledTaskCommand c => c.OperationCommandId == Guid.Empty ? c.CommandId : c.OperationCommandId,
        _ => command.CommandId
    };
    /// <summary>Creates a readable domain-specific business rejection.</summary>
    private static ScheduledTaskChange Reject(string code, string detail) => new(null, $"ScheduledTask.{code};{detail}");
}
