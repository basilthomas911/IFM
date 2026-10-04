using FluentValidation;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;

/// <summary>Structural rules for exact financial authorization evidence, before database fencing.</summary>
public static class FundRiskAuthorizationValidation
{
    private static readonly AuthorizationRules Rules = new();

    /// <summary>Appends every invalid authorization field without throwing.</summary>
    public static List<ValidationError> ValidateFundRiskAuthorization(this List<ValidationError> errors, FundRiskAuthorizationReference? authorization)
    {
        if (authorization is null) errors.Add(new("Financial authorization is required."));
        else errors.AddRange(Rules.Validate(authorization).Errors.Select(static error => new ValidationError(error.ErrorMessage)));
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
