using MessagePack;
using Newtonsoft.Json;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.MarketDataAnalytics;

/// <summary>
/// Represents a unique identifier for a futures ATR signal, encapsulating the contract ID, value date, time period, and
/// timestamp associated with the signal.
/// </summary>
/// <remarks>This record is used to distinguish individual ATR signals for futures contracts. It provides
/// essential information for analyzing market conditions and contract performance, and is compatible with serialization
/// frameworks such as MessagePack. The identifier can be formatted as a stable string key for storage or lookup
/// scenarios.</remarks>
[MessagePackObject(AllowPrivate = true)]
public record FuturesAtrSignalId : IActorEntityId
{
    /// <summary>Futures contract identifier (root + contract month/year code).</summary>
    [Key(0)]
    public string ContractId { get; init; }

    /// <summary>Value date associated with the ATR signal.</summary>
    [Key(1)]
    public DateOnly ValueDate { get; init; }
    
    /// <summary>
    /// Gets the time period for which the futures contract is valid.
    /// </summary>
    /// <remarks>The time period determines the duration over which the contract's terms apply. Understanding
    /// the time period is essential for analyzing contract performance and assessing market conditions.</remarks>
    [Key(2)]
    public TradeTimePeriodType TimePeriod { get; init; } 

    [Key(3)]
    public int PeriodLength { get; init; } 

    /// <summary>Timestamp (intraday time component) linked to the signal generation.</summary>
    [Key(4)]
    public TimeOnly Timestamp { get; init; }

    /// <summary>
    /// Parameterless constructor required for MessagePack and some serializers.
    /// </summary>
    public FuturesAtrSignalId() { }

    /// <summary>
    /// Initializes a new <see cref="FuturesAtrSignalId"/>.
    /// </summary>
    /// <param name="contractId">Futures contract identifier.</param>
    /// <param name="valueDate">Value date of the signal.</param>
    /// <param name="timePeriod">Time period of the signal.</param>
    /// <param name="periodLength">ATR signal source type.</param>   
    /// <param name="timestamp">Intraday timestamp component.</param>
    public FuturesAtrSignalId(string contractId, DateOnly valueDate, TradeTimePeriodType timePeriod, int periodLength,  TimeOnly timestamp)
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
    public static FuturesAtrSignalId Create(string contractId, DateOnly valueDate, TradeTimePeriodType timePeriod, int periodLength, TimeOnly timestamp)
        => new(contractId, valueDate, timePeriod, periodLength, timestamp);

    /// <summary>
    /// Formats the identifier into a stable string key: ContractId.yyyyMMdd.TimePeriod.AtrSignalSource.HH:mm:ss
    /// </summary>
    public string Format() => string.Create(null, stackalloc char[112], $"{ContractId}.{ValueDate:yyyyMMdd}.{TimePeriod}.{PeriodLength}.{Timestamp:HH:mm:ss}");

    /// <summary>
    /// Returns a compact JSON representation.
    /// </summary>
    public override string ToString() => JsonConvert.SerializeObject(this, Formatting.None);

    public FuturesAtrSignalEntityId ToEntityId() 
        => new(ContractId, ValueDate, TimePeriod, PeriodLength);

    public FuturesAtrDailySignalEntityId ToDailyEntityId()
    => new(ContractId, TimePeriod, PeriodLength);

}
