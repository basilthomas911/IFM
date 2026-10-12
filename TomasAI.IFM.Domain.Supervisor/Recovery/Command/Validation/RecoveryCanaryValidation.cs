using TomasAI.IFM.Domain.Supervisor.Shared.Recovery;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Supervisor.Recovery.Command.Validation;

/// <summary>Accumulates deterministic recovery-canary ingress errors.</summary>
public static class RecoveryCanaryValidation
{
    /// <summary>Checks the recovery episode correlation identity.</summary>
    public static TErrors ValidateCanaryCorrelation<TErrors>(
        this TErrors errors, Guid correlationId)

        where TErrors : class, ICollection<ValidationError>
    {
        ArgumentNullException.ThrowIfNull(errors);
        if (correlationId == Guid.Empty)
            errors.Add(new("Recovery canary correlation ID is empty."));
        return errors;
    }

    /// <summary>Checks the isolated publisher generation identity.</summary>
    public static TErrors ValidateCanaryGeneration<TErrors>(
        this TErrors errors, Guid generationId)

        where TErrors : class, ICollection<ValidationError>
    {
        ArgumentNullException.ThrowIfNull(errors);
        if (generationId == Guid.Empty)
            errors.Add(new("Recovery canary generation ID is empty."));
        return errors;
    }

    /// <summary>Checks the market value date.</summary>
    public static TErrors ValidateCanaryValueDate<TErrors>(
        this TErrors errors, DateOnly valueDate)

        where TErrors : class, ICollection<ValidationError>
    {
        ArgumentNullException.ThrowIfNull(errors);
        if (valueDate == default)
            errors.Add(new("Recovery canary value date is empty."));
        return errors;
    }

    /// <summary>Checks the bounded dataset identity.</summary>
    public static TErrors ValidateCanaryDataset<TErrors>(
        this TErrors errors, string? dataset)

        where TErrors : class, ICollection<ValidationError>
    {
        ArgumentNullException.ThrowIfNull(errors);
        if (string.IsNullOrWhiteSpace(dataset) || dataset.Length > 64)
            errors.Add(new("Recovery canary dataset must contain 1 to 64 characters."));
        return errors;
    }

    /// <summary>Checks the issued timestamp when one is supplied.</summary>
    public static TErrors ValidateCanaryIssuedUtc<TErrors>(
        this TErrors errors, DateTime issuedUtc)

        where TErrors : class, ICollection<ValidationError>
    {
        ArgumentNullException.ThrowIfNull(errors);
        if (issuedUtc != default && issuedUtc.Kind != DateTimeKind.Utc)
            errors.Add(new("Recovery canary issued timestamp must be UTC."));
        return errors;
    }

    /// <summary>Cross-checks the singleton canary entity and subject route.</summary>
    public static TErrors ValidateCanaryRoute<TErrors>(
        this TErrors errors, RecoveryCanaryCommand command)

        where TErrors : class, ICollection<ValidationError>
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(errors);
        if (command.EntityId != ActorEntityId.Default
            || !string.Equals(command.Subject.EntityId,command.CorrelationId.ToString("N"),StringComparison.Ordinal))
            errors.Add(new("Recovery canary subject must route by its correlation ID."));
        if (command.RouteTo != BoundedContextName.SupervisorBoundedContext)
            errors.Add(new("Recovery canary must route to the Supervisor bounded context."));
        if (!command.PostEvents)
            errors.Add(new("Recovery canary must enable event projection."));
        if (command.ErrorCode <= 0)
            errors.Add(new("Recovery canary error code must be positive."));
        return errors;
    }
}
