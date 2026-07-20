using MessagePack;
using Newtonsoft.Json;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.MarketDataAnalytics;

/// <summary>
/// Represents a unique identifier for a futures Average True Range (ATR) signal, encapsulating the contract identifier,
/// value date, time period, and ATR signal source type.
/// </summary>
/// <remarks>This record is designed for use with MessagePack serialization and provides a factory method for
/// explicit creation. The identifier can be formatted into a stable string key and serialized into a compact JSON
/// representation.</remarks>
[MessagePackObject(AllowPrivate = true)]
public record FuturesAtrDailySignalEntityId : IActorEntityId
{
    /// <summary>Futures contract identifier (root + month/year code).</summary>
    [Key(0)]
    public string ContractId { get; init; }

    /// <summary>Value (trading) date for the ATR signal.</summary>

    [Key(1)]
    public TradeTimePeriodType TimePeriod {  get; init; }

    [Key(2)]
    public int  PeriodLength { get; init; }

    /// <summary>
    /// Parameterless constructor required for MessagePack and some serializers.
    /// </summary>
    public FuturesAtrDailySignalEntityId() { }

    /// <summary>
    /// Initializes a new <see cref="FuturesAtrDailySignalEntityId"/>.
    /// </summary>
    /// <param name="contractId">Futures contract identifier.</param>
    /// <param name="timePeriod">Time period type.</param>
    /// <param name="periodLength">ATR signal source type.</param>
    public FuturesAtrDailySignalEntityId(string contractId, TradeTimePeriodType timePeriod, int periodLength)            
    {
        ContractId = contractId;
        TimePeriod = timePeriod;
        PeriodLength = periodLength;
    }

    /// <summary>
    /// Factory method for explicit creation.
    /// </summary>
    public static FuturesAtrDailySignalEntityId Create(string contractId,  TradeTimePeriodType timePeriod, int periodLength) 
        => new(contractId, timePeriod, periodLength);

    /// <summary>
    /// Formats the identifier into a stable string key: ContractId.yyyyMMdd.TimePeriod
    /// </summary>
    public string Format() => string.Create(null, stackalloc char[96], $"{ContractId}.{TimePeriod}.{PeriodLength}");

    /// <summary>
    /// Returns a compact JSON representation.
    /// </summary>
    public override string ToString() => JsonConvert.SerializeObject(this, Formatting.None);
}
