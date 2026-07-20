using FluentValidation;
using FluentValidation.Results;
using MessagePack;
using Newtonsoft.Json;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;

/// <summary>
/// Represents a computed TDI (Trend Direction/Divergence Index) signal for a futures contract
/// at a specific value date and intraday timestamp, including regime persistence counters.
/// </summary>
/// <remarks>
/// MessagePack serializable. Only primitive/enumeration fields are serialized. Derived identifier
/// properties are excluded via <see cref="IgnoreMemberAttribute"/>. Follows the same pattern as
/// <c>FundOrderReadModel</c>.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record FuturesTdiSignalReadModel
{
    /// <summary>Full futures contract identifier (root + contract month/year code).</summary>
    [Key(0)]
    public string ContractId { get; init; }

    /// <summary>Trading/value date for which this TDI signal applies.</summary>
    [Key(1)]
    public DateOnly ValueDate { get; init; }

    [Key(2)]
    public TradeTimePeriodType TimePeriod { get; init; }

    /// <summary>Intraday timestamp (time component) when the signal was generated.</summary>
    [Key(3)]
    public TimeOnly Timestamp { get; init; }

    /// <summary>Consecutive up-trend count (coastline persistence metric).</summary>
    [Key(4)]
    public int UpTrendCount { get; init; }

    /// <summary>Consecutive down-trend count (coastline persistence metric).</summary>
    [Key(5)]
    public int DownTrendCount { get; init; }

    /// <summary>Computed trend direction (e.g., UpTrending, DownTrending, Reversal).</summary>
    [Key(6)]
    public FuturesTrendDirectionType TDI { get; init; }

    /// <summary>Strength of the computed trend direction (e.g., Low, Medium, High).</summary>
    [Key(7)]
    public FuturesTrendDirectionStrengthType TDIStrength { get; init; }

    /// <summary>
    /// Entity identifier consisting of contract id and value date (not serialized).
    /// </summary>
    [JsonIgnore]
    [IgnoreMember]
    public FuturesTdiSignalEntityId EntityId => new(ContractId ?? string.Empty, ValueDate, TimePeriod);

    /// <summary>
    /// Full signal identifier including timestamp (not serialized).
    /// </summary>
    [JsonIgnore]
    [IgnoreMember]
    public FuturesTdiSignalId Id => new(ContractId ?? string.Empty, ValueDate, Timestamp);

    /// <summary>
    /// Parameterless constructor required for MessagePack and tooling.
    /// </summary>
    public FuturesTdiSignalReadModel() { }

    /// <summary>
    /// Full constructor initializing all serialized TDI signal properties.
    /// </summary>
    /// <param name="contractId">Futures contract identifier.</param>
    /// <param name="valueDate">Value date for the signal.</param>
    /// <param name="timePeriod">Time period for the signal.</param>
    /// <param name="timestamp">Intraday timestamp.</param>
    /// <param name="upTrendCount">Consecutive up-trend count.</param>
    /// <param name="downTrendCount">Consecutive down-trend count.</param>
    /// <param name="tdi">Computed trend direction.</param>
    /// <param name="tdiStrength">Strength of the trend direction.</param>
    public FuturesTdiSignalReadModel(
        string contractId,
        DateOnly valueDate,
        TradeTimePeriodType timePeriod,
        TimeOnly timestamp,
        int upTrendCount,
        int downTrendCount,
        FuturesTrendDirectionType tdi,
        FuturesTrendDirectionStrengthType tdiStrength)
    {
        ContractId = contractId;
        ValueDate = valueDate;
        TimePeriod = timePeriod;
        Timestamp = timestamp;
        UpTrendCount = upTrendCount;
        DownTrendCount = downTrendCount;
        TDI = tdi;
        TDIStrength = tdiStrength;
    }

    /// <summary>
    /// Returns a compact JSON representation (for diagnostics/logging).
    /// </summary>
    public override string ToString() => JsonConvert.SerializeObject(this, Formatting.None);
}

/// <summary>
/// Provides FluentValidation rules for <see cref="FuturesTdiSignalReadModel"/> instances.
/// </summary>
/// <remarks>
/// Validates TDI signal data ensuring all required fields are present, valid, and consistent
/// with business rules for trend direction indicators.
/// </remarks>
public class FuturesTdiSignalReadModelValidationRules : BaseValidationRules, IValidationRules<FuturesTdiSignalReadModel>
{
    /// <summary>
    /// Executes validation rules against the specified FuturesTdiSignalReadModel instance.
    /// </summary>
    /// <param name="futuresTdiSignal">The TDI signal read model to validate.</param>
    /// <returns>An array of validation errors, or an empty array if validation passes.</returns>
    public ValidationError[] Execute(FuturesTdiSignalReadModel futuresTdiSignal) 
        => Validate(futuresTdiSignal, new FuturesTdiSignalReadModelValidator());

    /// <summary>
    /// Internal FluentValidation validator for FuturesTdiSignalReadModel.
    /// </summary>
    private class FuturesTdiSignalReadModelValidator : AbstractValidator<FuturesTdiSignalReadModel>
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
                .IsInEnum()
                .WithMessage("FuturesTdiSignal.TimePeriod is invalid");

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

            // TDI (trend direction) validation
            RuleFor(x => x.TDI)
                .IsInEnum()
                .WithMessage("FuturesTdiSignal.TDI is invalid");

            // TDIStrength validation
            RuleFor(x => x.TDIStrength)
                .IsInEnum()
                .WithMessage("FuturesTdiSignal.TDIStrength is invalid");
        }

        /// <summary>
        /// Overrides the base validation to ensure the instance is not null before validating properties.
        /// </summary>
        /// <param name="context">The validation context.</param>
        /// <returns>The validation result.</returns>
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