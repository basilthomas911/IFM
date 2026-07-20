using FluentValidation;
using FluentValidation.Results;
using MessagePack;
using Newtonsoft.Json;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;

/// <summary>
/// Represents a computed RSI (Relative Strength Index) signal for a futures contract at a specific
/// value date and intraday timestamp, including intermediate gain/loss averages and slope metrics.
/// </summary>
/// <remarks>
/// MessagePack serializable. Only primitive/enumeration fields are serialized. Derived identifier
/// properties are excluded. Follows the same serialization pattern as other view models
/// (e.g. <c>FundOrderReadModel</c>).
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record FuturesRsiSignalReadModel
{
    /// <summary>Full futures contract identifier (root + month/year code).</summary>
    [Key(0)]
    public string ContractId { get; init; }

    /// <summary>Trading / value date for which this RSI signal applies.</summary>
    [Key(1)]
    public DateOnly ValueDate { get; init; }

    [Key(3)]
    public TradeTimePeriodType TimePeriod { get; init; }

    [Key(4)]
    public int PeriodLength { get; init; }

    /// <summary>Intraday timestamp (time component) when the signal was generated.</summary>
    [Key(5)]
    public TimeOnly Timestamp { get; init; }

    /// <summary>Classification of the RSI signal granularity (e.g. Daily, OneMinute).</summary>

    /// <summary>Price used as the basis for the RSI calculation at this timestamp.</summary>
    [Key(6)]
    public decimal Price { get; init; }

    /// <summary>Price change since the prior interval.</summary>
    [Key(7)]
    public decimal PriceChange { get; init; }

    /// <summary>Positive component of price change (gains only).</summary>
    [Key(8)]
    public decimal PriceGain { get; init; }

    /// <summary>Negative component of price change (losses only, expressed as positive magnitude).</summary>
    [Key(9)]
    public decimal PriceLoss { get; init; }

    /// <summary>Smoothed average gain over the RSI window.</summary>
    [Key(10)]
    public decimal AveragePriceGain { get; init; }

    /// <summary>Smoothed average loss over the RSI window.</summary>
    [Key(11)]
    public decimal AveragePriceLoss { get; init; }

    /// <summary>Relative Strength (RS = AvgGain / AvgLoss).</summary>
    [Key(12)]
    public double RS { get; init; }

    /// <summary>Relative Strength Index value.</summary>
    [Key(13)]
    public double RSI { get; init; }

    /// <summary>Smoothed average of RSI values (if a secondary smoothing is applied).</summary>
    [Key(14)]
    public double RSIAverage { get; init; }

    /// <summary>Slope (rate of change) of RSI over a trailing sub-window.</summary>
    [Key(15)]
    public double RSISlope { get; init; }

    /// <summary>
    /// Entity identifier consisting of contract id and value date (not serialized).
    /// </summary>
    [JsonIgnore]
    [IgnoreMember]
    public FuturesRsiSignalEntityId EntityId => new(ContractId , ValueDate, TimePeriod, PeriodLength);

    /// <summary>
    /// Full signal identifier including timestamp (not serialized).
    /// </summary>
    [JsonIgnore]
    [IgnoreMember]
    public FuturesRsiSignalId Id => new(ContractId , ValueDate, TimePeriod,  PeriodLength, Timestamp);

    /// <summary>
    /// Parameterless constructor required for MessagePack and some serializers.
    /// </summary>
    public FuturesRsiSignalReadModel() { }

    /// <summary>
    /// Full constructor initializing all serialized RSI signal properties.
    /// </summary>
    /// <param name="contractId">Futures contract identifier.</param>
    /// <param name="valueDate">Value date for the signal.</param>
    /// <param name="timestamp">Intraday timestamp.</param>
    /// <param name="signalType">Signal classification type.</param>
    /// <param name="price">Price at the signal time.</param>
    /// <param name="priceChange">Price change since prior period.</param>
    /// <param name="priceGain">Positive price change (gains).</param>
    /// <param name="priceLoss">Negative price change magnitude (losses).</param>
    /// <param name="averagePriceGain">Smoothed average gain.</param>
    /// <param name="averagePriceLoss">Smoothed average loss.</param>
    /// <param name="rs">Relative Strength.</param>
    /// <param name="rsi">RSI value.</param>
    /// <param name="rsiAverage">Smoothed RSI average.</param>
    /// <param name="rsiSlope">RSI slope.</param>
    /// <param name="windowSize">RSI window length.</param>
    public FuturesRsiSignalReadModel(
        string contractId,
        DateOnly valueDate,
        TradeTimePeriodType timePeriod,
        int periodLength,
        TimeOnly timestamp,
        decimal price,
        decimal priceChange,
        decimal priceGain,
        decimal priceLoss,
        decimal averagePriceGain,
        decimal averagePriceLoss,
        double rs,
        double rsi,
        double rsiAverage,
        double rsiSlope)
    {
        ContractId = contractId;
        ValueDate = valueDate;
        Timestamp = timestamp;
        TimePeriod = timePeriod;
        PeriodLength = periodLength;
        Price = price;
        PriceChange = priceChange;
        PriceGain = priceGain;
        PriceLoss = priceLoss;
        AveragePriceGain = averagePriceGain;
        AveragePriceLoss = averagePriceLoss;
        RS = rs;
        RSI = rsi;
        RSIAverage = rsiAverage;
        RSISlope = rsiSlope;
    }

    /// <summary>
    /// Returns a compact JSON representation (diagnostics / logging).
    /// </summary>
    public override string ToString() => JsonConvert.SerializeObject(this, Formatting.None);
}

/// <summary>
/// Validation rules for <see cref="FuturesRsiSignalReadModel"/>.
/// </summary>
public class FuturesRsiSignalReadModelValidationRules : BaseValidationRules, IValidationRules<FuturesRsiSignalReadModel>
{
    public ValidationError[] Execute(FuturesRsiSignalReadModel futuresRsiSignal) => Validate(futuresRsiSignal, new FuturesRsiSignalValidator());

    private class FuturesRsiSignalValidator : AbstractValidator<FuturesRsiSignalReadModel>
    {
        public FuturesRsiSignalValidator()
        {
            RuleFor(x => x.ContractId).NotEmpty().WithMessage("FuturesRsiSignal.ContractId is required");
            RuleFor(x => x.ValueDate).NotEqual(DateOnly.MinValue).WithMessage("FuturesRsiSignal.ValueDate must be greater than DateOnly.MinValue");
            RuleFor(x => x.ValueDate).NotEqual(DateOnly.MaxValue).WithMessage("FuturesRsiSignal.ValueDate must be less than DateOnly.MaxValue");
            RuleFor(x => x.Timestamp).NotEqual(TimeOnly.MinValue).WithMessage("FuturesRsiSignal.Timestamp must be greater than TimeOnly.MinValue");
            RuleFor(x => x.Timestamp).NotEqual(TimeOnly.MaxValue).WithMessage("FuturesRsiSignal.Timestamp must be less than TimeOnly.MaxValue");
            RuleFor(x => x.RS).Must(rs => !double.IsNaN(rs)).WithMessage("FuturesRsiSignal.RS is NaN");
            RuleFor(x => x.RSI).Must(rsi => !double.IsNaN(rsi)).WithMessage("FuturesRsiSignal.RSI is NaN");
            RuleFor(x => x.RSIAverage).Must(rsiAverage => !double.IsNaN(rsiAverage)).WithMessage("FuturesRsiSignal.RSIAverage is NaN");
            RuleFor(x => x.RSISlope).Must(rsiSlope => !double.IsNaN(rsiSlope)).WithMessage("FuturesRsiSignal.RSISlope is NaN");
        }

        public override ValidationResult Validate(ValidationContext<FuturesRsiSignalReadModel> context)
        {
            try
            {
                ArgumentNullException.ThrowIfNull(context.InstanceToValidate);
            }
            catch
            {
                var validationResult = new ValidationResult();
                validationResult.Errors.Add(new ValidationFailure("FuturesRsiSignal", "FuturesRsiSignal instance is null"));
                return validationResult;
            }
            return base.Validate(context);
        }
    }
}