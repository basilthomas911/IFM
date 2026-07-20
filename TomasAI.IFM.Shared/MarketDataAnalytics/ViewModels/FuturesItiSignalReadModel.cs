using FluentValidation;
using FluentValidation.Results;
using MessagePack;
using Newtonsoft.Json;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;

/// <summary>
/// Represents a view model for a futures contract's Intrinsic Time Indicator (ITI) signal, encapsulating trading signal
/// data, intrinsic time metrics, and trend analysis information for use in trading applications.
/// </summary>
/// <remarks>This record is designed to facilitate the analysis and visualization of futures ITI signals by
/// aggregating contract identifiers, time-based metrics, price trends, and threshold values relevant to trading
/// strategies. It supports serialization for efficient data transfer and is suitable for use in client applications,
/// analytics dashboards, or automated trading systems. The type provides both a parameterless constructor for
/// deserialization and a full constructor to ensure all key properties are initialized.</remarks>
[MessagePackObject(AllowPrivate = true)]
public record FuturesItiSignalV2ReadModel
{
    /// <summary>Full futures contract identifier (root + month/year).</summary>
    [Key(0)] public string ContractId { get; init; }

    /// <summary>Value (trading) date for the signal.</summary>
    [Key(1)] public DateOnly ValueDate { get; init; }

    [Key(2)] public TradeTimePeriodType TimePeriod { get; init; }

    /// <summary>Sequential intrinsic time sequence identifier.</summary>
    [Key(3)] public long SequenceId { get; init; }

    /// <summary>Absolute intrinsic time (UTC/local as defined upstream).</summary>
    [Key(4)] public DateTime IntrinsicTime { get; init; }

    /// <summary>Grouping identifier for intrinsic time segmentation.</summary>
    [Key(5)] public int IntrinsicTimeGroupId { get; init; }

    /// <summary>Length (duration or span) of the intrinsic time interval.</summary>
    [Key(6)] public double IntrinsicTimeLength { get; init; }

    /// <summary>Intrinsic (normalized) price used in ITI computations.</summary>
    [Key(7)] public double IntrinsicPrice { get; init; }

    /// <summary>Current intrinsic time trend (up or down).</summary>
    [Key(8)] public IntrinsicTimeTrendType IntrinsicTimeTrend { get; init; }

    /// <summary>Current intrinsic time mode (e.g. trend change, reversal).</summary>
    [Key(9)] public IntrinsicTimeModeType IntrinsicTimeMode { get; init; }

    [Key(10)] public double TrendPrice { get; init; }

    /// <summary>Recorded trend extreme price (pivot high/low).</summary>
    [Key(11)] public double TrendExtreme { get; init; }

    /// <summary>Price level indicating a potential trend reversal.</summary>
    [Key(12)] public double TrendReversal { get; init; }

    /// <summary>Predicted price delta for the current trend interval.</summary>
    [Key(13)] public double TrendDelta { get; init; }

    /// <summary>Target price delta for the current trend interval.</summary>
    [Key(14)] public double TargetDelta { get; init; }

    /// <summary>Lambda parameter used in intrinsic/predictive model calculations.</summary>
    [Key(15)] public double Lambda { get; init; }

    /// <summary>Number of trading days used in threshold calculations.</summary>
    [Key(16)] public int TradingDays { get; init; }

    /// <summary>Computed threshold for initiating trend actions.</summary>
    [Key(17)] public double Threshold { get; init; }

    /// <summary>Computed trigger threshold for initiating an up-trend action.</summary>
    [Key(18)] public double UpTrendTrigger { get; init; }

    /// <summary>Computed trigger threshold for initiating a down-trend action.</summary>
    [Key(19)] public double DownTrendTrigger { get; init; }

    /// <summary>Current trade state (ready, hold, placed, opened, closed).</summary>
    [Key(20)] public IntrinsicTimeTradeState TradeState { get; init; }

    /// <summary>Entity identifier (contract + value date). Not serialized.</summary>
    [JsonIgnore]
    [IgnoreMember]
    public FuturesItiSignalEntityId EntityId => FuturesItiSignalEntityId.Create(ContractId ?? string.Empty, ValueDate, TimePeriod);

    /// <summary>Full signal identifier including intrinsic time. Not serialized.</summary>
    [JsonIgnore]
    [IgnoreMember]
    public FuturesItiSignalId Id => FuturesItiSignalId.Create(ContractId ?? string.Empty, ValueDate, TimePeriod, IntrinsicTime);

    /// <summary>Parameterless constructor for MessagePack deserialization.</summary>
    public FuturesItiSignalV2ReadModel() { }

    /// <summary>
    /// Full constructor initializing all serialized ITI signal properties.
    /// </summary>
    public FuturesItiSignalV2ReadModel(
        string contractId,
        DateOnly valueDate,
        TradeTimePeriodType timePeriod,
        long sequenceId,
        DateTime intrinsicTime,
        int intrinsicTimeGroupId,
        double intrinsicTimeLength,
        double intrinsicPrice,
        IntrinsicTimeTrendType intrinsicTimeTrend,
        IntrinsicTimeModeType intrinsicTimeMode,
        double trendPrice,
        double trendExtreme,
        double trendReversal,
        double trendDelta,
        double targetDelta,
        double lambda,
        int tradingDays,
        double threshold,
        double upTrendTrigger,
        double downTrendTrigger,
        IntrinsicTimeTradeState tradeState)
    {
        ContractId = contractId;
        ValueDate = valueDate;
        TimePeriod = timePeriod;
        SequenceId = sequenceId;
        IntrinsicTime = intrinsicTime;
        IntrinsicTimeGroupId = intrinsicTimeGroupId;
        IntrinsicTimeLength = intrinsicTimeLength;
        IntrinsicPrice = intrinsicPrice;
        IntrinsicTimeTrend = intrinsicTimeTrend;
        IntrinsicTimeMode = intrinsicTimeMode;
        TrendPrice = trendPrice;
        TrendExtreme = trendExtreme;
        TrendReversal = trendReversal;
        TrendDelta = trendDelta;
        TargetDelta = targetDelta;
        Lambda = lambda;
        TradingDays = tradingDays;
        Threshold = threshold;
        UpTrendTrigger = upTrendTrigger;
        DownTrendTrigger = downTrendTrigger;
        TradeState = tradeState;
    }

    /// <summary>
    /// Returns a compact JSON representation (diagnostics/logging).
    /// </summary>
    public override string ToString() => JsonConvert.SerializeObject(this, Formatting.None);

    [JsonIgnore]
    [IgnoreMember]
    public bool IsValid => !string.IsNullOrEmpty(ContractId) && ValueDate > DateOnly.MinValue;
}

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
    /// <summary>
    /// Execute validation for the supplied <see cref="FuturesItiSignalV2ReadModel"/>.
    /// </summary>
    /// <param name="futuresItiSignal">The futures ITI signal view model to validate.</param>
    /// <returns>An array of <see cref="ValidationError"/> describing validation failures. Empty if valid.</returns>
    public ValidationError[] Execute(FuturesItiSignalV2ReadModel futuresItiSignal)
        => Validate(futuresItiSignal, new FuturesItiSignalV2Validator());

    /// <summary>
    /// FluentValidation validator describing the rules for <see cref="FuturesItiSignalV2ReadModel"/>.
    /// </summary>
    /// <remarks>
    /// The validator ensures required fields are populated, numeric values are valid (not NaN or Infinity),
    /// enums have valid values, and returns a friendly error when the instance itself is null.
    /// </remarks>
    class FuturesItiSignalV2Validator : AbstractValidator<FuturesItiSignalV2ReadModel>
    {
        /// <summary>
        /// Initializes a new instance of <see cref="FuturesItiSignalV2Validator"/> and configures validation rules.
        /// </summary>
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

            // Enum validation
            RuleFor(x => x.TimePeriod)
                .IsInEnum()
                .NotEqual(TradeTimePeriodType.None)
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