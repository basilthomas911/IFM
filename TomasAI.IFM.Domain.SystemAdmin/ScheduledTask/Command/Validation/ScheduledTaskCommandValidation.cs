using static TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Validation.ScheduledTaskPayloadValidation;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Commands;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;
using TomasAI.IFM.Shared.Validation;
namespace TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Command.Validation;

/// <summary>Validates payloads for this single command-state owner before loading state.</summary>
public static class ScheduledTaskCommandValidation
{
    /// <summary>Validates the AdmitScheduledTaskRun payload, retaining optional correlation and diagnostic fields.</summary>
    public static TErrors ValidateScheduledTaskCommand<TErrors>(this TErrors errors, AdmitScheduledTaskRunCommand command)
        where TErrors : class, ICollection<ValidationError>
    {
        ArgumentNullException.ThrowIfNull(errors);
        ArgumentNullException.ThrowIfNull(command);
        Check(errors, command.Subject.ActorType == ActorType.Command &&
            string.Equals(command.Subject.Name, AdmitScheduledTaskRunCommand.Actor, StringComparison.Ordinal) &&
            string.Equals(command.Subject.Verb, AdmitScheduledTaskRunCommand.Verb, StringComparison.Ordinal) &&
            string.Equals(command.Subject.EntityId, command.EntityId.Format(), StringComparison.Ordinal), "Subject must match the command actor, verb and aggregate identity.");
        Check(errors, command.ExpectedRevision >= 0, "ExpectedRevision cannot be negative.");
        Check(errors, command.Operator is not null, "Operator cannot be null.");
        Check(errors, command.Reason is not null, "Reason cannot be null.");
        Check(errors, command.RunId != Guid.Empty, "RunId is required.");
        Check(errors, command.DefinitionRevision >= 0, "DefinitionRevision cannot be negative.");
        Check(errors, command.IntendedFireTimeUtc != default && command.IntendedFireTimeUtc.Offset == TimeSpan.Zero, "IntendedFireTimeUtc must be a nondefault UTC instant.");
        Check(errors, !string.IsNullOrWhiteSpace(command.HostId), "HostId is required.");
        Check(errors, !string.IsNullOrWhiteSpace(command.Environment), "Environment is required.");
        return errors;
    }

    /// <summary>Validates the ChangeScheduledTaskSchedule payload, retaining optional correlation and diagnostic fields.</summary>
    public static TErrors ValidateScheduledTaskCommand<TErrors>(this TErrors errors, ChangeScheduledTaskScheduleCommand command)
        where TErrors : class, ICollection<ValidationError>
    {
        ArgumentNullException.ThrowIfNull(errors);
        ArgumentNullException.ThrowIfNull(command);
        Check(errors, command.Subject.ActorType == ActorType.Command &&
            string.Equals(command.Subject.Name, ChangeScheduledTaskScheduleCommand.Actor, StringComparison.Ordinal) &&
            string.Equals(command.Subject.Verb, ChangeScheduledTaskScheduleCommand.Verb, StringComparison.Ordinal) &&
            string.Equals(command.Subject.EntityId, command.EntityId.Format(), StringComparison.Ordinal), "Subject must match the command actor, verb and aggregate identity.");
        Check(errors, command.ExpectedRevision >= 0, "ExpectedRevision cannot be negative.");
        Check(errors, command.Operator is not null, "Operator cannot be null.");
        Check(errors, command.Reason is not null, "Reason cannot be null.");
        Validate(errors, command.Schedule);
        return errors;
    }

    /// <summary>Validates the CreateScheduledTask payload, retaining optional correlation and diagnostic fields.</summary>
    public static TErrors ValidateScheduledTaskCommand<TErrors>(this TErrors errors, CreateScheduledTaskCommand command)
        where TErrors : class, ICollection<ValidationError>
    {
        ArgumentNullException.ThrowIfNull(errors);
        ArgumentNullException.ThrowIfNull(command);
        Check(errors, command.Subject.ActorType == ActorType.Command &&
            string.Equals(command.Subject.Name, CreateScheduledTaskCommand.Actor, StringComparison.Ordinal) &&
            string.Equals(command.Subject.Verb, CreateScheduledTaskCommand.Verb, StringComparison.Ordinal) &&
            string.Equals(command.Subject.EntityId, command.EntityId.Format(), StringComparison.Ordinal), "Subject must match the command actor, verb and aggregate identity.");
        Check(errors, command.ExpectedRevision >= 0, "ExpectedRevision cannot be negative.");
        Check(errors, command.Operator is not null, "Operator cannot be null.");
        Check(errors, command.Reason is not null, "Reason cannot be null.");
        Validate(errors, command.Schedule);
        return errors;
    }

    /// <summary>Validates the DisableScheduledTask payload, retaining optional correlation and diagnostic fields.</summary>
    public static TErrors ValidateScheduledTaskCommand<TErrors>(this TErrors errors, DisableScheduledTaskCommand command)
        where TErrors : class, ICollection<ValidationError>
    {
        ArgumentNullException.ThrowIfNull(errors);
        ArgumentNullException.ThrowIfNull(command);
        Check(errors, command.Subject.ActorType == ActorType.Command &&
            string.Equals(command.Subject.Name, DisableScheduledTaskCommand.Actor, StringComparison.Ordinal) &&
            string.Equals(command.Subject.Verb, DisableScheduledTaskCommand.Verb, StringComparison.Ordinal) &&
            string.Equals(command.Subject.EntityId, command.EntityId.Format(), StringComparison.Ordinal), "Subject must match the command actor, verb and aggregate identity.");
        Check(errors, command.ExpectedRevision >= 0, "ExpectedRevision cannot be negative.");
        Check(errors, command.Operator is not null, "Operator cannot be null.");
        Check(errors, command.Reason is not null, "Reason cannot be null.");
        return errors;
    }

    /// <summary>Validates the EnableScheduledTask payload, retaining optional correlation and diagnostic fields.</summary>
    public static TErrors ValidateScheduledTaskCommand<TErrors>(this TErrors errors, EnableScheduledTaskCommand command)
        where TErrors : class, ICollection<ValidationError>
    {
        ArgumentNullException.ThrowIfNull(errors);
        ArgumentNullException.ThrowIfNull(command);
        Check(errors, command.Subject.ActorType == ActorType.Command &&
            string.Equals(command.Subject.Name, EnableScheduledTaskCommand.Actor, StringComparison.Ordinal) &&
            string.Equals(command.Subject.Verb, EnableScheduledTaskCommand.Verb, StringComparison.Ordinal) &&
            string.Equals(command.Subject.EntityId, command.EntityId.Format(), StringComparison.Ordinal), "Subject must match the command actor, verb and aggregate identity.");
        Check(errors, command.ExpectedRevision >= 0, "ExpectedRevision cannot be negative.");
        Check(errors, command.Operator is not null, "Operator cannot be null.");
        Check(errors, command.Reason is not null, "Reason cannot be null.");
        return errors;
    }

    /// <summary>Validates the RecordScheduledTaskInstallation payload, retaining optional correlation and diagnostic fields.</summary>
    public static TErrors ValidateScheduledTaskCommand<TErrors>(this TErrors errors, RecordScheduledTaskInstallationCommand command)
        where TErrors : class, ICollection<ValidationError>
    {
        ArgumentNullException.ThrowIfNull(errors);
        ArgumentNullException.ThrowIfNull(command);
        Check(errors, command.Subject.ActorType == ActorType.Command &&
            string.Equals(command.Subject.Name, RecordScheduledTaskInstallationCommand.Actor, StringComparison.Ordinal) &&
            string.Equals(command.Subject.Verb, RecordScheduledTaskInstallationCommand.Verb, StringComparison.Ordinal) &&
            string.Equals(command.Subject.EntityId, command.EntityId.Format(), StringComparison.Ordinal), "Subject must match the command actor, verb and aggregate identity.");
        Check(errors, command.ExpectedRevision >= 0, "ExpectedRevision cannot be negative.");
        Check(errors, command.Operator is not null, "Operator cannot be null.");
        Check(errors, command.Reason is not null, "Reason cannot be null.");
        Check(errors, command.DesiredRevision > 0, "DesiredRevision must be positive.");
        Check(errors, !string.IsNullOrWhiteSpace(command.Fingerprint), "Fingerprint is required.");
        Check(errors, command.Detail is not null, "Detail cannot be null.");
        return errors;
    }

    /// <summary>Validates the RecordScheduledTaskInstallationFailure payload, retaining optional correlation and diagnostic fields.</summary>
    public static TErrors ValidateScheduledTaskCommand<TErrors>(this TErrors errors, RecordScheduledTaskInstallationFailureCommand command)
        where TErrors : class, ICollection<ValidationError>
    {
        ArgumentNullException.ThrowIfNull(errors);
        ArgumentNullException.ThrowIfNull(command);
        Check(errors, command.Subject.ActorType == ActorType.Command &&
            string.Equals(command.Subject.Name, RecordScheduledTaskInstallationFailureCommand.Actor, StringComparison.Ordinal) &&
            string.Equals(command.Subject.Verb, RecordScheduledTaskInstallationFailureCommand.Verb, StringComparison.Ordinal) &&
            string.Equals(command.Subject.EntityId, command.EntityId.Format(), StringComparison.Ordinal), "Subject must match the command actor, verb and aggregate identity.");
        Check(errors, command.ExpectedRevision >= 0, "ExpectedRevision cannot be negative.");
        Check(errors, command.Operator is not null, "Operator cannot be null.");
        Check(errors, command.Reason is not null, "Reason cannot be null.");
        Check(errors, command.DesiredRevision > 0, "DesiredRevision must be positive.");
        Check(errors, command.Detail is not null, "Detail cannot be null.");
        return errors;
    }

    /// <summary>Validates the RecordScheduledTaskRunCompletion payload, retaining optional correlation and diagnostic fields.</summary>
    public static TErrors ValidateScheduledTaskCommand<TErrors>(this TErrors errors, RecordScheduledTaskRunCompletionCommand command)
        where TErrors : class, ICollection<ValidationError>
    {
        ArgumentNullException.ThrowIfNull(errors);
        ArgumentNullException.ThrowIfNull(command);
        Check(errors, command.Subject.ActorType == ActorType.Command &&
            string.Equals(command.Subject.Name, RecordScheduledTaskRunCompletionCommand.Actor, StringComparison.Ordinal) &&
            string.Equals(command.Subject.Verb, RecordScheduledTaskRunCompletionCommand.Verb, StringComparison.Ordinal) &&
            string.Equals(command.Subject.EntityId, command.EntityId.Format(), StringComparison.Ordinal), "Subject must match the command actor, verb and aggregate identity.");
        Check(errors, command.ExpectedRevision >= 0, "ExpectedRevision cannot be negative.");
        Check(errors, command.Operator is not null, "Operator cannot be null.");
        Check(errors, command.Reason is not null, "Reason cannot be null.");
        Check(errors, command.RunId != Guid.Empty, "RunId is required.");
        return errors;
    }

    /// <summary>Validates the RemoveScheduledTask payload, retaining optional correlation and diagnostic fields.</summary>
    public static TErrors ValidateScheduledTaskCommand<TErrors>(this TErrors errors, RemoveScheduledTaskCommand command)
        where TErrors : class, ICollection<ValidationError>
    {
        ArgumentNullException.ThrowIfNull(errors);
        ArgumentNullException.ThrowIfNull(command);
        Check(errors, command.Subject.ActorType == ActorType.Command &&
            string.Equals(command.Subject.Name, RemoveScheduledTaskCommand.Actor, StringComparison.Ordinal) &&
            string.Equals(command.Subject.Verb, RemoveScheduledTaskCommand.Verb, StringComparison.Ordinal) &&
            string.Equals(command.Subject.EntityId, command.EntityId.Format(), StringComparison.Ordinal), "Subject must match the command actor, verb and aggregate identity.");
        Check(errors, command.ExpectedRevision >= 0, "ExpectedRevision cannot be negative.");
        Check(errors, command.Operator is not null, "Operator cannot be null.");
        Check(errors, command.Reason is not null, "Reason cannot be null.");
        return errors;
    }
}
