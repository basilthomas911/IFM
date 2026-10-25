using FluentValidation;
using FluentValidation.Results;
using MessagePack;
using Newtonsoft.Json;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;

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

    [Key(2)] public TimeFrameType TimePeriod { get; init; }

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

    /// <summary>First observed trading value date in this Daily, Weekly, or Monthly timeframe.</summary>
    [Key(21)] public DateOnly TimeFrameStartValueDate { get; init; }

    /// <summary>Price of the last durable signal from which the publication band is measured.</summary>
    [Key(22)] public double BandAnchorPrice { get; init; }

    /// <summary>Fraction of the calculated ITI threshold used as the publication band.</summary>
    [Key(23)] public double BandPercentage { get; init; }

    /// <summary>Absolute price movement required before another band-qualified signal is durable.</summary>
    [Key(24)] public double BandSize { get; init; }

    /// <summary>
    /// Signed progress from the current direction-change price, expressed as a multiple of the full ITI threshold.
    /// </summary>
    [Key(25)] public double BandLevel { get; init; }

    /// <summary>
    /// Fraction of the established move from the direction-change price to the trend extreme that has been retraced.
    /// </summary>
    [Key(26)] public double ReversalLevel { get; init; }

    /// <summary>Entity identifier (contract + value date). Not serialized.</summary>
    [JsonIgnore]
    [IgnoreMember]
    public FuturesItiSignalEntityId EntityId => FuturesItiSignalEntityId.Create(
        ContractId ?? string.Empty,
        TimeFrameStartValueDate == default ? ValueDate : TimeFrameStartValueDate,
        TimePeriod);

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
        TimeFrameType timePeriod,
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
        IntrinsicTimeTradeState tradeState,
        DateOnly timeFrameStartValueDate = default,
        double bandAnchorPrice = 0,
        double bandPercentage = 0,
        double bandSize = 0,
        double bandLevel = 0,
        double reversalLevel = 0)
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
        TimeFrameStartValueDate = timeFrameStartValueDate;
        BandAnchorPrice = bandAnchorPrice;
        BandPercentage = bandPercentage;
        BandSize = bandSize;
        BandLevel = bandLevel;
        ReversalLevel = reversalLevel;
    }

    /// <summary>
    /// Returns a compact JSON representation (diagnostics/logging).
    /// </summary>
    public override string ToString() => JsonConvert.SerializeObject(this, Formatting.None);

    [JsonIgnore]
    [IgnoreMember]
    public bool IsValid => !string.IsNullOrEmpty(ContractId) && ValueDate > DateOnly.MinValue;
}
