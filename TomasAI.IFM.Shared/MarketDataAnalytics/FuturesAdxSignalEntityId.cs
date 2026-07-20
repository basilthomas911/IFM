using MessagePack;
using Newtonsoft.Json;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.MarketDataAnalytics;

/// <summary>
/// Unique identifier for a futures ADX (Average Directional Index) signal composed of a contract identifier,
/// a value (trading) date, and a time period.
/// </summary>
/// <remarks>
/// MessagePack serializable (primitive components only). Implements <see cref="IActorEntityId"/> using a dot-separated
/// format: ContractId.yyyyMMdd.TimePeriod. Provides factory, formatting, and JSON helpers.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record FuturesAdxSignalEntityId : IActorEntityId
{
    /// <summary>Futures contract identifier (root + month/year code).</summary>
    [Key(0)]
    public string ContractId { get; init; }

    [Key(1)]
    public DateOnly ValueDate { get; init; }

    [Key(2)]
    public TradeTimePeriodType TimePeriod { get; init; }

    [Key(3)]
    public int PeriodLength { get; init; }

    /// <summary>
    /// Parameterless constructor required for MessagePack and some serializers.
    /// </summary>
    public FuturesAdxSignalEntityId() { }

    /// <summary>
    /// Initializes a new <see cref="FuturesAdxSignalEntityId"/>.
    /// </summary>
    /// <param name="contractId">Futures contract identifier.</param>
    /// <param name="valueDate">Value date.</param>
    /// <param name="timePeriod">Time period.</param>
    /// <param name="periodLength">Period length.</param>
    public FuturesAdxSignalEntityId(string contractId, DateOnly valueDate, TradeTimePeriodType timePeriod, int periodLength)
    {
        ContractId = contractId;
        ValueDate = valueDate;
        TimePeriod = timePeriod;
        PeriodLength = periodLength;
    }

    /// <summary>
    /// Factory method for explicit creation.
    /// </summary>
    public static FuturesAdxSignalEntityId Create(string contractId, DateOnly valueDate, TradeTimePeriodType timePeriod, int periodLength) 
        => new(contractId, valueDate, timePeriod, periodLength   );

    /// <summary>
    /// Formats the identifier into a stable string key: ContractId.yyyyMMdd
    /// </summary>
    public string Format() => string.Create(null, stackalloc char[64], $"{ContractId}.{ValueDate:yyyyMMdd}.{TimePeriod}.{PeriodLength}");

    /// <summary>
    /// Returns a compact JSON representation.
    /// </summary>
    public override string ToString() => JsonConvert.SerializeObject(this, Formatting.None);
}
