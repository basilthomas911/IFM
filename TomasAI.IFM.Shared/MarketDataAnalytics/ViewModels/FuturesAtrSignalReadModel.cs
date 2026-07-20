using MessagePack;
using Newtonsoft.Json;

namespace TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;

/// <summary>
/// Represents a computed ATR (Average True Range) signal for a futures contract
/// at a specific value date and intraday timestamp.
/// </summary>
/// <remarks>
/// MessagePack serializable. Only primitive/enumeration fields are serialized. Derived identifier
/// properties are excluded via <see cref="IgnoreMemberAttribute"/>.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record FuturesAtrSignalReadModel
{
    /// <summary>Full futures contract identifier (root + contract month/year code).</summary>
    [Key(0)]
    public string ContractId { get; init; }

    /// <summary>Trading/value date for which this ATR signal applies.</summary>
    [Key(1)]
    public DateOnly ValueDate { get; init; }

    [Key(2)]
    public TradeTimePeriodType TimePeriod { get; init; }

    [Key(3)]
    public int  PeriodLength { get; init; }

    /// <summary>Intraday timestamp (time component) when the signal was generated.</summary>
    [Key(4)]
    public TimeOnly Timestamp { get; init; }

    [Key(5)]
    public decimal FuturesPrice { get; init; }

    /// <summary>Average True Range value.</summary>
    [Key(6)]
    public double AtrValue { get; init; }

    /// <summary>True Range value for the current period.</summary>
    [Key(7)]
    public double TrueRange { get; init; }

    /// <summary>Computed trend direction (e.g., UpTrending, DownTrending, Reversal).</summary>
    [Key(8)]
    public FuturesTrendDirectionType ATR { get; init; }

    /// <summary>Strength of the computed trend direction (e.g., Low, Medium, High).</summary>
    [Key(9)]
    public FuturesTrendDirectionStrengthType ATRStrength { get; init; }

    /// <summary>
    /// Entity identifier consisting of contract id and value date (not serialized).
    /// </summary>
    [JsonIgnore]
    [IgnoreMember]
    public FuturesAtrSignalEntityId EntityId => new(ContractId, ValueDate, TimePeriod, PeriodLength);

    /// <summary>
    /// Full signal identifier including timestamp (not serialized).
    /// </summary>
    [JsonIgnore]
    [IgnoreMember]
    public FuturesAtrSignalId Id => new(ContractId, ValueDate, TimePeriod, PeriodLength, Timestamp);

    /// <summary>
    /// Parameterless constructor required for MessagePack and tooling.
    /// </summary>
    public FuturesAtrSignalReadModel() { }

    /// <summary>
    /// Full constructor initializing all serialized ATR signal properties.
    /// </summary>
    /// <param name="contractId">Futures contract identifier.</param>
    /// <param name="valueDate">Value date for the signal.</param>
    /// <param name="timePeriod">Time period for the signal.</param>
    /// <param name="atrSignalSource">Source type for the ATR signal.</param>
    /// <param name="timestamp">Intraday timestamp.</param>
    /// <param name="atrValue">Average True Range value.</param>
    /// <param name="trueRange">True Range value for the current period.</param>
    /// <param name="atr">Computed trend direction.</param>
    /// <param name="atrStrength">Strength of the trend direction.</param>
    public FuturesAtrSignalReadModel(
        string contractId,
        DateOnly valueDate,
        TradeTimePeriodType timePeriod,
        int periodLength,
        TimeOnly timestamp,
        decimal futuresPrice,
        double atrValue,
        double trueRange,
        FuturesTrendDirectionType atr,
        FuturesTrendDirectionStrengthType atrStrength)
    {
        ContractId = contractId;
        ValueDate = valueDate;
        TimePeriod = timePeriod;
        PeriodLength = periodLength;
        Timestamp = timestamp;
        FuturesPrice = futuresPrice;
        AtrValue = atrValue;
        TrueRange = trueRange;
        ATR = atr;
        ATRStrength = atrStrength;
    }

    /// <summary>
    /// Returns a compact JSON representation (for diagnostics/logging).
    /// </summary>
    public override string ToString() => JsonConvert.SerializeObject(this, Formatting.None);
}
