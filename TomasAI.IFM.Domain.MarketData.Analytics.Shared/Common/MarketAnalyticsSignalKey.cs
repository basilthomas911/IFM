using FluentValidation;
using MessagePack;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;

/// <summary>Identifies the calculation family represented by an analytics signal.</summary>
public enum MarketAnalyticsSignalKind : byte
{
    /// <summary>No signal family is identified.</summary>
    Unknown = 0,

    /// <summary>Relative Strength Index.</summary>
    Rsi = 1,

    /// <summary>Average Directional Index.</summary>
    Adx = 2,

    /// <summary>Average True Range.</summary>
    Atr = 3,

    /// <summary>Moving Average Convergence Divergence.</summary>
    Macd = 4,

    /// <summary>Exponential Moving Average.</summary>
    Ema = 5,

    /// <summary>Bollinger Band.</summary>
    BollingerBand = 6,

    /// <summary>Market structure.</summary>
    MarketStructure = 7,

    /// <summary>VX futures term structure.</summary>
    VxTermStructure = 8,

    /// <summary>Volume Weighted Average Price.</summary>
    Vwap = 9,

    /// <summary>Intrinsic Time Indicator.</summary>
    Iti = 10,

    /// <summary>Traders Dynamic Index.</summary>
    Tdi = 11
}

/// <summary>
/// Identifies one configured signal stream without conflating contracts and continuation series.
/// </summary>
[MessagePackObject]
public readonly record struct MarketAnalyticsSignalKey
{
    /// <summary>Gets the market series being calculated.</summary>
    [Key(0)]
    public MarketSeriesIdentity MarketSeriesIdentity { get; init; }

    /// <summary>Gets the signal calculation family.</summary>
    [Key(1)]
    public MarketAnalyticsSignalKind SignalKind { get; init; }

    /// <summary>Gets the signal timeframe.</summary>
    [Key(2)]
    public TimeFrameType TimeFrame { get; init; }

    /// <summary>Gets the immutable calculation-configuration identity.</summary>
    [Key(3)]
    public string CalculationConfigurationId { get; init; }

    /// <summary>Initializes an empty value for serialization.</summary>
    public MarketAnalyticsSignalKey() => CalculationConfigurationId = string.Empty;

    /// <summary>Initializes a configured analytics signal key.</summary>
    /// <param name="marketSeriesIdentity">Specific-contract or continuation identity.</param>
    /// <param name="signalKind">Signal calculation family.</param>
    /// <param name="timeFrame">Signal timeframe.</param>
    /// <param name="calculationConfigurationId">Immutable configuration identity.</param>
    [SerializationConstructor]
    public MarketAnalyticsSignalKey(
        MarketSeriesIdentity marketSeriesIdentity,
        MarketAnalyticsSignalKind signalKind,
        TimeFrameType timeFrame,
        string calculationConfigurationId)
    {
        MarketSeriesIdentity = marketSeriesIdentity;
        SignalKind = signalKind;
        TimeFrame = timeFrame;
        CalculationConfigurationId = calculationConfigurationId ?? string.Empty;
    }

    /// <summary>Formats the configured signal key.</summary>
    /// <returns>A stable identity string.</returns>
    public string Format() =>
        $"{Uri.EscapeDataString(MarketSeriesIdentity.Format())}|{SignalKind}|{TimeFrame}|{Uri.EscapeDataString(CalculationConfigurationId)}";

    /// <summary>Returns the stable formatted key.</summary>
    public override string ToString() => Format();
}

/// <summary>Validates a configured analytics signal key.</summary>
public sealed class MarketAnalyticsSignalKeyValidationRules
    : BaseValidationRules, IValidationStructRules<MarketAnalyticsSignalKey>
{
    static readonly Validator Rules = new();

    /// <summary>Validates the supplied signal key.</summary>
    /// <param name="value">Signal key to validate.</param>
    /// <returns>All validation errors, or an empty array when valid.</returns>
    public ValidationError[] Execute(MarketAnalyticsSignalKey value) => Validate(value, Rules);

    sealed class Validator : AbstractValidator<MarketAnalyticsSignalKey>
    {
        public Validator()
        {
            RuleFor(x => x.MarketSeriesIdentity)
                .Must(x => new MarketSeriesIdentityValidationRules().Execute(x).Length == 0);
            RuleFor(x => x.SignalKind).IsInEnum().NotEqual(MarketAnalyticsSignalKind.Unknown);
            RuleFor(x => x.TimeFrame).IsInEnum().NotEqual(TimeFrameType.None);
            RuleFor(x => x.CalculationConfigurationId).NotEmpty();
        }
    }
}
