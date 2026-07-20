using MessagePack;
using Newtonsoft.Json;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.MarketDataAnalytics;

/// <summary>
/// Represents a unique identifier for a MACD signal associated with a specific futures contract, including the contract
/// ID, value date, and time period type.
/// </summary>
/// <remarks>This record is designed for use with MessagePack serialization and provides methods for explicit
/// creation, stable string formatting, and compact JSON serialization. The identifier enables consistent referencing
/// and storage of MACD signal entities for futures contracts across analytics and data processing systems.</remarks>
[MessagePackObject(AllowPrivate = true)]
public record FuturesMacdSignalEntityId : IActorEntityId
{
    /// <summary>Futures contract identifier (root + month/year code).</summary>
    [Key(0)]
    public string ContractId { get; init; }

    /// <summary>Value (trading) date for the MACD signal.</summary>
    [Key(1)]
    public DateOnly ValueDate { get; init; }

    [Key(2)]
    public TradeTimePeriodType TimePeriod {  get; init; }

    [Key(3)]
    public int PeriodLength { get; init; }

    /// <summary>
    /// Parameterless constructor required for MessagePack and some serializers.
    /// </summary>
    public FuturesMacdSignalEntityId() { }

    /// <summary>
    /// Initializes a new <see cref="FuturesMacdSignalEntityId"/>.
    /// </summary>
    /// <param name="contractId">Futures contract identifier.</param>
    /// <param name="valueDate">Value date.</param>
    /// <param name="timePeriod">Time period type.</param>
    /// <param name="periodLength">Length of the time period.</param>
    public FuturesMacdSignalEntityId(string contractId, DateOnly valueDate, TradeTimePeriodType timePeriod, int periodLength)
    {
        ContractId = contractId;
        ValueDate = valueDate;
        TimePeriod = timePeriod;
        PeriodLength = periodLength;
    }

    /// <summary>
    /// Factory method for explicit creation.
    /// </summary>
    public static FuturesMacdSignalEntityId Create(string contractId, DateOnly valueDate, TradeTimePeriodType timePeriod, int periodLength) => new(contractId, valueDate, timePeriod, periodLength);

    /// <summary>
    /// Formats the identifier into a stable string key: ContractId.yyyyMMdd.TimePeriod.PeriodLength
    /// </summary>
    public string Format() => string.Create(null, stackalloc char[80], $"{ContractId}.{ValueDate:yyyyMMdd}.{TimePeriod}.{PeriodLength}");

    /// <summary>
    /// Returns a compact JSON representation.
    /// </summary>
    public override string ToString() => JsonConvert.SerializeObject(this, Formatting.None);
}
