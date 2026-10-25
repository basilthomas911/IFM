using FluentValidation;
using FluentValidation.Results;
using MessagePack;
using Newtonsoft.Json;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;

/// <summary>
/// Provides FluentValidation rules for <see cref="FuturesTdiSignalReadModel"/> instances.
/// </summary>
/// <remarks>
/// Validates TDI signal data ensuring all required fields are present, valid, and consistent
/// with business rules for trend direction indicators.
/// </remarks>
public class FuturesTdiSignalReadModelValidationRules : BaseValidationRules, IValidationRules<FuturesTdiSignalReadModel>
{
    static readonly FuturesTdiSignalReadModelValidator Validator = new();
    /// <summary>
    /// Executes validation rules against the specified FuturesTdiSignalReadModel instance.
    /// </summary>
    /// <param name="futuresTdiSignal">The TDI signal read model to validate.</param>
    /// <returns>An array of validation errors, or an empty array if validation passes.</returns>
    public ValidationError[] Execute(FuturesTdiSignalReadModel futuresTdiSignal)
        => Validate(futuresTdiSignal, Validator);

    /// <summary>
    /// Internal FluentValidation validator for FuturesTdiSignalReadModel.
    /// </summary>
    private sealed class FuturesTdiSignalReadModelValidator : AbstractValidator<FuturesTdiSignalReadModel>
    {
        public FuturesTdiSignalReadModelValidator()
        {
            // ContractId validation
            RuleFor(x => x.ContractId)
                .NotEmpty()
                .WithMessage("FuturesTdiSignal.ContractId is required");

            // ValueDate validation
            RuleFor(x => x.ValueDate)
                .Must(valueDate => valueDate != DateOnly.MinValue && valueDate != DateOnly.MaxValue)
                .WithMessage("FuturesTdiSignal.ValueDate is invalid");

            // TimePeriod validation (enum should be defined)
            RuleFor(x => x.TimePeriod)
                .Must(FuturesTdiConfiguration.IsSupportedIntraday)
                .WithMessage("FuturesTdiSignal.TimePeriod must be an intraday period");

            // Timestamp validation
            RuleFor(x => x.Timestamp)
                .Must(timestamp => timestamp != TimeOnly.MinValue && timestamp != TimeOnly.MaxValue)
                .WithMessage("FuturesTdiSignal.Timestamp is invalid");

            // UpTrendCount validation (should be non-negative)
            RuleFor(x => x.UpTrendCount)
                .GreaterThanOrEqualTo(0)
                .WithMessage("FuturesTdiSignal.UpTrendCount must be non-negative");

            // DownTrendCount validation (should be non-negative)
            RuleFor(x => x.DownTrendCount)
                .GreaterThanOrEqualTo(0)
                .WithMessage("FuturesTdiSignal.DownTrendCount must be non-negative");

            // TDI trend classification validation
            RuleFor(x => x.TDI)
                .IsInEnum()
                .WithMessage("FuturesTdiSignal.TDI is invalid");

            // TDIStrength validation
            RuleFor(x => x.TDIStrength)
                .IsInEnum()
                .WithMessage("FuturesTdiSignal.TDIStrength is invalid");

            When(x => x.SchemaVersion >= FuturesTdiConfiguration.CurrentSchemaVersion, () =>
            {
                RuleFor(x => x.ConfigurationId).NotEmpty();
                RuleFor(x => x.RsiPeriod).GreaterThan(1);
                RuleFor(x => x.PriceLinePeriod).GreaterThan(0);
                RuleFor(x => x.SignalLinePeriod).GreaterThan(0);
                RuleFor(x => x.MarketBasePeriod).GreaterThan(1);
                RuleFor(x => x.VolatilityBandPeriod).GreaterThan(1);
                RuleFor(x => x.VolatilityBandDeviation).GreaterThan(0d);
                RuleFor(x => x.Rsi).InclusiveBetween(0d, 100d);
                RuleFor(x => x.PriceLine).InclusiveBetween(0d, 100d);
                RuleFor(x => x.SignalLine).InclusiveBetween(0d, 100d);
                RuleFor(x => x.MarketBaseLine).InclusiveBetween(0d, 100d);
                RuleFor(x => x.UpperVolatilityBand).GreaterThanOrEqualTo(x => x.LowerVolatilityBand);
                RuleFor(x => x.Cross).IsInEnum();
                RuleFor(x => x.MarketState).IsInEnum();
            });
        }

        /// <summary>
        /// Overrides the base validation to ensure the instance is not null before validating properties.
        /// </summary>
        /// <param name="context">The validation context.</param>
        /// <returns>The validation result.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "Return aggregated validation failures for malformed external models rather than allowing exceptions to escape.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0015", Justification = "The validation diagnostic intentionally identifies the validated model property.")]
        public override ValidationResult Validate(ValidationContext<FuturesTdiSignalReadModel> context)
        {
            try
            {
                ArgumentNullException.ThrowIfNull(context.InstanceToValidate);
            }
            catch
            {
                var validationResult = new ValidationResult();
                validationResult.Errors.Add(new ValidationFailure("FuturesTdiSignal", "FuturesTdiSignal instance is null"));
                return validationResult;
            }
            return base.Validate(context);
        }
    }
}
