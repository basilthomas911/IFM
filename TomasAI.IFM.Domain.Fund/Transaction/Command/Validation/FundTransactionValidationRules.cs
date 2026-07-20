using FluentValidation;
using FluentValidation.Results;
using TomasAI.IFM.Shared.Validation;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Fund.Transaction.Command.Validation;

/// <summary>
/// Validation rules for <see cref="FundTransactionReadModel"/> used in the Fund Transaction actor.
/// </summary>
/// <remarks>
/// Implements <see cref="IValidationRules{T}"/> to provide a simple entry point for validating
/// incoming fund transaction view models before they are processed by command handlers.
/// </remarks>
public class FundTransactionValidationRules : BaseValidationRules, IValidationRules<FundTransactionReadModel>
{
    /// <summary>
    /// Validate the supplied <paramref name="fundTransaction"/> and return any validation errors.
    /// </summary>
    /// <param name="fundTransaction">The fund transaction view model to validate.</param>
    /// <returns>An array of <see cref="ValidationError"/> describing validation problems. Empty if valid.</returns>
    public ValidationError[] Execute(FundTransactionReadModel fundTransaction) => Validate(fundTransaction, new FundTransactionValidator());

    /// <summary>
    /// FluentValidation validator that defines the validation rules for <see cref="FundTransactionReadModel"/>.
    /// </summary>
    private class FundTransactionValidator : AbstractValidator<FundTransactionReadModel>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FundTransactionValidator"/> and configures rules.
        /// </summary>
        public FundTransactionValidator()
        {
            RuleFor(x => x.FundId).GreaterThan(0).WithMessage("FundTransaction.FundId is zero or negative");
            RuleFor(x => x.OrderId).GreaterThan(0).WithMessage("FundTransaction.OrderId is zero or negative");
            RuleFor(x => x.TradeId).GreaterThan(0).WithMessage("FundTransaction.TradeId is zero or negative");
            RuleFor(x => x.TransactionDate).Must(d => d != DateTime.MinValue && d != DateTime.MaxValue).WithMessage("FundTransaction.TransactionDate is invalid");
            RuleFor(x => x.ValueDate).Must(d => d != DateOnly.MinValue && d != DateOnly.MaxValue).WithMessage("FundTransaction.ValueDate is invalid");
        }

        /// <summary>
        /// Validates the provided <see cref="FundTransactionReadModel"/>, returning a <see cref="ValidationResult"/>.
        /// </summary>
        /// <param name="context">The validation context containing the instance to validate.</param>
        /// <returns>A <see cref="ValidationResult"/> describing any validation failures.</returns>
        /// <remarks>
        /// Overrides the base <see cref="AbstractValidator{T}.Validate(ValidationContext{T})"/> to provide a
        /// consistent error when the instance to validate is null.
        /// </remarks>
        public override ValidationResult Validate(ValidationContext<FundTransactionReadModel> context)
        {
            try
            {
                ArgumentNullException.ThrowIfNull(context.InstanceToValidate);
            }
            catch 
            {
                var validationResult = new ValidationResult();
                validationResult.Errors.Add(new ValidationFailure("FundTransaction", "FundTransaction instance is null"));
                return validationResult;
            }
            return base.Validate(context);
        }
    }
}
