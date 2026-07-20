using FluentValidation;
using FluentValidation.Results;
using TomasAI.IFM.Shared.Validation;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Fund.Command.Validation;

/// <summary>
/// Validation rules for <see cref="FundReadModel"/> instances used by the Fund actor.
/// </summary>
/// <remarks>
/// Provides a simple entry point (<see cref="Execute"/>) used by actor command handlers and decorators
/// to validate incoming fund view models before they are processed. The concrete rules are implemented
/// by the nested <see cref="FundValidator"/> which uses FluentValidation to describe validation constraints.
/// </remarks>
public class FundValidationRules : BaseValidationRules, IValidationRules<FundReadModel>
{
    /// <summary>
    /// Execute validation for the supplied <see cref="FundReadModel"/>.
    /// </summary>
    /// <param name="fundOrder">The fund view model to validate.</param>
    /// <returns>An array of <see cref="ValidationError"/> describing validation failures. Empty if valid.</returns>
    public ValidationError[] Execute(FundReadModel fundOrder)
        => Validate(fundOrder, new FundValidator());

    /// <summary>
    /// FluentValidation validator describing the rules for <see cref="FundReadModel"/>.
    /// </summary>
    /// <remarks>
    /// The validator ensures required numeric identifiers are positive and returns a friendly error when the
    /// instance itself is null.
    /// </remarks>
    class FundValidator : AbstractValidator<FundReadModel>
    {
        /// <summary>
        /// Initializes a new instance of <see cref="FundValidator"/> and configures validation rules.
        /// </summary>
        public FundValidator()
        {
            RuleFor(x => x.FundId).GreaterThan(0).WithMessage("Fund.FundId is zero or negative");
        }

        /// <summary>
        /// Validates the supplied <see cref="FundReadModel"/> instance.
        /// </summary>
        /// <param name="context">The validation context containing the instance to validate.</param>
        /// <returns>A <see cref="ValidationResult"/> describing validation failures.</returns>
        /// <remarks>
        /// This override provides an explicit null-check for the instance and returns a single validation
        /// failure describing the null instance to keep error reporting consistent.
        /// </remarks>
        public override ValidationResult Validate(ValidationContext<FundReadModel> context)
        {
            try
            {
                ArgumentNullException.ThrowIfNull(context.InstanceToValidate);
            }
            catch
            {
                var validationResult = new ValidationResult();
                validationResult.Errors.Add(new ValidationFailure("Fund", "Fund instance is null"));
                return validationResult;
            }
            return base.Validate(context);
        }
    }
}