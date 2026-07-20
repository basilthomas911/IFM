using FluentValidation;
using FluentValidation.Results;
using MessagePack;
using Newtonsoft.Json;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Shared.MarketDataFeed.ViewModels;

/// <summary>
/// Represents a futures bar (OHLCV-like) data point enriched with model triggers,
/// suitable for storage and transport in analytics and feed bounded contexts.
/// </summary>
/// <remarks>
/// MessagePack serializable. Only primitive and enum members are serialized. The derived identifier
/// <see cref="Id"/> is excluded from serialization.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record FuturesBarDataReadModel
{
    /// <summary>Full futures contract identifier (root + contract month/year code).</summary>
    [Key(0)]
    public string ContractId { get; init; }

    /// <summary>Symbol root associated with the contract (e.g., ES, NQ, CL).</summary>
    [Key(1)]
    public string Symbol { get; init; }

    /// <summary>Value (trading) date the bar belongs to.</summary>
    [Key(2)]
    public DateOnly ValueDate { get; init; }

    /// <summary>Actual timestamp of the bar within the trading session.</summary>
    [Key(3)]
    public DateTime BarDate { get; init; }

    /// <summary>Granularity/rate of the bar (e.g., Minute).</summary>
    [Key(4)]
    public BarRateType BarRateType { get; init; }

    /// <summary>Numeric value of the bar (e.g., close or aggregated metric).</summary>
    [Key(5)]
    public decimal BarValue { get; init; }

    /// <summary>Computed trigger threshold for initiating an up-trend action.</summary>
    [Key(6)]
    public double UpTrendTrigger { get; init; }

    /// <summary>Computed trigger threshold for initiating a down-trend action.</summary>
    [Key(7)]
    public double DownTrendTrigger { get; init; }

    /// <summary>
    /// Composite identifier (ContractId + Symbol + ValueDate). Not serialized.
    /// </summary>
    [JsonIgnore]
    [IgnoreMember]
    public FuturesBarDataId Id => new(ContractId ?? string.Empty, Symbol ?? string.Empty, ValueDate);

    /// <summary>
    /// Parameterless constructor required for MessagePack and tooling.
    /// </summary>
    public FuturesBarDataReadModel() { }

    /// <summary>
    /// Full constructor initializing all serialized properties.
    /// </summary>
    /// <param name="contractId">Full futures contract identifier.</param>
    /// <param name="symbol">Symbol root.</param>
    /// <param name="valueDate">Trading date of the bar.</param>
    /// <param name="barDate">Timestamp of the bar within the session.</param>
    /// <param name="barRateType">Bar granularity.</param>
    /// <param name="barValue">Bar value (e.g., close).</param>
    /// <param name="upTrendTrigger">Up-trend trigger threshold.</param>
    /// <param name="downTrendTrigger">Down-trend trigger threshold.</param>
    public FuturesBarDataReadModel(
        string contractId,
        string symbol,
        DateOnly valueDate,
        DateTime barDate,
        BarRateType barRateType,
        decimal barValue,
        double upTrendTrigger,
        double downTrendTrigger)
    {
        ContractId = contractId;
        Symbol = symbol;
        ValueDate = valueDate;
        BarDate = barDate;
        BarRateType = barRateType;
        BarValue = barValue;
        UpTrendTrigger = upTrendTrigger;
        DownTrendTrigger = downTrendTrigger;
    }

    /// <summary>
    /// Returns a compact JSON representation (diagnostics/logging).
    /// </summary>
    public override string ToString() => JsonConvert.SerializeObject(this, Formatting.None);
}

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
    private class FuturesBarDataReadModelValidator : AbstractValidator<FuturesBarDataReadModel>
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
            try
            {
                ArgumentNullException.ThrowIfNull(context.InstanceToValidate);
            }
            catch 
            {
                var validationResult = new ValidationResult();
                validationResult.Errors.Add(new ValidationFailure("FuturesBarData", "FuturesBarData instance is null"));
                return validationResult;
            }
            return base.Validate(context);
        }
    }
}