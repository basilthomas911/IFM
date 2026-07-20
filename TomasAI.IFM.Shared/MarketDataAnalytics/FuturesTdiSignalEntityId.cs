using MessagePack;
using Newtonsoft.Json;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.MarketDataAnalytics;

/// <summary>
/// Represents the unique identifier for a futures TDI signal, including the contract identifier, value date, and time
/// period.
/// </summary>
/// <remarks>This record is intended for use with MessagePack serialization and provides methods for formatting
/// and compact JSON representation. It is used to uniquely identify TDI signals for futures contracts based on
/// contract, date, and time period.</remarks>
[MessagePackObject(AllowPrivate = true)]
public record FuturesTdiSignalEntityId : IActorEntityId
{
    /// <summary>Futures contract identifier (root + month/year code).</summary>
    [Key(0)]
    public string ContractId { get; init; }

    /// <summary>Value (trading) date for the TDI signal.</summary>
    [Key(1)]
    public DateOnly ValueDate { get; init; }

    [Key(2)]
    public TradeTimePeriodType TimePeriod { get; init; }

    /// <summary>
    /// Parameterless constructor required for MessagePack and some serializers.
    /// </summary>
    public FuturesTdiSignalEntityId() { }

    /// <summary>
    /// Initializes a new <see cref="FuturesTdiSignalEntityId"/>.
    /// </summary>
    /// <param name="contractId">Futures contract identifier.</param>
    /// <param name="valueDate">Value date.</param>
    /// <param name="timePeriod">Time period for the TDI signal.</param>
    public FuturesTdiSignalEntityId(string contractId, DateOnly valueDate, TradeTimePeriodType timePeriod)
    {
        ContractId = contractId;
        ValueDate = valueDate;
        TimePeriod = timePeriod;
    }

    /// <summary>
    /// Factory method for explicit creation.
    /// </summary>
    public static FuturesTdiSignalEntityId Create(string contractId, DateOnly valueDate, TradeTimePeriodType timePeriod) => new(contractId, valueDate, timePeriod);

    /// <summary>
    /// Formats the identifier into a stable string key: ContractId.yyyyMMdd.TimePeriod
    /// </summary>
    public string Format() => string.Create(null, stackalloc char[80], $"{ContractId}.{ValueDate:yyyyMMdd}.{TimePeriod}");

    /// <summary>
    /// Returns a compact JSON representation.
    /// </summary>
    public override string ToString() => JsonConvert.SerializeObject(this, Formatting.None);
}
