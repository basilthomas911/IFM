using FluentValidation;
using FluentValidation.Results;
using TomasAI.IFM.Shared.Validation;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Fund.Command.Validation;

/// <summary>
/// Validation rules for <see cref="FundOrderTradeReadModel"/> instances used by the Fund actor.
/// </summary>
/// <remarks>
/// Provides an <see cref="Execute"/> entry point used by actor command handlers and decorators
/// to validate incoming fund order trade view models before they are processed. The concrete rules are implemented
/// by the nested <see cref="FundOrderTradeValidator"/> which uses FluentValidation to describe validation constraints.
/// </remarks>
public class FundOrderTradeValidationRules : BaseValidationRules, IValidationRules<FundOrderTradeReadModel>
{
    /// <summary>
    /// Validate the provided <see cref="FundOrderTradeReadModel"/> and return any validation errors.
    /// </summary>
    /// <param name="fundOrderTrade">The fund order trade view model to validate.</param>
    /// <returns>An array of <see cref="ValidationError"/> describing validation failures. Empty if valid.</returns>
    public ValidationError[] Execute(FundOrderTradeReadModel fundOrderTrade)
        => Validate(fundOrderTrade, new FundOrderTradeValidator());

    /// <summary>
    /// FluentValidation validator describing rules for <see cref="FundOrderTradeReadModel"/>.
    /// </summary>
    /// <remarks>
    /// The validator ensures required numeric identifiers are positive and that dates are provided.
    /// It also returns a friendly error when the instance itself is null.
    /// </remarks>
    class FundOrderTradeValidator : AbstractValidator<FundOrderTradeReadModel>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FundOrderTradeValidator"/> and configures validation rules.
        /// </summary>
        public FundOrderTradeValidator()
        {
            RuleFor(x => x.OrderId).GreaterThan(0).WithMessage("FundOrderTrade.OrderId is zero or negative");
            RuleFor(x => x.TradeId).GreaterThan(0).WithMessage("FundOrderTrade.TradeId is zero or negative");
            RuleFor(x => x.TradeDate).Must(d => d > DateOnly.MinValue && d < DateOnly.MaxValue).WithMessage("FundOrderTrade.TradeDate is invalid");
            RuleFor(x => x.MaturityDate).Must(d => d > DateOnly.MinValue && d < DateOnly.MaxValue).WithMessage("FundOrderTrade.MaturityDate is invalid");
        }

        /// <summary>
        /// Validates the supplied <see cref="FundOrderTradeReadModel"/> instance.
        /// </summary>
        /// <param name="context">The validation context containing the instance to validate.</param>
        /// <returns>A <see cref="ValidationResult"/> describing validation failures.</returns>
        /// <remarks>
        /// This override performs an explicit null check and returns a single validation failure when the
        /// instance to validate is null to keep error reporting consistent.
        /// </remarks>
        public override ValidationResult Validate(ValidationContext<FundOrderTradeReadModel> context)
        {
            try
            {
                ArgumentNullException.ThrowIfNull(context.InstanceToValidate);
            }
            catch
            {
                var validationResult = new ValidationResult();
                validationResult.Errors.Add(new ValidationFailure("FundOrderTrade", "FundOrderTrade instance is null"));
                return validationResult;
            }
            return base.Validate(context);
        }
    }
}