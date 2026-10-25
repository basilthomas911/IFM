using FluentValidation;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.BrokerAccount.Contracts;

/// <summary>Structural snapshot rules shared by account command boundaries.</summary>
public static class BrokerAccountSnapshotValidation
{
    private static readonly SnapshotRules Rules = new();

    /// <summary>Appends all snapshot failures, including missing position collections.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1002", Justification = "Preserve the published broker message storage width and collection contract.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0016", Justification = "Preserve the published broker message storage width and collection contract.")]
    public static TErrors ValidateAccountSnapshot<TErrors>(this TErrors errors, BrokerAccountSnapshotEvidence? snapshot)

        where TErrors : class, ICollection<ValidationError>
    {
        ArgumentNullException.ThrowIfNull(errors);
        if (snapshot is null) errors.Add(new("BrokerAccount.Snapshot is required."));
        else
            foreach (var failure in Rules.Validate(snapshot).Errors)
                errors.Add(new ValidationError(failure.ErrorMessage));
        return errors;
    }

    private sealed class SnapshotRules : AbstractValidator<BrokerAccountSnapshotEvidence>
    {
        public SnapshotRules()
        {
            RuleFor(snapshot => snapshot.AccountAlias).NotEmpty();
            RuleFor(snapshot => snapshot.Currency).NotEmpty();
            // Signed cash, available funds, Complete and NewRiskAllowed retain their complete valid ranges.
            RuleFor(snapshot => snapshot.Generation).GreaterThan(0);
            RuleFor(snapshot => snapshot.AsOfUtc).Must(time => time.Kind == DateTimeKind.Utc);
            RuleFor(snapshot => snapshot.Positions).NotNull();
            When(snapshot => snapshot.Positions is not null, () =>
                RuleForEach(snapshot => snapshot.Positions).NotNull().SetValidator(new PositionRules()));
        }
    }

    private sealed class PositionRules : AbstractValidator<BrokerAccountPositionEvidence>
    {
        public PositionRules()
        {
            RuleFor(position => position.ContractId).NotEmpty();
            // Both positive and negative quantities, including zero during reconciliation, are valid.
            RuleFor(position => position.AveragePrice).GreaterThanOrEqualTo(0);
        }
    }
}
