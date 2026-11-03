using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Run.Commands;
using static TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Validation.ScheduledTaskPayloadValidation;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Commands;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;
using TomasAI.IFM.Shared.Validation;
namespace TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Run.Command.Validation;

/// <summary>Validates payloads for this single command-state owner before loading state.</summary>
public static class ScheduledTaskRunCommandValidation
{
    /// <summary>Validates the CompleteScheduledTaskRun payload, retaining optional correlation and diagnostic fields.</summary>
    public static TErrors ValidateScheduledTaskCommand<TErrors>(this TErrors errors, CompleteScheduledTaskRunCommand command)
        where TErrors : class, ICollection<ValidationError>
    {
        ArgumentNullException.ThrowIfNull(errors);
        ArgumentNullException.ThrowIfNull(command);
        Check(errors, command.Subject.ActorType == ActorType.Command &&
            string.Equals(command.Subject.Name, CompleteScheduledTaskRunCommand.Actor, StringComparison.Ordinal) &&
            string.Equals(command.Subject.Verb, CompleteScheduledTaskRunCommand.Verb, StringComparison.Ordinal) &&
            string.Equals(command.Subject.EntityId, command.EntityId.Format(), StringComparison.Ordinal), "Subject must match the command actor, verb and aggregate identity.");
        Check(errors, command.ExpectedRevision >= 0, "ExpectedRevision cannot be negative.");
        Check(errors, command.Operator is not null, "Operator cannot be null.");
        Check(errors, command.Reason is not null, "Reason cannot be null.");
        Check(errors, command.FinishedAtUtc != default && command.FinishedAtUtc.Offset == TimeSpan.Zero, "FinishedAtUtc must be a nondefault UTC instant.");
        Check(errors, command.ExitCode == 0, "Successful completion requires exit code zero.");
        Check(errors, command.ValueDate is null || command.ValueDate != default(DateOnly), "ValueDate cannot be the default date.");
        Check(errors, !string.IsNullOrWhiteSpace(command.Stage), "Stage is required.");
        Check(errors, command.Detail is not null, "Detail cannot be null.");
        Check(errors, command.StandardOutputTail is not null, "StandardOutputTail cannot be null.");
        Check(errors, command.StandardErrorTail is not null, "StandardErrorTail cannot be null.");
        Check(errors, command.OutputDirectory is not null, "OutputDirectory cannot be null.");
        return errors;
    }

    /// <summary>Validates the FailScheduledTaskRun payload, retaining optional correlation and diagnostic fields.</summary>
    public static TErrors ValidateScheduledTaskCommand<TErrors>(this TErrors errors, FailScheduledTaskRunCommand command)
        where TErrors : class, ICollection<ValidationError>
    {
        ArgumentNullException.ThrowIfNull(errors);
        ArgumentNullException.ThrowIfNull(command);
        Check(errors, command.Subject.ActorType == ActorType.Command &&
            string.Equals(command.Subject.Name, FailScheduledTaskRunCommand.Actor, StringComparison.Ordinal) &&
            string.Equals(command.Subject.Verb, FailScheduledTaskRunCommand.Verb, StringComparison.Ordinal) &&
            string.Equals(command.Subject.EntityId, command.EntityId.Format(), StringComparison.Ordinal), "Subject must match the command actor, verb and aggregate identity.");
        Check(errors, command.ExpectedRevision >= 0, "ExpectedRevision cannot be negative.");
        Check(errors, command.Operator is not null, "Operator cannot be null.");
        Check(errors, command.Reason is not null, "Reason cannot be null.");
        Check(errors, command.FinishedAtUtc != default && command.FinishedAtUtc.Offset == TimeSpan.Zero, "FinishedAtUtc must be a nondefault UTC instant.");
        Check(errors, command.Stage is not null, "Stage cannot be null.");
        Check(errors, command.Detail is not null, "Detail cannot be null.");
        Check(errors, command.StandardOutputTail is not null, "StandardOutputTail cannot be null.");
        Check(errors, command.StandardErrorTail is not null, "StandardErrorTail cannot be null.");
        Check(errors, command.OutputDirectory is not null, "OutputDirectory cannot be null.");
        return errors;
    }

    /// <summary>Validates the RecordScheduledTaskRunAdmission payload, retaining optional correlation and diagnostic fields.</summary>
    public static TErrors ValidateScheduledTaskCommand<TErrors>(this TErrors errors, RecordScheduledTaskRunAdmissionCommand command)
        where TErrors : class, ICollection<ValidationError>
    {
        ArgumentNullException.ThrowIfNull(errors);
        ArgumentNullException.ThrowIfNull(command);
        Check(errors, command.Subject.ActorType == ActorType.Command &&
            string.Equals(command.Subject.Name, RecordScheduledTaskRunAdmissionCommand.Actor, StringComparison.Ordinal) &&
            string.Equals(command.Subject.Verb, RecordScheduledTaskRunAdmissionCommand.Verb, StringComparison.Ordinal) &&
            string.Equals(command.Subject.EntityId, command.EntityId.Format(), StringComparison.Ordinal), "Subject must match the command actor, verb and aggregate identity.");
        Check(errors, command.ExpectedRevision >= 0, "ExpectedRevision cannot be negative.");
        Check(errors, command.Operator is not null, "Operator cannot be null.");
        Check(errors, command.Reason is not null, "Reason cannot be null.");
        Check(errors, command.Detail is not null, "Detail cannot be null.");
        return errors;
    }

    /// <summary>Validates the RecordScheduledTaskRunStage payload, retaining optional correlation and diagnostic fields.</summary>
    public static TErrors ValidateScheduledTaskCommand<TErrors>(this TErrors errors, RecordScheduledTaskRunStageCommand command)
        where TErrors : class, ICollection<ValidationError>
    {
        ArgumentNullException.ThrowIfNull(errors);
        ArgumentNullException.ThrowIfNull(command);
        Check(errors, command.Subject.ActorType == ActorType.Command &&
            string.Equals(command.Subject.Name, RecordScheduledTaskRunStageCommand.Actor, StringComparison.Ordinal) &&
            string.Equals(command.Subject.Verb, RecordScheduledTaskRunStageCommand.Verb, StringComparison.Ordinal) &&
            string.Equals(command.Subject.EntityId, command.EntityId.Format(), StringComparison.Ordinal), "Subject must match the command actor, verb and aggregate identity.");
        Check(errors, command.ExpectedRevision >= 0, "ExpectedRevision cannot be negative.");
        Check(errors, command.Operator is not null, "Operator cannot be null.");
        Check(errors, command.Reason is not null, "Reason cannot be null.");
        Check(errors, !string.IsNullOrWhiteSpace(command.Stage), "Stage is required.");
        Check(errors, command.ValueDate != default, "ValueDate is required.");
        Check(errors, command.Detail is not null, "Detail cannot be null.");
        return errors;
    }

    /// <summary>Validates the RecordScheduledTaskRunStarted payload, retaining optional correlation and diagnostic fields.</summary>
    public static TErrors ValidateScheduledTaskCommand<TErrors>(this TErrors errors, RecordScheduledTaskRunStartedCommand command)
        where TErrors : class, ICollection<ValidationError>
    {
        ArgumentNullException.ThrowIfNull(errors);
        ArgumentNullException.ThrowIfNull(command);
        Check(errors, command.Subject.ActorType == ActorType.Command &&
            string.Equals(command.Subject.Name, RecordScheduledTaskRunStartedCommand.Actor, StringComparison.Ordinal) &&
            string.Equals(command.Subject.Verb, RecordScheduledTaskRunStartedCommand.Verb, StringComparison.Ordinal) &&
            string.Equals(command.Subject.EntityId, command.EntityId.Format(), StringComparison.Ordinal), "Subject must match the command actor, verb and aggregate identity.");
        Check(errors, command.ExpectedRevision >= 0, "ExpectedRevision cannot be negative.");
        Check(errors, command.Operator is not null, "Operator cannot be null.");
        Check(errors, command.Reason is not null, "Reason cannot be null.");
        Check(errors, command.ProcessId > 0, "ProcessId must be positive.");
        Check(errors, command.StartedAtUtc != default && command.StartedAtUtc.Offset == TimeSpan.Zero, "StartedAtUtc must be a nondefault UTC instant.");
        return errors;
    }

    /// <summary>Validates the RecordScheduledTaskRunUncertain payload, retaining optional correlation and diagnostic fields.</summary>
    public static TErrors ValidateScheduledTaskCommand<TErrors>(this TErrors errors, RecordScheduledTaskRunUncertainCommand command)
        where TErrors : class, ICollection<ValidationError>
    {
        ArgumentNullException.ThrowIfNull(errors);
        ArgumentNullException.ThrowIfNull(command);
        Check(errors, command.Subject.ActorType == ActorType.Command &&
            string.Equals(command.Subject.Name, RecordScheduledTaskRunUncertainCommand.Actor, StringComparison.Ordinal) &&
            string.Equals(command.Subject.Verb, RecordScheduledTaskRunUncertainCommand.Verb, StringComparison.Ordinal) &&
            string.Equals(command.Subject.EntityId, command.EntityId.Format(), StringComparison.Ordinal), "Subject must match the command actor, verb and aggregate identity.");
        Check(errors, command.ExpectedRevision >= 0, "ExpectedRevision cannot be negative.");
        Check(errors, command.Operator is not null, "Operator cannot be null.");
        Check(errors, command.Reason is not null, "Reason cannot be null.");
        Check(errors, command.FinishedAtUtc != default && command.FinishedAtUtc.Offset == TimeSpan.Zero, "FinishedAtUtc must be a nondefault UTC instant.");
        Check(errors, command.Detail is not null, "Detail cannot be null.");
        Check(errors, command.StandardOutputTail is not null, "StandardOutputTail cannot be null.");
        Check(errors, command.StandardErrorTail is not null, "StandardErrorTail cannot be null.");
        Check(errors, command.OutputDirectory is not null, "OutputDirectory cannot be null.");
        return errors;
    }

    /// <summary>Validates the RequestScheduledTaskRun payload, retaining optional correlation and diagnostic fields.</summary>
    public static TErrors ValidateScheduledTaskCommand<TErrors>(this TErrors errors, RequestScheduledTaskRunCommand command)
        where TErrors : class, ICollection<ValidationError>
    {
        ArgumentNullException.ThrowIfNull(errors);
        ArgumentNullException.ThrowIfNull(command);
        Check(errors, command.Subject.ActorType == ActorType.Command &&
            string.Equals(command.Subject.Name, RequestScheduledTaskRunCommand.Actor, StringComparison.Ordinal) &&
            string.Equals(command.Subject.Verb, RequestScheduledTaskRunCommand.Verb, StringComparison.Ordinal) &&
            string.Equals(command.Subject.EntityId, command.EntityId.Format(), StringComparison.Ordinal), "Subject must match the command actor, verb and aggregate identity.");
        Check(errors, command.ExpectedRevision >= 0, "ExpectedRevision cannot be negative.");
        Check(errors, command.Operator is not null, "Operator cannot be null.");
        Check(errors, command.Reason is not null, "Reason cannot be null.");
        errors.ValidateScheduledTaskId(command.ScheduleId, command.CommandName);
        Check(errors, command.DefinitionRevision >= 0, "DefinitionRevision cannot be negative.");
        Check(errors, command.IntendedFireTimeUtc != default && command.IntendedFireTimeUtc.Offset == TimeSpan.Zero, "IntendedFireTimeUtc must be a nondefault UTC instant.");
        Check(errors, !string.IsNullOrWhiteSpace(command.TaskKey), "TaskKey is required.");
        Check(errors, !string.IsNullOrWhiteSpace(command.HostId), "HostId is required.");
        Check(errors, !string.IsNullOrWhiteSpace(command.Environment), "Environment is required.");
        return errors;
    }
}
