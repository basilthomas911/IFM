using TomasAI.IFM.Domain.Supervisor.Shared.Operations.Commands;
using TomasAI.IFM.Domain.Supervisor.Shared.Enums;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Supervisor.Operations.Command.Validation;

/// <summary>Accumulates independent, state-free Supervisor command payload errors.</summary>
public static class SupervisorOperationValidation
{
    /// <summary>Validates the target actor thread identity.</summary>
    public static TErrors ValidateSupervisorTarget<TErrors>(
        this TErrors errors, ActorThreadId target, string commandName)

        where TErrors : class, ICollection<ValidationError>
    {
        ArgumentNullException.ThrowIfNull(errors);
        if (string.IsNullOrWhiteSpace(target.Name) || string.IsNullOrWhiteSpace(target.EntityId))
            errors.Add(new($"{commandName}.Target must identify an actor name and entity."));
        return errors;
    }

    /// <summary>Validates generation-fence input for the requested operation.</summary>
    public static TErrors ValidateSupervisorGeneration<TErrors>(
        this TErrors errors, long generation,
        SupervisorActorOperationKind operation, string commandName)

        where TErrors : class, ICollection<ValidationError>
    {
        ArgumentNullException.ThrowIfNull(errors);
        if (generation < 0 ||
            (generation == 0 && operation is SupervisorActorOperationKind.Resume
                or SupervisorActorOperationKind.Restart or SupervisorActorOperationKind.Recycle
                or SupervisorActorOperationKind.Retire))
            errors.Add(new($"{commandName}.ExpectedGeneration must be positive for a fenced operation."));
        return errors;
    }

    /// <summary>Validates a bounded non-empty operator identity.</summary>
    public static TErrors ValidateSupervisorRequester<TErrors>(
        this TErrors errors, string? requester, string commandName)

        where TErrors : class, ICollection<ValidationError>
    {
        ArgumentNullException.ThrowIfNull(errors);
        if (string.IsNullOrWhiteSpace(requester) || requester.Length > 128)
            errors.Add(new($"{commandName}.Requester must contain 1 to 128 characters."));
        return errors;
    }

    /// <summary>Validates a bounded non-empty operator reason.</summary>
    public static TErrors ValidateSupervisorReason<TErrors>(
        this TErrors errors, string? reason, string commandName)

        where TErrors : class, ICollection<ValidationError>
    {
        ArgumentNullException.ThrowIfNull(errors);
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 1024)
            errors.Add(new($"{commandName}.Reason must contain 1 to 1024 characters."));
        return errors;
    }

    /// <summary>Validates the bounded lifecycle deadline.</summary>
    public static TErrors ValidateSupervisorTimeout<TErrors>(
        this TErrors errors, long timeoutTicks, string commandName)

        where TErrors : class, ICollection<ValidationError>
    {
        ArgumentNullException.ThrowIfNull(errors);
        if (timeoutTicks <= 0 || timeoutTicks > TimeSpan.FromMinutes(10).Ticks)
            errors.Add(new($"{commandName}.Timeout must be greater than zero and at most ten minutes."));
        return errors;
    }

    /// <summary>Checks that the singleton Supervisor entity and routed subject agree.</summary>
    public static TErrors ValidateSupervisorRoute<TErrors>(
        this TErrors errors, ISupervisorOperationCommand command)

        where TErrors : class, ICollection<ValidationError>
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(errors);
        if (command.EntityId != ActorEntityId.Default
            || !string.Equals(command.Subject.EntityId,command.EntityId.Format(),StringComparison.Ordinal))
            errors.Add(new($"{command.CommandName}.EntityId must match the Supervisor singleton route."));
        if (command.RouteTo != BoundedContextName.SupervisorBoundedContext)
            errors.Add(new($"{command.CommandName}.RouteTo must identify the Supervisor bounded context."));
        if (!command.PostEvents)
            errors.Add(new($"{command.CommandName}.PostEvents must be enabled for outcome projection."));
        if (command.ErrorCode <= 0)
            errors.Add(new($"{command.CommandName}.ErrorCode must be positive."));
        return errors;
    }
}
