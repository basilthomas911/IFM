using MessagePack;
using Newtonsoft.Json;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.MarketDataAnalytics;

/// <summary>
/// Represents the identifier for a futures ITI signal, consisting of a contract identifier and a value date.
/// </summary>
/// <remarks>This record is designed for use with MessagePack serialization. It provides a factory method for
/// creating instances and a method to format the identifier into a stable string key. The ToString method returns a
/// compact JSON representation of the instance.</remarks>
[MessagePackObject(AllowPrivate = true)]
public record FuturesItiSignalEntityId : IActorEntityId
{
    [Key(0)] public string ContractId { get; init; }
    [Key(1)] public DateOnly ValueDate { get; init; }
    [Key(2)] public TradeTimePeriodType TimePeriod { get; init; }

    /// <summary>
    public FuturesItiSignalEntityId() { }

    /// <summary>
    /// Initializes a new <see cref="FuturesItiSignalEntityId"/>.
    /// </summary>
    /// <param name="contractId">Futures contract identifier.</param>
    /// <param name="valueDate">Value date.</param>
    /// <param name="timePeriod">Time period type.</param>
    public FuturesItiSignalEntityId(string contractId, DateOnly valueDate, TradeTimePeriodType timePeriod)
    {
        ContractId = contractId;
        ValueDate = valueDate;
        TimePeriod = timePeriod;
    }

    /// <summary>
    /// Factory method for explicit creation.
    /// </summary>
    /// <param name="contractId">Futures contract identifier.</param>
    /// <param name="valueDate">Value date.</param>
    /// <param name="timePeriod">Time period type.</param>
    public static FuturesItiSignalEntityId Create(string contractId, DateOnly valueDate, TradeTimePeriodType timePeriod) 
        => new(contractId, valueDate, timePeriod);

    /// <summary>
    public string Format() => string.Create(null, stackalloc char[80], $"{ContractId}.{ValueDate:yyyyMMdd}.{TimePeriod}");

    /// <summary>
    public override string ToString() => JsonConvert.SerializeObject(this, Formatting.None);
}
