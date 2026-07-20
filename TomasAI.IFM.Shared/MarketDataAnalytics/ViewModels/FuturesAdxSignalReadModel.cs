using MessagePack;
using Newtonsoft.Json;

namespace TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;

/// <summary>
/// Represents a computed ADX (Average Directional Index) signal for a futures contract
/// at a specific value date and intraday timestamp.
/// </summary>
/// <remarks>
/// MessagePack serializable. Only primitive/enumeration fields are serialized. Derived identifier
/// properties are excluded via <see cref="IgnoreMemberAttribute"/>.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record FuturesAdxSignalReadModel
{
    /// <summary>Full futures contract identifier (root + contract month/year code).</summary>
    [Key(0)]
    public string ContractId { get; init; }

    /// <summary>Trading/value date for which this ADX signal applies.</summary>
    [Key(1)]
    public DateOnly ValueDate { get; init; }

    [Key(2)]
    public TradeTimePeriodType TimePeriod { get; init; }
    [Key(3)]
    public int PeriodLength { get; init; }

    /// <summary>Intraday timestamp (time component) when the signal was generated.</summary>
    [Key(4)]
    public TimeOnly Timestamp { get; init; }

    [Key(5)]
    public decimal FuturesPrice { get; init; }

    /// <summary>Plus Directional Indicator (+DI) value.</summary>
    [Key(6)]
    public double PlusDI { get; init; }

    /// <summary>Minus Directional Indicator (-DI) value.</summary>
    [Key(7)]
    public double MinusDI { get; init; }

    /// <summary>Average Directional Index value.</summary>
    [Key(8)]
    public double AdxValue { get; init; }

    /// <summary>Computed trend direction (e.g., UpTrending, DownTrending, Reversal).</summary>
    [Key(9)]
    public FuturesTrendDirectionType ADX { get; init; }

    /// <summary>Strength of the computed trend direction (e.g., Low, Medium, High).</summary>
    [Key(10)]
    public FuturesTrendDirectionStrengthType ADXStrength { get; init; }

    /// <summary>
    /// Entity identifier consisting of contract id, value date, and time period (not serialized).
    /// </summary>
    [JsonIgnore]
    [IgnoreMember]
    public FuturesAdxSignalEntityId EntityId => new(ContractId, ValueDate, TimePeriod, PeriodLength);

    /// <summary>
    /// Full signal identifier including timestamp (not serialized).
    /// </summary>
    [JsonIgnore]
    [IgnoreMember]
    public FuturesAdxSignalId Id => new(ContractId, ValueDate, TimePeriod, PeriodLength, Timestamp);

    /// <summary>
    /// Parameterless constructor required for MessagePack and tooling.
    /// </summary>
    public FuturesAdxSignalReadModel() { }

    /// <summary>
    /// Full constructor initializing all serialized ADX signal properties.
    /// </summary>
    /// <param name="contractId">Futures contract identifier.</param>
    /// <param name="valueDate">Value date for the signal.</param>
    /// <param name="timePeriod">Time period for the signal.</param>
    /// <param name="periodLength">Length of the period.</param>
    /// <param name="timestamp">Intraday timestamp.</param>
    /// <param name="futuresPrice"> </param>
    /// <param name="plusDI">Plus Directional Indicator value.</param>
    /// <param name="minusDI">Minus Directional Indicator value.</param>
    /// <param name="adxValue">Average Directional Index value.</param>
    /// <param name="adx">Computed trend direction.</param>
    /// <param name="adxStrength">Strength of the trend direction.</param>
    public FuturesAdxSignalReadModel(
        string contractId,
        DateOnly valueDate,
        TradeTimePeriodType timePeriod,
        int periodLength,
        TimeOnly timestamp,
        decimal futuresPrice,
        double plusDI,
        double minusDI,
        double adxValue,
        FuturesTrendDirectionType adx,
        FuturesTrendDirectionStrengthType adxStrength)
    {
        ContractId = contractId;
        ValueDate = valueDate;
        TimePeriod = timePeriod;
        PeriodLength = periodLength;
        Timestamp = timestamp;
        FuturesPrice = futuresPrice;
        PlusDI = plusDI;
        MinusDI = minusDI;
        AdxValue = adxValue;
        ADX = adx;
        ADXStrength = adxStrength;
    }

    /// <summary>
    /// Returns a compact JSON representation (for diagnostics/logging).
    /// </summary>
    public override string ToString() => JsonConvert.SerializeObject(this, Formatting.None);
}
