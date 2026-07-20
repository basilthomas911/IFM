using MessagePack;
using Newtonsoft.Json;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.MarketDataAnalytics;

/// <summary>
/// Unique identifier for a futures RSI (Relative Strength Index) signal composed of a contract identifier,
/// a value date, and an intraday timestamp component.
/// </summary>
/// <remarks>
/// MessagePack serializable (primitive components only). Implements <see cref="IActorEntityId"/> with a dot-separated
/// format: ContractId.yyyy-MM-dd.HH:mm:ss. Use <see cref="Create(string, DateOnly, TimeOnly)"/> for explicit construction.
/// Instances are immutable.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record FuturesRsiSignalId : IActorEntityId
{
    /// <summary>Futures contract identifier (root + contract month/year code).</summary>
    [Key(0)]
    public string ContractId { get; init; }
    [Key(1)]
    public DateOnly ValueDate { get; init; }
    /// <summary>Value date associated with the RSI signal.</summary>
    [Key(2)]
    public TradeTimePeriodType TimePeriod { get; init; }
    [Key(3)]
    public int PeriodLength { get; init; }
    /// <summary>Intraday timestamp (time component) when the signal was generated.</summary>
    [Key(4)]
    public TimeOnly Timestamp { get; init; }

    /// <summary>
    /// Parameterless constructor required for MessagePack and some serializers.
    /// </summary>
    public FuturesRsiSignalId() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="FuturesRsiSignalId"/> record with the specified components.
    /// </summary>
    /// <param name="contractId">Full futures contract identifier.</param>
    /// <param name="valueDate">Value (trading) date.</param>
    /// <param name="timePeriod">Time period type.</param>
    /// <param name="periodLength">Length of the period.</param>
    /// <param name="timestamp">Intraday timestamp component.</param>
    public FuturesRsiSignalId(string contractId, DateOnly valueDate, TradeTimePeriodType timePeriod, int periodLength, TimeOnly timestamp)
    {
        ContractId = contractId;
        ValueDate = valueDate;
        TimePeriod = timePeriod;
        PeriodLength = periodLength;
        Timestamp = timestamp;
    }

    /// <summary>
    /// Factory method for creating a new identifier instance.
    /// </summary>
    public static FuturesRsiSignalId Create(string contractId, DateOnly valueDate, TradeTimePeriodType timePeriod, int periodLength, TimeOnly timestamp)
        => new(contractId, valueDate, timePeriod, periodLength, timestamp);

    /// <summary>
    /// Formats the identifier as a dot-separated string: ContractId.TimePeriod.PeriodLength.ValueDate.Timestamp.
    /// </summary>
    /// <returns></returns>
    public string Format() => string.Create(null, stackalloc char[80], $"{ContractId}.{ValueDate:yyyyMMdd}.{TimePeriod}.{PeriodLength}.{Timestamp:HH\\:mm\\:ss}");

    /// <summary>
    /// Returns a compact JSON representation of the identifier.
    /// </summary>
    public override string ToString() => JsonConvert.SerializeObject(this, Formatting.None);

    public FuturesRsiSignalEntityId ToEntityId() => new(ContractId, ValueDate, TimePeriod, PeriodLength);
    public FuturesRsiDailySignalEntityId ToEntityDailyId() => new(ContractId, TimePeriod, PeriodLength);
   
}
