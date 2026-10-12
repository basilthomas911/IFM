using FluentValidation;
using FluentValidation.Results;
using MessagePack;
using Newtonsoft.Json;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

/// <summary>
/// Validation rules for <see cref="FuturesEodDataV2ReadModel"/> instances.
/// </summary>
/// <remarks>
/// Provides a simple entry point (<see cref="Execute"/>) used to validate incoming futures EOD data view models
/// before they are processed. The concrete rules are implemented by the nested <see cref="FuturesEodDataV2Validator"/>
/// which uses FluentValidation to describe validation constraints.
/// </remarks>
public class FuturesEodDataValidationRules : BaseValidationRules, IValidationRules<FuturesEodDataV2ReadModel>
{
    /// <summary>
    /// Execute validation for the supplied <see cref="FuturesEodDataV2ReadModel"/>.
    /// </summary>
    /// <param name="futuresEodData">The futures EOD data view model to validate.</param>
    /// <returns>An array of <see cref="ValidationError"/> describing validation failures. Empty if valid.</returns>
    public ValidationError[] Execute(FuturesEodDataV2ReadModel futuresEodData)
        => Validate(futuresEodData, new FuturesEodDataV2Validator());

    /// <summary>
    /// FluentValidation validator describing the rules for <see cref="FuturesEodDataV2ReadModel"/>.
    /// </summary>
    /// <remarks>
    /// The validator ensures required fields are populated, numeric values are valid (not NaN or Infinity),
    /// enums have valid values, prices are non-negative, and returns a friendly error when the instance itself is null.
    /// </remarks>
    sealed class FuturesEodDataV2Validator : AbstractValidator<FuturesEodDataV2ReadModel>
    {
        /// <summary>
        /// Initializes a new instance of <see cref="FuturesEodDataV2Validator"/> and configures validation rules.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051", Justification = "Keep related reference qualification invariants together so the accepted payload requirements can be audited as one validation step.")]
        public FuturesEodDataV2Validator()
        {
            // Required string fields
            RuleFor(x => x.ContractId)
                .NotEmpty()
                .WithMessage("FuturesEodDataV2.ContractId is required");

            RuleFor(x => x.Symbol)
                .NotEmpty()
                .WithMessage("FuturesEodDataV2.Symbol is required");

            // Required date fields
            RuleFor(x => x.ValueDate)
                .NotEmpty()
                .WithMessage("FuturesEodDataV2.ValueDate is required");

            // Price validations - must be non-negative decimals
            RuleFor(x => x.OpenPrice)
                .GreaterThanOrEqualTo(0)
                .WithMessage("FuturesEodDataV2.OpenPrice must be non-negative");

            RuleFor(x => x.HighPrice)
                .GreaterThanOrEqualTo(0)
                .WithMessage("FuturesEodDataV2.HighPrice must be non-negative");

            RuleFor(x => x.LowPrice)
                .GreaterThanOrEqualTo(0)
                .WithMessage("FuturesEodDataV2.LowPrice must be non-negative");

            RuleFor(x => x.ClosePrice)
                .GreaterThanOrEqualTo(0)
                .WithMessage("FuturesEodDataV2.ClosePrice must be non-negative");

            // Volume validation - must be non-negative
            RuleFor(x => x.Volume)
                .GreaterThanOrEqualTo(0)
                .WithMessage("FuturesEodDataV2.Volume must be non-negative");

            // Double validations - must be valid numbers
            RuleFor(x => x.DailyPercentChange)
                .Must(value => !double.IsNaN(value) && !double.IsInfinity(value))
                .WithMessage("FuturesEodDataV2.DailyPercentChange is NaN or Infinity");

            RuleFor(x => x.DailyStdDev)
                .Must(value => !double.IsNaN(value) && !double.IsInfinity(value))
                .WithMessage("FuturesEodDataV2.DailyStdDev is NaN or Infinity");

            RuleFor(x => x.DailyStdDevAmount)
                .Must(value => !double.IsNaN(value) && !double.IsInfinity(value))
                .WithMessage("FuturesEodDataV2.DailyStdDevAmount is NaN or Infinity");

            RuleFor(x => x.UpperBand)
                .Must(value => !double.IsNaN(value) && !double.IsInfinity(value))
                .WithMessage("FuturesEodDataV2.UpperBand is NaN or Infinity");

            RuleFor(x => x.Mean)
                .Must(value => !double.IsNaN(value) && !double.IsInfinity(value))
                .WithMessage("FuturesEodDataV2.Mean is NaN or Infinity");

            RuleFor(x => x.LowerBand)
                .Must(value => !double.IsNaN(value) && !double.IsInfinity(value))
                .WithMessage("FuturesEodDataV2.LowerBand is NaN or Infinity");

            RuleFor(x => x.MarketDirectionIndicator)
                .Must(value => !double.IsNaN(value) && !double.IsInfinity(value))
                .WithMessage("FuturesEodDataV2.MarketDirectionIndicator is NaN or Infinity");

            // Enum validations
            RuleFor(x => x.MarketDirection)
                .IsInEnum()
                .WithMessage("FuturesEodDataV2.MarketDirection must be a valid enum value");

            RuleFor(x => x.MarketVolatility)
                .IsInEnum()
                .WithMessage("FuturesEodDataV2.MarketVolatility must be a valid enum value");

            RuleFor(x => x.PriceDirection)
                .IsInEnum()
                .WithMessage("FuturesEodDataV2.PriceDirection must be a valid enum value");

            RuleFor(x => x.PriceVolatility)
                .IsInEnum()
                .WithMessage("FuturesEodDataV2.PriceVolatility must be a valid enum value");

            // WindowSize should be non-negative
            RuleFor(x => x.WindowSize)
                .GreaterThanOrEqualTo(0)
                .WithMessage("FuturesEodDataV2.WindowSize must be non-negative");

            // Moving averages - must be non-negative
            RuleFor(x => x.FiftyDMA)
                .GreaterThanOrEqualTo(0)
                .WithMessage("FuturesEodDataV2.FiftyDMA must be non-negative");

            RuleFor(x => x.TwoHundredDMA)
                .GreaterThanOrEqualTo(0)
                .WithMessage("FuturesEodDataV2.TwoHundredDMA must be non-negative");
        }

        /// <summary>
        /// Validates the supplied <see cref="FuturesEodDataV2ReadModel"/> instance.
        /// </summary>
        /// <param name="context">The validation context containing the instance to validate.</param>
        /// <returns>A <see cref="ValidationResult"/> describing validation failures.</returns>
        /// <remarks>
        /// This override provides an explicit null-check for the instance and returns a single validation
        /// failure describing the null instance to keep error reporting consistent.
        /// </remarks>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0015", Justification = "The existing model/command Validate method validates object properties, not method parameters. Preserve ArgumentException and property-specific diagnostic labels consumed by validation/error adapters.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "Validation collects failure details instead of throwing; compatibility lookup helpers preserve their documented fallback when input data is unavailable.")]
        public override ValidationResult Validate(ValidationContext<FuturesEodDataV2ReadModel> context)
        {
            try
            {
                ArgumentNullException.ThrowIfNull(context.InstanceToValidate);
            }
            catch
            {
                var validationResult = new ValidationResult();
                validationResult.Errors.Add(new ValidationFailure("FuturesEodDataV2", "FuturesEodDataV2 instance is null"));
                return validationResult;
            }
            return base.Validate(context);
        }
    }
}
