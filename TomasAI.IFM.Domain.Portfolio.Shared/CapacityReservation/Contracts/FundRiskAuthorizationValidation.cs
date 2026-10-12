using FluentValidation;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;

/// <summary>Structural rules for exact financial authorization evidence, before database fencing.</summary>
public static class FundRiskAuthorizationValidation
{
    private static readonly AuthorizationRules Rules = new();

    /// <summary>Appends every invalid authorization field without throwing.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1002", Justification = "Preserve the existing concrete financial model collection contract.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0016", Justification = "Preserve the existing concrete financial model collection contract.")]
    public static TErrors ValidateFundRiskAuthorization<TErrors>(this TErrors errors, FundRiskAuthorizationReference? authorization)

        where TErrors : class, ICollection<ValidationError>
    {
        ArgumentNullException.ThrowIfNull(errors);
        if (authorization is null) errors.Add(new("Financial authorization is required."));
        else
            foreach (var failure in Rules.Validate(authorization).Errors)
                errors.Add(new ValidationError(failure.ErrorMessage));
        return errors;
    }

    private sealed class AuthorizationRules : AbstractValidator<FundRiskAuthorizationReference>
    {
        public AuthorizationRules()
        {
            RuleFor(value => value.SchemaVersion).Equal(1);
            RuleFor(value => value.RiskInvocationId).NotEmpty();
            RuleFor(value => value.RiskResultId).NotEmpty();
            RuleFor(value => value.ReservationId).NotEmpty();
            RuleFor(value => value.ReservationCompletedEventId).NotEmpty();
            RuleFor(value => value.ReservationOperationId).NotEmpty();
            RuleFor(value => value.WorkflowId).NotEmpty();
            RuleFor(value => value.PortfolioId).GreaterThan(0);
            RuleFor(value => value.FundId).GreaterThan(0);
            RuleFor(value => value.OrderId).GreaterThan(0);
            RuleFor(value => value.StrategyUnits).GreaterThan(0);
            RuleFor(value => value.FinancialRevision).GreaterThan(0);
            RuleFor(value => value.AuthorityEpoch).GreaterThan(0);
            RuleFor(value => value.ValidUntilUtc).Must(time => time != default && time.Kind == DateTimeKind.Utc);
            RuleFor(value => value.ExecutionEnvironment).Equal("Emulator");
            RuleFor(value => value.CompositionResultHash).Must(IsHash);
            RuleFor(value => value.UnitCandidateHash).Must(IsHash);
            RuleFor(value => value.RiskAssessmentHash).Must(IsHash);
            RuleFor(value => value.SizedOrderHash).Must(IsHash);
            RuleFor(value => value.RequirementsHash).Must(IsHash);
        }

        private static bool IsHash(string? value) => value is { Length: 64 } && value.All(Uri.IsHexDigit);
    }
}
