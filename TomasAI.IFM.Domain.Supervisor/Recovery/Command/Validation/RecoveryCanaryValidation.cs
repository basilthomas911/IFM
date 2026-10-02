using TomasAI.IFM.Domain.Supervisor.Shared.Recovery;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Supervisor.Recovery.Command.Validation;

/// <summary>Accumulates deterministic recovery-canary ingress errors.</summary>
public static class RecoveryCanaryValidation
{
    /// <summary>Checks the recovery episode correlation identity.</summary>
    public static List<ValidationError> ValidateCanaryCorrelation(
        this List<ValidationError> errors, Guid correlationId)
    {
        if (correlationId == Guid.Empty)
            errors.Add(new("Recovery canary correlation ID is empty."));
        return errors;
    }

    /// <summary>Checks the isolated publisher generation identity.</summary>
    public static List<ValidationError> ValidateCanaryGeneration(
        this List<ValidationError> errors, Guid generationId)
    {
        if (generationId == Guid.Empty)
            errors.Add(new("Recovery canary generation ID is empty."));
        return errors;
    }

    /// <summary>Checks the market value date.</summary>
    public static List<ValidationError> ValidateCanaryValueDate(
        this List<ValidationError> errors, DateOnly valueDate)
    {
        if (valueDate == default)
            errors.Add(new("Recovery canary value date is empty."));
        return errors;
    }

    /// <summary>Checks the bounded dataset identity.</summary>
    public static List<ValidationError> ValidateCanaryDataset(
        this List<ValidationError> errors, string? dataset)
    {
        if (string.IsNullOrWhiteSpace(dataset) || dataset.Length > 64)
            errors.Add(new("Recovery canary dataset must contain 1 to 64 characters."));
        return errors;
    }

    /// <summary>Checks the issued timestamp when one is supplied.</summary>
    public static List<ValidationError> ValidateCanaryIssuedUtc(
        this List<ValidationError> errors, DateTime issuedUtc)
    {
        if (issuedUtc != default && issuedUtc.Kind != DateTimeKind.Utc)
            errors.Add(new("Recovery canary issued timestamp must be UTC."));
        return errors;
    }

    /// <summary>Cross-checks the singleton canary entity and subject route.</summary>
    public static List<ValidationError> ValidateCanaryRoute(
        this List<ValidationError> errors, RecoveryCanaryCommand command)
    {
        if (command.EntityId != ActorEntityId.Default
            || command.Subject.EntityId != command.CorrelationId.ToString("N"))
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
