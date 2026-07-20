using FluentValidation;
using FluentValidation.Results;
using TomasAI.IFM.Shared.Validation;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Fund.Command.Validation;

/// <summary>
/// Validation rules for <see cref="FundOrderReadModel"/> instances.
/// </summary>
/// <remarks>
/// Provides a simple entry point (<see cref="Execute"/>) used by actor command handlers and decorators
/// to validate incoming fund order view models before they are processed. The concrete rules are implemented
/// by the nested <see cref="FundOrderValidator"/> which uses FluentValidation to describe validation constraints.
/// </remarks>
public class FundOrderValidationRules : BaseValidationRules, IValidationRules<FundOrderReadModel>
{
    /// <summary>
    /// Execute validation for the supplied <see cref="FundOrderReadModel"/>.
    /// </summary>
    /// <param name="fundOrder">The fund order view model to validate.</param>
    /// <returns>An array of <see cref="ValidationError"/> describing validation failures. Empty if valid.</returns>
    public ValidationError[] Execute(FundOrderReadModel fundOrder)
        => Validate(fundOrder, new FundOrderValidator());

    /// <summary>
    /// FluentValidation validator describing the rules for <see cref="FundOrderReadModel"/>.
    /// </summary>
    /// <remarks>
    /// The validator ensures required numeric identifiers are positive and returns a friendly error when the
    /// instance itself is null.
    /// </remarks>
    class FundOrderValidator : AbstractValidator<FundOrderReadModel>
    {
        /// <summary>
        /// Initializes a new instance of <see cref="FundOrderValidator"/> and configures validation rules.
        /// </summary>
        public FundOrderValidator()
        {
            RuleFor(x => x.OrderId).GreaterThan(0).WithMessage("FundOrder.OrderId is zero or negative");
            RuleFor(x => x.FundId).GreaterThan(0).WithMessage("FundOrder.FundId is zero or negative");
            RuleFor(x => x.OrderDate).Must(e => e > DateTime.MinValue && e < DateTime.MaxValue).WithMessage("FundOrder.OrderDate is not a valid date");
        }

        /// <summary>
        /// Validates the supplied <see cref="FundOrderReadModel"/> instance.
        /// </summary>
        /// <param name="context">The validation context containing the instance to validate.</param>
        /// <returns>A <see cref="ValidationResult"/> describing validation failures.</returns>
        /// <remarks>
        /// This override provides an explicit null-check for the instance and returns a single validation
        /// failure describing the null instance to keep error reporting consistent.
        /// </remarks>
        public override ValidationResult Validate(ValidationContext<FundOrderReadModel> context)
        {
            try
            {
                ArgumentNullException.ThrowIfNull(context.InstanceToValidate);
            }
            catch
            {
                var validationResult = new ValidationResult();
                validationResult.Errors.Add(new ValidationFailure("FundOrder", "FundOrder instance is null"));
                return validationResult;
            }
            return base.Validate(context);
        }
    }
}