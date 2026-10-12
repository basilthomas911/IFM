using FluentValidation;
using FluentValidation.Results;
using MessagePack;
using Newtonsoft.Json;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

/// <summary>
/// Provides validation rules for yield curve rate data, ensuring that all required fields are present and contain valid
/// values.
/// </summary>
/// <remarks>This class implements validation logic for the YieldCurveRateReadModel, checking that date and rate
/// fields are within acceptable ranges and not invalid (such as NaN or out-of-range dates). Use this class to validate
/// yield curve rate entries before processing or persisting them. Inherits from BaseValidationRules and implements
/// IValidationRules for yield curve rate models.</remarks>
public class YieldCurveRateValidationRules : BaseValidationRules, IValidationRules<YieldCurveRateReadModel>
{
    static readonly YieldCurveRateValidator Validator = new();

    public ValidationError[] Execute(YieldCurveRateReadModel e) => Validate(e, Validator);

    sealed class YieldCurveRateValidator : AbstractValidator<YieldCurveRateReadModel>
    {
        public YieldCurveRateValidator()
        {
            RuleFor(x => x.ValueDate).Must(e => e != DateOnly.MinValue && e != DateOnly.MaxValue).WithMessage("YieldCurveRate.ValueDate is invalid");
            RuleFor(x => x.OneMonth).Must(e => !double.IsNaN(e)).GreaterThanOrEqualTo(0.0).WithMessage(e => $"YieldCurveRate.OneMonth for {e.ValueDate:yyyy-MM-dd} is not a valid number");
            RuleFor(x => x.TwoMonth).Must(e => !double.IsNaN(e)).GreaterThanOrEqualTo(0.0).WithMessage(e => $"YieldCurveRate.TwoMonth for {e.ValueDate:yyyy-MM-dd} is not a valid number");
            RuleFor(x => x.ThreeMonth).Must(e => !double.IsNaN(e)).GreaterThanOrEqualTo(0.0).WithMessage(e => $"YieldCurveRate.ThreeMonth for {e.ValueDate:yyyy-MM-dd} is not a valid number");
            RuleFor(x => x.SixMonth).Must(e => !double.IsNaN(e)).GreaterThanOrEqualTo(0.0).WithMessage(e => $"YieldCurveRate.SixMonth for {e.ValueDate:yyyy-MM-dd} is not a valid number");
            RuleFor(x => x.OneYear).Must(e => !double.IsNaN(e)).GreaterThanOrEqualTo(0.0).WithMessage(e => $"YieldCurveRate.OneYear for {e.ValueDate:yyyy-MM-dd} is not a valid number");
            RuleFor(x => x.TwoYear).Must(e => !double.IsNaN(e)).GreaterThanOrEqualTo(0.0).WithMessage(e => $"YieldCurveRate.TwoYear for {e.ValueDate:yyyy-MM-dd} is not a valid number");
            RuleFor(x => x.ThreeYear).Must(e => !double.IsNaN(e)).GreaterThanOrEqualTo(0.0).WithMessage(e => $"YieldCurveRate.ThreeYear for {e.ValueDate:yyyy-MM-dd} is not a valid number");
            RuleFor(x => x.FiveYear).Must(e => !double.IsNaN(e)).GreaterThanOrEqualTo(0.0).WithMessage(e => $"YieldCurveRate.FiveYear for {e.ValueDate:yyyy-MM-dd} is not a valid number");
            RuleFor(x => x.SevenYear).Must(e => !double.IsNaN(e)).GreaterThanOrEqualTo(0.0).WithMessage(e => $"YieldCurveRate.SevenYear for {e.ValueDate:yyyy-MM-dd} is not a valid number");
            RuleFor(x => x.TenYear).Must(e => !double.IsNaN(e)).GreaterThanOrEqualTo(0.0).WithMessage(e => $"YieldCurveRate.TenYear for {e.ValueDate:yyyy-MM-dd} is not a valid number");
            RuleFor(x => x.TwentyYear).Must(e => !double.IsNaN(e)).GreaterThanOrEqualTo(0.0).WithMessage(e => $"YieldCurveRate.TwentyYear for {e.ValueDate:yyyy-MM-dd} is not a valid number");
            RuleFor(x => x.ThirtyYear).Must(e => !double.IsNaN(e)).GreaterThanOrEqualTo(0.0).WithMessage(e => $"YieldCurveRate.ThirtyYear for {e.ValueDate:yyyy-MM-dd} is not a valid number");
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0015", Justification = "The existing model/command Validate method validates object properties, not method parameters. Preserve ArgumentException and property-specific diagnostic labels consumed by validation/error adapters.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "Validation collects failure details instead of throwing; compatibility lookup helpers preserve their documented fallback when input data is unavailable.")]
        public override ValidationResult Validate(ValidationContext<YieldCurveRateReadModel> context)
        {
            try
            {
                ArgumentNullException.ThrowIfNull(context.InstanceToValidate);
            }
            catch
            {
                var validationResult = new ValidationResult();
                validationResult.Errors.Add(new ValidationFailure("YieldCurveRate", "YieldCurveRate instance is null"));
                return validationResult;
            }
            return base.Validate(context);
        }

    }
}
