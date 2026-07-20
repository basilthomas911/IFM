using MessagePack;
using Newtonsoft.Json;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.MarketDataAnalytics;

/// <summary>
/// Unique identifier for a futures trade signal composed of a contract identifier, value date, and time period.
/// </summary>
/// <remarks>
/// MessagePack serializable (primitive components only). Implements <see cref="IActorEntityId"/> with a dot-separated
/// format: ContractId.yyyyMMdd.TimePeriod. Provides factory, formatting, and JSON helpers.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record FuturesTradeSignalEntityId : IActorEntityId
{
    /// <summary>Futures contract identifier (root + month/year code).</summary>
    [Key(0)] public string ContractId { get; init; }

    /// <summary>Value (trading) date for the trade signal.</summary>
    [Key(1)] public DateOnly ValueDate { get; init; }
    /// <summary>Time period granularity for the trade signal.</summary>
    [Key(2)] public TradeTimePeriodType TimePeriod { get; init; }

    /// <summary>
    /// Parameterless constructor required for MessagePack and some serializers.
    /// </summary>
    public FuturesTradeSignalEntityId() { }

    /// <summary>
    /// Initializes a new <see cref="FuturesTradeSignalEntityId"/>.
    /// </summary>
    /// <param name="contractId">Futures contract identifier.</param>
    /// <param name="valueDate">Value date.</param>
    /// <param name="timePeriod">Time period for the trade signal.</param>
    public FuturesTradeSignalEntityId(string contractId, DateOnly valueDate, TradeTimePeriodType timePeriod)
    {
        ContractId = contractId;
        ValueDate = valueDate;
        TimePeriod = timePeriod;
    }

    /// <summary>
    /// Factory method for explicit creation.
    /// </summary>
    public static FuturesTradeSignalEntityId Create(string contractId, DateOnly valueDate, TradeTimePeriodType timePeriod) 
        => new(contractId, valueDate, timePeriod);

    /// <summary>
    /// Formats the identifier into a stable string key: ContractId.yyyyMMdd.TimePeriod
    /// </summary>
    public string Format() => string.Create(null, stackalloc char[80], $"{ContractId}.{ValueDate:yyyyMMdd}.{TimePeriod}");

    /// <summary>
    /// Returns a compact JSON representation.
    /// </summary>
    public override string ToString() => JsonConvert.SerializeObject(this, Formatting.None);
}
