using MessagePack;
using Newtonsoft.Json;

namespace TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;

/// <summary>
/// MessagePack-serializable view model representing a computed futures trade signal snapshot for a contract and value date.
/// </summary>
/// <remarks>
/// Pattern mirrors FundOrderReadModel: explicit properties with sequential MessagePack keys; derived/computed members
/// are excluded via <see cref="IgnoreMemberAttribute"/> and <see cref="JsonIgnoreAttribute"/>.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record FuturesTradeSignalV2ReadModel
{
    /// <summary>Full futures contract identifier.</summary>
    [Key(0)]
    public string ContractId { get; init; }

    /// <summary>Value (trading) date associated with the signal.</summary>
    [Key(1)]
    public DateOnly ValueDate { get; init; }

    /// <summary>Time period classification for the signal.</summary>
    [Key(2)]
    public TradeTimePeriodType TimePeriod { get; init; }

    /// <summary>Monotonic sequence number for ordering signals.</summary>
    [Key(3)]
    public long SequenceId { get; init; }

    /// <summary>Time component when the signal was generated.</summary>
    [Key(4)]
    public TimeOnly Timestamp { get; init; }

    /// <summary>Computed mean (central tendency) of the monitored metric.</summary>
    [Key(5)]
    public double Mean { get; init; }

    /// <summary>Computed standard deviation of the monitored metric.</summary>
    [Key(6)]
    public double StdDev { get; init; }

    /// <summary>Current futures price used in signal calculations.</summary>
    [Key(7)]
    public double FuturesPrice { get; init; }

    /// <summary>Percent change in price from reference point.</summary>
    [Key(8)]
    public double PriceChangePercent { get; init; }

    /// <summary>Allocated fund risk as a percentage.</summary>
    [Key(9)]
    public double FundRiskPercent { get; init; }

    /// <summary>Relative Strength Index (RSI) value.</summary>
    [Key(10)]
    public double RSI { get; init; }

    /// <summary>Slope/change rate of the RSI.</summary>
    [Key(11)]
    public double RSISlope { get; init; }

    /// <summary>Detected trend classification.</summary>
    [Key(12)]
    public FuturesTrendType TrendType { get; init; }

    /// <summary>Strength level of the detected trend.</summary>
    [Key(13)]
    public FuturesTrendStrengthType TrendStrength { get; init; }

    /// <summary>Trade signal (e.g., Buy/Sell/None).</summary>
    [Key(14)]
    public TradeSignalType TradeSignal { get; init; }

    /// <summary>Trend direction indicator (TDI) state.</summary>
    [Key(15)]
    public FuturesTrendDirectionType TDI { get; init; }

    /// <summary>Strength of the TDI reading.</summary>
    [Key(16)]
    public FuturesTrendDirectionStrengthType TDIStrength { get; init; }

    /// <summary>Market Direction Indicator (MDI) primary value.</summary>
    [Key(17)]
    public double MDI { get; init; }

    /// <summary>MDI trend classification.</summary>
    [Key(18)]
    public FuturesMDITrendType MDITrend { get; init; }

    /// <summary>Upper limit threshold for up-trending MDI.</summary>
    [Key(19)]
    public double MDIUpTrendLimit { get; init; }

    /// <summary>Lower limit threshold for down-trending MDI.</summary>
    [Key(20)]
    public double MDIDownTrendLimit { get; init; }

    /// <summary>Trigger level indicating an emerging up trend.</summary>
    [Key(21)]
    public double UpTrendingTrigger { get; init; }

    /// <summary>Trigger level indicating an emerging down trend.</summary>
    [Key(22)]
    public double DownTrendingTrigger { get; init; }

    /// <summary>Entry trigger threshold for trade initiation.</summary>
    [Key(23)]
    public double EntryTrigger { get; init; }

    /// <summary>Exit trigger threshold for trade closure.</summary>
    [Key(24)]
    public double ExitTrigger { get; init; }

    /// <summary>Delta between current trend metric and baseline.</summary>
    [Key(25)]
    public double TrendDelta { get; init; }

    /// <summary>Extreme value detected in the current trend window.</summary>
    [Key(26)]
    public double TrendExtreme { get; init; }

    /// <summary>Trend reversal metric (transition indicator).</summary>
    [Key(27)]
    public double TrendReversal { get; init; }

    /// <summary>50-day moving average price.</summary>
    [Key(28)]
    public decimal FiftyDMA { get; init; }

    /// <summary>200-day moving average price.</summary>
    [Key(29)]
    public decimal TwoHundredDMA { get; init; }

    /// <summary>State indicating trade execution readiness or progression.</summary>
    [Key(30)]
    public TradeExecuteState TradeExecuteState { get; init; }

    /// <summary>
    /// Parameterless constructor for serializers; initializes primitives to defaults.
    /// </summary>
    public FuturesTradeSignalV2ReadModel()
    {
        ContractId = string.Empty;
    }

    /// <summary>
    /// Creates a new futures trade signal snapshot.
    /// </summary>
    public FuturesTradeSignalV2ReadModel(
        string contractId,
        DateOnly valueDate,
        TradeTimePeriodType timePeriod,
        long sequenceId,
        TimeOnly timestamp,
        double mean,
        double stdDev,
        double futuresPrice,
        double priceChangePercent,
        double fundRiskPercent,
        double rsi,
        double rsiSlope,
        FuturesTrendType trendType,
        FuturesTrendStrengthType trendStrength,
        TradeSignalType tradeSignal,
        FuturesTrendDirectionType tdi,
        FuturesTrendDirectionStrengthType tdiStrength,
        double mdi,
        FuturesMDITrendType mdiTrend,
        double mdiUpTrendLimit,
        double mdiDownTrendLimit,
        double upTrendingTrigger,
        double downTrendingTrigger,
        double entryTrigger,
        double exitTrigger,
        double trendDelta,
        double trendExtreme,
        double trendReversal,
        decimal fiftyDMA,
        decimal twoHundredDMA,
        TradeExecuteState tradeExecuteState)
    {
        ContractId = contractId;
        ValueDate = valueDate;
        TimePeriod = timePeriod;
        SequenceId = sequenceId;
        Timestamp = timestamp;
        Mean = mean;
        StdDev = stdDev;
        FuturesPrice = futuresPrice;
        PriceChangePercent = priceChangePercent;
        FundRiskPercent = fundRiskPercent;
        RSI = rsi;
        RSISlope = rsiSlope;
        TrendType = trendType;
        TrendStrength = trendStrength;
        TradeSignal = tradeSignal;
        TDI = tdi;
        TDIStrength = tdiStrength;
        MDI = mdi;
        MDITrend = mdiTrend;
        MDIUpTrendLimit = mdiUpTrendLimit;
        MDIDownTrendLimit = mdiDownTrendLimit;
        UpTrendingTrigger = upTrendingTrigger;
        DownTrendingTrigger = downTrendingTrigger;
        EntryTrigger = entryTrigger;
        ExitTrigger = exitTrigger;
        TrendDelta = trendDelta;
        TrendExtreme = trendExtreme;
        TrendReversal = trendReversal;
        FiftyDMA = fiftyDMA;
        TwoHundredDMA = twoHundredDMA;
        TradeExecuteState = tradeExecuteState;
    }

    [JsonIgnore]
    [IgnoreMember]
    public FuturesTradeSignalId Id => new(ContractId, ValueDate, TimePeriod, SequenceId);

    /// <summary>Derived entity identifier (excluded from MessagePack).</summary>
    [JsonIgnore]
    [IgnoreMember]
    public FuturesTradeSignalEntityId EntityId => new(ContractId, ValueDate, TimePeriod);

    /// <summary>
    /// Indicates whether MDI is within a trailing window watermark (domain-specific, computed externally).
    /// </summary>
    [JsonIgnore]
    [IgnoreMember]
    public bool IsMDIWithinWatermarkTrailingWindow => false;

    [JsonIgnore]
    [IgnoreMember]
    public bool IsValid => !string.IsNullOrEmpty(ContractId) && ValueDate > DateOnly.MinValue;

    /// <summary>
    /// Returns a compact JSON representation of the signal.
    /// </summary>
    public override string ToString() => JsonConvert.SerializeObject(this);
}