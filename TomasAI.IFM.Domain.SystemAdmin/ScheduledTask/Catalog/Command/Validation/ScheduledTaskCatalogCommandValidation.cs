using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Catalog.Commands;
using static TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Validation.ScheduledTaskPayloadValidation;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Commands;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;
using TomasAI.IFM.Shared.Validation;
namespace TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Catalog.Command.Validation;

/// <summary>Validates payloads for this single command-state owner before loading state.</summary>
public static class ScheduledTaskCatalogCommandValidation
{
    /// <summary>Validates the RecordScheduledTaskHostCapability payload, retaining optional correlation and diagnostic fields.</summary>
    public static TErrors ValidateScheduledTaskCommand<TErrors>(this TErrors errors, RecordScheduledTaskHostCapabilityCommand command)
        where TErrors : class, ICollection<ValidationError>
    {
        ArgumentNullException.ThrowIfNull(errors);
        ArgumentNullException.ThrowIfNull(command);
        Check(errors, command.Subject.ActorType == ActorType.Command &&
            string.Equals(command.Subject.Name, RecordScheduledTaskHostCapabilityCommand.Actor, StringComparison.Ordinal) &&
            string.Equals(command.Subject.Verb, RecordScheduledTaskHostCapabilityCommand.Verb, StringComparison.Ordinal) &&
            string.Equals(command.Subject.EntityId, command.EntityId.Format(), StringComparison.Ordinal), "Subject must match the command actor, verb and aggregate identity.");
        Check(errors, command.ExpectedRevision >= 0, "ExpectedRevision cannot be negative.");
        Check(errors, command.Operator is not null, "Operator cannot be null.");
        Check(errors, command.Reason is not null, "Reason cannot be null.");
        Validate(errors, command.HostCapability);
        return errors;
    }

    /// <summary>Validates the RegisterScheduledTaskProject payload, retaining optional correlation and diagnostic fields.</summary>
    public static TErrors ValidateScheduledTaskCommand<TErrors>(this TErrors errors, RegisterScheduledTaskProjectCommand command)
        where TErrors : class, ICollection<ValidationError>
    {
        ArgumentNullException.ThrowIfNull(errors);
        ArgumentNullException.ThrowIfNull(command);
        Check(errors, command.Subject.ActorType == ActorType.Command &&
            string.Equals(command.Subject.Name, RegisterScheduledTaskProjectCommand.Actor, StringComparison.Ordinal) &&
            string.Equals(command.Subject.Verb, RegisterScheduledTaskProjectCommand.Verb, StringComparison.Ordinal) &&
            string.Equals(command.Subject.EntityId, command.EntityId.Format(), StringComparison.Ordinal), "Subject must match the command actor, verb and aggregate identity.");
        Check(errors, command.ExpectedRevision >= 0, "ExpectedRevision cannot be negative.");
        Check(errors, command.Operator is not null, "Operator cannot be null.");
        Check(errors, command.Reason is not null, "Reason cannot be null.");
        Check(errors, !string.IsNullOrWhiteSpace(command.HostId), "HostId is required.");
        Check(errors, !string.IsNullOrWhiteSpace(command.Environment), "Environment is required.");
        Validate(errors, command.Project);
        return errors;
    }
}
