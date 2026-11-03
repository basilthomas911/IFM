using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Catalog.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;
using TomasAI.IFM.Shared.Validation;
namespace TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Validation;

/// <summary>Shares stateless structural checks across scheduled-task aggregate owners.</summary>
internal static class ScheduledTaskPayloadValidation
{
    /// <summary>Validates schedule shape; occurrence availability is checked with the actor clock during computation.</summary>
    internal static void Validate(ICollection<ValidationError> errors, ScheduledTaskSchedule? schedule)
    {
        if (schedule is null) { errors.Add(new("Schedule is required.")); return; }
        Check(errors, !string.IsNullOrWhiteSpace(schedule.TaskKey), "Schedule.TaskKey is required.");
        Check(errors, !string.IsNullOrWhiteSpace(schedule.Name), "Schedule.Name is required.");
        Check(errors, !string.IsNullOrWhiteSpace(schedule.Environment), "Schedule.Environment is required.");
        Check(errors, !string.IsNullOrWhiteSpace(schedule.HostId), "Schedule.HostId is required.");
        Check(errors, Enum.IsDefined(schedule.Timing), "Schedule.Timing is invalid.");
        Check(errors, schedule.Expression is not null && (schedule.Timing != ScheduledTaskTiming.Cron || !string.IsNullOrWhiteSpace(schedule.Expression)), "Schedule.Expression is required for cron timing.");
        Check(errors, !string.IsNullOrWhiteSpace(schedule.TimeZoneId), "Schedule.TimeZoneId is required.");
        Check(errors, schedule.StartsAtUtc is null || schedule.StartsAtUtc.Value != default && schedule.StartsAtUtc.Value.Offset == TimeSpan.Zero, "Schedule.StartsAtUtc must be UTC when supplied.");
        Check(errors, schedule.EndsAtUtc is null || schedule.EndsAtUtc.Value != default && schedule.EndsAtUtc.Value.Offset == TimeSpan.Zero, "Schedule.EndsAtUtc must be UTC when supplied.");
        Check(errors, schedule.Timing != ScheduledTaskTiming.OneTime || schedule.StartsAtUtc is not null, "One-time schedule requires StartsAtUtc.");
        Check(errors, !(schedule.EndsAtUtc <= schedule.StartsAtUtc), "Schedule end must follow start.");
        Check(errors, schedule.MaximumRuntimeSeconds is >= 1 and <= 86400, "Schedule runtime must be 1..86400 seconds.");
        Check(errors, schedule.DispatchToleranceSeconds is >= 1 and <= 3600, "Dispatch tolerance must be 1..3600 seconds.");
        Check(errors, schedule.Description is not null, "Schedule.Description cannot be null.");
    }
    /// <summary>Checks the deployed artifact identity and supported execution bounds without consulting runtime services.</summary>
    internal static void Validate(ICollection<ValidationError> errors, ScheduledTaskProject? project)
    {
        if (project is null) { errors.Add(new("Project is required.")); return; }
        Check(errors, !string.IsNullOrWhiteSpace(project.TaskKey), "Project.TaskKey is required.");
        Check(errors, !string.IsNullOrWhiteSpace(project.ProjectName), "Project.ProjectName is required.");
        Check(errors, !string.IsNullOrWhiteSpace(project.ManifestVersion), "Project.ManifestVersion is required.");
        Check(errors, !string.IsNullOrWhiteSpace(project.ArtifactDigest), "Project.ArtifactDigest is required.");
        Check(errors, project.MaximumRuntimeSeconds is >= 1 and <= 86400, "Project runtime must be 1..86400 seconds.");
        Check(errors, project.DisplayName is not null && project.Platform is not null && project.CompletionProtocol is not null, "Project display, platform and completion protocol cannot be null.");
    }
    /// <summary>Checks a host observation's intrinsic identity and generation; freshness remains a business guard.</summary>
    internal static void Validate(ICollection<ValidationError> errors, ScheduledTaskHostCapability? capability)
    {
        if (capability is null) { errors.Add(new("HostCapability is required.")); return; }
        Check(errors, !string.IsNullOrWhiteSpace(capability.HostId), "HostCapability.HostId is required.");
        Check(errors, !string.IsNullOrWhiteSpace(capability.Environment), "HostCapability.Environment is required.");
        Check(errors, !string.IsNullOrWhiteSpace(capability.Platform), "HostCapability.Platform is required.");
        Check(errors, capability.Generation > 0, "HostCapability.Generation must be positive.");
        Check(errors, capability.ObservedAtUtc != default && capability.ObservedAtUtc.Offset == TimeSpan.Zero, "Host observation must be a nondefault UTC instant.");
        Check(errors, capability.Detail is not null, "HostCapability.Detail cannot be null.");
    }
    /// <summary>Adds an error without interrupting validation of subsequent fields.</summary>
    internal static void Check(ICollection<ValidationError> errors, bool valid, string message)
    {
        if (!valid) errors.Add(new(message));
    }
}
