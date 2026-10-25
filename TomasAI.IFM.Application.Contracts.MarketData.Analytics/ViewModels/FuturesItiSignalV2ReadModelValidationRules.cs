using FluentValidation;
using FluentValidation.Results;
using MessagePack;
using Newtonsoft.Json;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;

/// <summary>
/// Validation rules for <see cref="FuturesItiSignalV2ReadModel"/> instances.
/// </summary>
/// <remarks>
/// Provides a simple entry point (<see cref="Execute"/>) used to validate incoming futures ITI signal view models
/// before they are processed. The concrete rules are implemented by the nested <see cref="FuturesItiSignalV2Validator"/>
/// which uses FluentValidation to describe validation constraints.
/// </remarks>
public class FuturesItiSignalV2ReadModelValidationRules : BaseValidationRules, IValidationRules<FuturesItiSignalV2ReadModel>
{
    static readonly FuturesItiSignalV2Validator Validator = new();
    /// <summary>
    /// Execute validation for the supplied <see cref="FuturesItiSignalV2ReadModel"/>.
    /// </summary>
    /// <param name="futuresItiSignal">The futures ITI signal view model to validate.</param>
    /// <returns>An array of <see cref="ValidationError"/> describing validation failures. Empty if valid.</returns>
    public ValidationError[] Execute(FuturesItiSignalV2ReadModel futuresItiSignal)
        => Validate(futuresItiSignal, Validator);

    /// <summary>
    /// FluentValidation validator describing the rules for <see cref="FuturesItiSignalV2ReadModel"/>.
    /// </summary>
    /// <remarks>
    /// The validator ensures required fields are populated, numeric values are valid (not NaN or Infinity),
    /// enums have valid values, and returns a friendly error when the instance itself is null.
    /// </remarks>
    sealed class FuturesItiSignalV2Validator : AbstractValidator<FuturesItiSignalV2ReadModel>
    {
        /// <summary>
        /// Initializes a new instance of <see cref="FuturesItiSignalV2Validator"/> and configures validation rules.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051", Justification = "Keep related signal validation checks together so qualification failures are aggregated in one pass.")]
        public FuturesItiSignalV2Validator()
        {
            // Required string fields
            RuleFor(x => x.ContractId)
                .NotEmpty()
                .WithMessage("FuturesItiSignalV2.ContractId is required");

            // Required date fields
            RuleFor(x => x.ValueDate)
                .NotEmpty()
                .WithMessage("FuturesItiSignalV2.ValueDate is required");

            RuleFor(x => x.TimeFrameStartValueDate)
                .Must((signal, frameStart) =>
                    frameStart == default || frameStart <= signal.ValueDate)
                .WithMessage("FuturesItiSignalV2.TimeFrameStartValueDate cannot be after ValueDate");

            // Enum validation
            RuleFor(x => x.TimePeriod)
                .IsInEnum()
                .NotEqual(TimeFrameType.None)
                .WithMessage("FuturesItiSignalV2.TimePeriod must be a valid value other than None");

            // Numeric validations - SequenceId should be positive
            RuleFor(x => x.SequenceId)
                .GreaterThan(0)
                .WithMessage("FuturesItiSignalV2.SequenceId must be greater than zero");

            // DateTime validation
            RuleFor(x => x.IntrinsicTime)
                .NotEmpty()
                .WithMessage("FuturesItiSignalV2.IntrinsicTime is required");

            // IntrinsicTimeLength should be non-negative
            RuleFor(x => x.IntrinsicTimeLength)
                .GreaterThanOrEqualTo(0)
                .Must(value => !double.IsNaN(value) && !double.IsInfinity(value))
                .WithMessage("FuturesItiSignalV2.IntrinsicTimeLength must be a valid non-negative number");

            // Price validations - must be valid doubles
            RuleFor(x => x.IntrinsicPrice)
                .Must(value => !double.IsNaN(value) && !double.IsInfinity(value))
                .WithMessage("FuturesItiSignalV2.IntrinsicPrice is NaN or Infinity");

            RuleFor(x => x.TrendPrice)
                .Must(value => !double.IsNaN(value) && !double.IsInfinity(value))
                .WithMessage("FuturesItiSignalV2.TrendPrice is NaN or Infinity");

            RuleFor(x => x.TrendExtreme)
                .Must(value => !double.IsNaN(value) && !double.IsInfinity(value))
                .WithMessage("FuturesItiSignalV2.TrendExtreme is NaN or Infinity");

            RuleFor(x => x.TrendReversal)
                .Must(value => !double.IsNaN(value) && !double.IsInfinity(value))
                .WithMessage("FuturesItiSignalV2.TrendReversal is NaN or Infinity");

            RuleFor(x => x.TrendDelta)
                .Must(value => !double.IsNaN(value) && !double.IsInfinity(value))
                .WithMessage("FuturesItiSignalV2.TrendDelta is NaN or Infinity");

            RuleFor(x => x.TargetDelta)
                .Must(value => !double.IsNaN(value) && !double.IsInfinity(value))
                .WithMessage("FuturesItiSignalV2.TargetDelta is NaN or Infinity");

            // Lambda parameter validation
            RuleFor(x => x.Lambda)
                .Must(value => !double.IsNaN(value) && !double.IsInfinity(value))
                .WithMessage("FuturesItiSignalV2.Lambda is NaN or Infinity");

            // TradingDays should be positive
            RuleFor(x => x.TradingDays)
                .GreaterThan(0)
                .WithMessage("FuturesItiSignalV2.TradingDays must be greater than zero");

            // Threshold validations
            RuleFor(x => x.Threshold)
                .Must(value => !double.IsNaN(value) && !double.IsInfinity(value))
                .WithMessage("FuturesItiSignalV2.Threshold is NaN or Infinity");

            RuleFor(x => x.UpTrendTrigger)
                .Must(value => !double.IsNaN(value) && !double.IsInfinity(value))
                .WithMessage("FuturesItiSignalV2.UpTrendTrigger is NaN or Infinity");

            RuleFor(x => x.DownTrendTrigger)
                .Must(value => !double.IsNaN(value) && !double.IsInfinity(value))
                .WithMessage("FuturesItiSignalV2.DownTrendTrigger is NaN or Infinity");

            RuleFor(x => x.BandAnchorPrice)
                .Must(value => !double.IsNaN(value) && !double.IsInfinity(value))
                .WithMessage("FuturesItiSignalV2.BandAnchorPrice is NaN or Infinity");

            RuleFor(x => x.BandPercentage)
                .GreaterThanOrEqualTo(0)
                .Must(value => !double.IsNaN(value) && !double.IsInfinity(value))
                .WithMessage("FuturesItiSignalV2.BandPercentage must be a valid non-negative number");

            RuleFor(x => x.BandSize)
                .GreaterThanOrEqualTo(0)
                .Must(value => !double.IsNaN(value) && !double.IsInfinity(value))
                .WithMessage("FuturesItiSignalV2.BandSize must be a valid non-negative number");

            RuleFor(x => x.BandLevel)
                .Must(value => !double.IsNaN(value) && !double.IsInfinity(value))
                .WithMessage("FuturesItiSignalV2.BandLevel must be a finite number");

            RuleFor(x => x.ReversalLevel)
                .Must(value => !double.IsNaN(value) && !double.IsInfinity(value))
                .WithMessage("FuturesItiSignalV2.ReversalLevel must be a finite number");

            // Enum validations
            RuleFor(x => x.IntrinsicTimeTrend)
                .IsInEnum()
                .WithMessage("FuturesItiSignalV2.IntrinsicTimeTrend must be a valid enum value");

            RuleFor(x => x.IntrinsicTimeMode)
                .IsInEnum()
                .WithMessage("FuturesItiSignalV2.IntrinsicTimeMode must be a valid enum value");

            RuleFor(x => x.TradeState)
                .IsInEnum()
                .WithMessage("FuturesItiSignalV2.TradeState must be a valid enum value");
        }

        /// <summary>
        /// Validates the supplied <see cref="FuturesItiSignalV2ReadModel"/> instance.
        /// </summary>
        /// <param name="context">The validation context containing the instance to validate.</param>
        /// <returns>A <see cref="ValidationResult"/> describing validation failures.</returns>
        /// <remarks>
        /// This override provides an explicit null-check for the instance and returns a single validation
        /// failure describing the null instance to keep error reporting consistent.
        /// </remarks>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "Return aggregated validation failures for malformed external models rather than allowing exceptions to escape.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0015", Justification = "The validation diagnostic intentionally identifies the validated model property.")]
        public override ValidationResult Validate(ValidationContext<FuturesItiSignalV2ReadModel> context)
        {
            try
            {
                ArgumentNullException.ThrowIfNull(context.InstanceToValidate);
            }
            catch
            {
                var validationResult = new ValidationResult();
                validationResult.Errors.Add(new ValidationFailure("FuturesItiSignalV2", "FuturesItiSignalV2 instance is null"));
                return validationResult;
            }
            return base.Validate(context);
        }
    }
}
