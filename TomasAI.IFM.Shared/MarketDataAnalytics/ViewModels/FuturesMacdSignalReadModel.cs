using MessagePack;
using Newtonsoft.Json;

namespace TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;

/// <summary>
/// Represents a computed MACD (Moving Average Convergence Divergence) signal for a futures contract
/// at a specific value date and intraday timestamp.
/// </summary>
/// <remarks>
/// MessagePack serializable. Only primitive/enumeration fields are serialized. Derived identifier
/// properties are excluded via <see cref="IgnoreMemberAttribute"/>.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record FuturesMacdSignalReadModel
{
    /// <summary>Full futures contract identifier (root + contract month/year code).</summary>
    [Key(0)]
    public string ContractId { get; init; }

    /// <summary>Trading/value date for which this MACD signal applies.</summary>
    [Key(1)]
    public DateOnly ValueDate { get; init; }

    [Key(2)]
    public TradeTimePeriodType TimePeriod { get; init; }

    [Key(3)]
    public int PeriodLength { get; init; }

    /// <summary>Intraday timestamp (time component) when the signal was generated.</summary>
    [Key(4)]
    public TimeOnly Timestamp { get; init; }

    /// <summary>Current futures price.</summary>
    [Key(5)]
    public decimal FuturesPrice { get; init; }

    /// <summary>MACD line value (fast EMA minus slow EMA).</summary>
    [Key(6)]
    public double MacdLine { get; init; }

    /// <summary>Signal line value (EMA of the MACD line).</summary>
    [Key(7)]
    public double SignalLine { get; init; }

    /// <summary>Histogram value (MACD line minus signal line).</summary>
    [Key(8)]
    public double Histogram { get; init; }

    /// <summary>Computed trend direction (e.g., UpTrending, DownTrending, Reversal).</summary>
    [Key(9)]
    public FuturesTrendDirectionType MACD { get; init; }

    /// <summary>Strength of the computed trend direction (e.g., Low, Medium, High).</summary>
    [Key(10)]
    public FuturesTrendDirectionStrengthType MACDStrength { get; init; }

    /// <summary>
    /// Entity identifier consisting of contract id and value date (not serialized).
    /// </summary>
    [JsonIgnore]
    [IgnoreMember]
    public FuturesMacdSignalEntityId EntityId => new(ContractId ?? string.Empty, ValueDate, TimePeriod, PeriodLength);

    /// <summary>
    /// Full signal identifier including timestamp (not serialized).
    /// </summary>
    [JsonIgnore]
    [IgnoreMember]
    public FuturesMacdSignalId Id => new(ContractId ?? string.Empty, ValueDate, TimePeriod, PeriodLength, Timestamp);

    /// <summary>
    /// Parameterless constructor required for MessagePack and tooling.
    /// </summary>
    public FuturesMacdSignalReadModel() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="FuturesMacdSignalReadModel"/> record with specified values.
    /// </summary>
    /// <param name="contractId"></param>
    /// <param name="valueDate"></param>
    /// <param name="timePeriod"></param>
    /// <param name="periodLength"></param>
    /// <param name="timestamp"></param>
    /// <param name="futuresPrice"
    /// <param name="macdLine"></param>
    /// <param name="signalLine"></param>
    /// <param name="histogram"></param>
    /// <param name="macd"></param>
    /// <param name="macdStrength"></param>
    public FuturesMacdSignalReadModel(
        string contractId,
        DateOnly valueDate,
        TradeTimePeriodType timePeriod,
        int periodLength,
        TimeOnly timestamp,
        decimal futuresPrice,
        double macdLine,
        double signalLine,
        double histogram,
        FuturesTrendDirectionType macd,
        FuturesTrendDirectionStrengthType macdStrength)
    {
        ContractId = contractId;
        ValueDate = valueDate;
        TimePeriod = timePeriod;
        PeriodLength = periodLength;    
        Timestamp = timestamp;
        FuturesPrice = futuresPrice;
        MacdLine = macdLine;
        SignalLine = signalLine;
        Histogram = histogram;
        MACD = macd;
        MACDStrength = macdStrength;
    }

    /// <summary>
    /// Returns a compact JSON representation (for diagnostics/logging).
    /// </summary>
    public override string ToString() => JsonConvert.SerializeObject(this, Formatting.None);
}
