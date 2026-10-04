using FluentValidation;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Trade.Shared.Order.Broker;

/// <summary>Structural broker observation validation beside the shared evidence contract.</summary>
public static class BrokerObservationValidation
{
    private static readonly ObservationRules Rules = new();

    /// <summary>Appends every sourced observation failure without throwing on missing payloads.</summary>
    public static List<ValidationError> ValidateBrokerObservation(this List<ValidationError> errors, BrokerOrderObservationEvidence? observation)
    {
        if (observation is null) errors.Add(new("BrokerOrder.Observation is required."));
        else errors.AddRange(Rules.Validate(observation).Errors.Select(static failure => new ValidationError(failure.ErrorMessage)));
        return errors;
    }

    private sealed class ObservationRules : AbstractValidator<BrokerOrderObservationEvidence>
    {
        public ObservationRules()
        {
            RuleFor(value => value.ObservationId).NotEmpty();
            RuleFor(value => value.Kind).IsInEnum().NotEqual(BrokerOrderObservationKind.Unknown);
            RuleFor(value => value.AccountAlias).NotEmpty();
            // OperationId may be absent for unsolicited observations; exact correlation is checked by the handler.
            RuleFor(value => value.ComponentId).NotEmpty();
            RuleFor(value => value.ContractId).NotNull();
            RuleFor(value => value.ExternalExecutionId).NotNull();
            RuleFor(value => value.OrderRevision).GreaterThanOrEqualTo(0);
            RuleFor(value => value.SourceEpoch).GreaterThan(0);
            RuleFor(value => value.SourceSequence).GreaterThan(0);
            RuleFor(value => value.OccurredAtUtc).Must(time => time.Kind == DateTimeKind.Utc);
            RuleFor(value => value.Category).NotNull();
            RuleFor(value => value.Detail).NotNull();
            RuleFor(value => value.ContentHash).NotEmpty();
            When(value => value.Kind == BrokerOrderObservationKind.Execution, () =>
            {
                RuleFor(value => value.LegId).NotEmpty();
                RuleFor(value => value.ContractId).NotEmpty();
                RuleFor(value => value.ExternalExecutionId).NotEmpty();
                RuleFor(value => value.SignedQuantity).NotEqual(0);
                RuleFor(value => value.Price).GreaterThan(0);
            });
            When(value => value.Kind == BrokerOrderObservationKind.Commission, () =>
            {
                RuleFor(value => value.ExternalExecutionId).NotEmpty();
                RuleFor(value => value.Commission).GreaterThanOrEqualTo(0);
            });
            // Fields irrelevant to the observation kind retain their full serialized ranges.
        }
    }
}
