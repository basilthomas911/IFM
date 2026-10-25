using TomasAI.IFM.Domain.MarketData.Shared;
using MessagePack;
using Newtonsoft.Json;
using FluentValidation;
using FluentValidation.Results;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

/// <summary>
/// Provides FluentValidation rules for <see cref="FuturesOptionTickDataV2ReadModel"/> instances.
/// </summary>
/// <remarks>
/// Validates futures option tick data ensuring all required fields are present and valid.
/// Does NOT check for null reference value (per requirements).
/// </remarks>
public class FuturesOptionTickDataV2ReadModelValidationRules : BaseValidationRules, IValidationRules<FuturesOptionTickDataV2ReadModel>
{
    public const string InstanceErrorMessage = "FuturesOptionTickDataV2ReadModel instance is null";
    public const string ContractIdErrorMessage = "FuturesOptionTickDataV2ReadModel.ContractId is required";
    public const string ValueDateErrorMessage = "FuturesOptionTickDataV2ReadModel.ValueDate is invalid";
    public const string TickIdErrorMessage = "FuturesOptionTickDataV2ReadModel.TickId must be greater than zero";
    public const string TickTimeErrorMessage = "FuturesOptionTickDataV2ReadModel.TickTime is invalid";
    public const string OptionPriceErrorMessage = "FuturesOptionTickDataV2ReadModel.OptionPrice must be non-negative";
    public const string BidPriceErrorMessage = "FuturesOptionTickDataV2ReadModel.BidPrice must be non-negative";
    public const string AskPriceErrorMessage = "FuturesOptionTickDataV2ReadModel.AskPrice must be non-negative";
    public const string BidSizeErrorMessage = "FuturesOptionTickDataV2ReadModel.BidSize must be non-negative";
    public const string AskSizeErrorMessage = "FuturesOptionTickDataV2ReadModel.AskSize must be non-negative";
    public const string ImpliedVolatilityErrorMessage = "FuturesOptionTickDataV2ReadModel.ImpliedVolatility is invalid";
    public const string UnderlyingPriceErrorMessage = "FuturesOptionTickDataV2ReadModel.UnderlyingPrice must be non-negative";
    public const string DeltaErrorMessage = "FuturesOptionTickDataV2ReadModel.Delta is invalid";
    public const string GammaErrorMessage = "FuturesOptionTickDataV2ReadModel.Gamma is invalid";
    public const string VegaErrorMessage = "FuturesOptionTickDataV2ReadModel.Vega is invalid";
    public const string ThetaErrorMessage = "FuturesOptionTickDataV2ReadModel.Theta is invalid";
    public const string RhoErrorMessage = "FuturesOptionTickDataV2ReadModel.Rho is invalid";

    /// <summary>
    /// Executes validation rules against the specified FuturesOptionTickDataV2ReadModel instance.
    /// </summary>
    /// <param name="futuresOptionTickData">The futures option tick data to validate.</param>
    /// <returns>An array of validation errors, or an empty array if validation passes.</returns>
    public ValidationError[] Execute(FuturesOptionTickDataV2ReadModel futuresOptionTickData)
        => Validate(futuresOptionTickData, new FuturesOptionTickDataV2ReadModelValidator());

    /// <summary>
    /// Internal FluentValidation validator for FuturesOptionTickDataV2ReadModel.
    /// </summary>
    sealed class FuturesOptionTickDataV2ReadModelValidator : AbstractValidator<FuturesOptionTickDataV2ReadModel>
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051", Justification = "Keep related reference qualification invariants together so the accepted payload requirements can be audited as one validation step.")]
        public FuturesOptionTickDataV2ReadModelValidator()
        {
            // ContractId validation
            RuleFor(x => x.ContractId)
                .NotEmpty()
                .WithMessage(ContractIdErrorMessage);

            // ValueDate validation
            RuleFor(x => x.ValueDate)
                .Must(vd => vd != DateOnly.MinValue && vd != DateOnly.MaxValue)
                .WithMessage(ValueDateErrorMessage);

            // TickId validation
            RuleFor(x => x.TickId)
                .GreaterThan(0)
                .WithMessage(TickIdErrorMessage);

            // TickTime validation
            RuleFor(x => x.TickTime)
                .Must(tt => tt != TimeOnly.MinValue && tt != TimeOnly.MaxValue)
                .WithMessage(TickTimeErrorMessage);

            // OptionPrice validation
            RuleFor(x => x.OptionPrice)
                .Must(p => !double.IsNaN(p) && !double.IsInfinity(p) && p >= 0)
                .WithMessage(OptionPriceErrorMessage);

            // BidPrice validation
            RuleFor(x => x.BidPrice)
                .Must(p => !double.IsNaN(p) && !double.IsInfinity(p) && p >= 0)
                .WithMessage(BidPriceErrorMessage);

            // AskPrice validation
            RuleFor(x => x.AskPrice)
                .Must(p => !double.IsNaN(p) && !double.IsInfinity(p) && p >= 0)
                .WithMessage(AskPriceErrorMessage);

            // BidSize validation
            RuleFor(x => x.BidSize)
                .GreaterThanOrEqualTo(0)
                .WithMessage(BidSizeErrorMessage);

            // AskSize validation
            RuleFor(x => x.AskSize)
                .GreaterThanOrEqualTo(0)
                .WithMessage(AskSizeErrorMessage);

            // ImpliedVolatility validation
            RuleFor(x => x.ImpliedVolatility)
                .Must(iv => !double.IsNaN(iv) && !double.IsInfinity(iv))
                .WithMessage(ImpliedVolatilityErrorMessage);

            // UnderlyingPrice validation
            RuleFor(x => x.UnderlyingPrice)
                .Must(p => !double.IsNaN(p) && !double.IsInfinity(p) && p >= 0)
                .WithMessage(UnderlyingPriceErrorMessage);

            // Delta validation
            RuleFor(x => x.Delta)
                .Must(d => !double.IsNaN(d) && !double.IsInfinity(d))
                .WithMessage(DeltaErrorMessage);

            // Gamma validation
            RuleFor(x => x.Gamma)
                .Must(g => !double.IsNaN(g) && !double.IsInfinity(g))
                .WithMessage(GammaErrorMessage);

            // Vega validation
            RuleFor(x => x.Vega)
                .Must(v => !double.IsNaN(v) && !double.IsInfinity(v))
                .WithMessage(VegaErrorMessage);

            // Theta validation
            RuleFor(x => x.Theta)
                .Must(t => !double.IsNaN(t) && !double.IsInfinity(t))
                .WithMessage(ThetaErrorMessage);

            // Rho validation
            RuleFor(x => x.Rho)
                .Must(r => !double.IsNaN(r) && !double.IsInfinity(r))
                .WithMessage(RhoErrorMessage);
        }

        /// <summary>
        /// Overrides the base validation to ensure the instance is not null before validating properties.
        /// </summary>
        /// <param name="context">The validation context.</param>
        /// <returns>The validation result.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0015", Justification = "The existing model/command Validate method validates object properties, not method parameters. Preserve ArgumentException and property-specific diagnostic labels consumed by validation/error adapters.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "Validation collects failure details instead of throwing; compatibility lookup helpers preserve their documented fallback when input data is unavailable.")]
        public override ValidationResult Validate(ValidationContext<FuturesOptionTickDataV2ReadModel> context)
        {
            try
            {
                ArgumentNullException.ThrowIfNull(context.InstanceToValidate);
            }
            catch
            {
                var validationResult = new ValidationResult();
                validationResult.Errors.Add(new ValidationFailure("FuturesOptionTickDataV2ReadModel", InstanceErrorMessage));
                return validationResult;
            }
            return base.Validate(context);
        }
    }
}
