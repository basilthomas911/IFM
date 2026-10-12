using FluentValidation;
using FluentValidation.Results;
using MessagePack;
using Newtonsoft.Json;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;

/// <summary>
/// Provides FluentValidation rules for <see cref="FuturesBarDataReadModel"/> instances.
/// </summary>
/// <remarks>
/// Validates futures bar data ensuring all required fields are present, valid, and consistent
/// with business rules for bar/OHLC data points.
/// </remarks>
public class FuturesBarDataReadModelValidationRules : BaseValidationRules, IValidationRules<FuturesBarDataReadModel>
{
    /// <summary>
    /// Executes validation rules against the specified FuturesBarDataReadModel instance.
    /// </summary>
    /// <param name="futuresBarData">The futures bar data read model to validate.</param>
    /// <returns>An array of validation errors, or an empty array if validation passes.</returns>
    public ValidationError[] Execute(FuturesBarDataReadModel futuresBarData)
        => Validate(futuresBarData, new FuturesBarDataReadModelValidator());

    /// <summary>
    /// Internal FluentValidation validator for FuturesBarDataReadModel.
    /// </summary>
    private sealed class FuturesBarDataReadModelValidator : AbstractValidator<FuturesBarDataReadModel>
    {
        public FuturesBarDataReadModelValidator()
        {
            // ContractId validation
            RuleFor(x => x.ContractId)
                .NotEmpty()
                .WithMessage("FuturesBarData.ContractId is required");

            // Symbol validation
            RuleFor(x => x.Symbol)
                .NotEmpty()
                .WithMessage("FuturesBarData.Symbol is required");

            // ValueDate validation
            RuleFor(x => x.ValueDate)
                .Must(valueDate => valueDate != DateOnly.MinValue && valueDate != DateOnly.MaxValue)
                .WithMessage("FuturesBarData.ValueDate is invalid");

            // BarDate validation
            RuleFor(x => x.BarDate)
                .Must(barDate => barDate != DateTime.MinValue && barDate != DateTime.MaxValue)
                .WithMessage("FuturesBarData.BarDate is invalid");

            // BarRateType validation (enum should be defined)
            RuleFor(x => x.BarRateType)
                .IsInEnum()
                .WithMessage("FuturesBarData.BarRateType is invalid");

            // BarValue validation (should be reasonable)
            RuleFor(x => x.BarValue)
                .Must(barValue => barValue >= 0)
                .WithMessage("FuturesBarData.BarValue must be non-negative");

            // UpTrendTrigger validation (should not be NaN)
            RuleFor(x => x.UpTrendTrigger)
                .Must(upTrendTrigger => !double.IsNaN(upTrendTrigger))
                .WithMessage("FuturesBarData.UpTrendTrigger cannot be NaN");

            // DownTrendTrigger validation (should not be NaN)
            RuleFor(x => x.DownTrendTrigger)
                .Must(downTrendTrigger => !double.IsNaN(downTrendTrigger))
                .WithMessage("FuturesBarData.DownTrendTrigger cannot be NaN");
        }

        /// <summary>
        /// Overrides the base validation to ensure the instance is not null before validating properties.
        /// </summary>
        /// <param name="context">The validation context.</param>
        /// <returns>The validation result.</returns>
        public override ValidationResult Validate(ValidationContext<FuturesBarDataReadModel> context)
        {
            ArgumentNullException.ThrowIfNull(context);
            if (context.InstanceToValidate is null)
            {
                var validationResult = new ValidationResult();
                validationResult.Errors.Add(new ValidationFailure("FuturesBarData", "FuturesBarData instance is null"));
                return validationResult;
            }
            return base.Validate(context);
        }
    }
}