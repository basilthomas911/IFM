using MessagePack;
using Newtonsoft.Json;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.MarketDataAnalytics;

/// <summary>
/// Represents a unique identifier for a futures ITI signal, encapsulating the contract identifier, value date, and
/// intrinsic time.
/// </summary>
/// <remarks>This record is intended for use in market data analytics scenarios where a stable, serializable
/// identifier is required. It supports MessagePack serialization and provides methods for formatting and compact JSON
/// representation. The identifier can be used as a key for signal tracking, storage, or messaging.</remarks>
[MessagePackObject(AllowPrivate = true)]
public record FuturesItiSignalId : IActorEntityId
{
    [Key(0)] public string ContractId { get; init; }
    [Key(1)] public DateOnly ValueDate { get; init; }
    [Key(2)] public TradeTimePeriodType TimePeriod { get; init; }
    [Key(3)] public DateTime IntrinsicTime { get; init; }

    /// <summary>
    /// Parameterless constructor required for MessagePack and some serializers.
    /// </summary>
    public FuturesItiSignalId() { }

    /// <summary>
    /// Initializes a new <see cref="FuturesItiSignalId"/>.
    /// </summary>
    /// <param name="contractId">Futures contract identifier.</param>
    /// <param name="valueDate">Value date.</param>
    /// <param name="timePeriod">Time period type.</param>
    /// <param name="intrinsicTime">Intrinsic (timestamp) time.</param>
    public FuturesItiSignalId(string contractId, DateOnly valueDate, TradeTimePeriodType timePeriod, DateTime intrinsicTime)
    {
        ContractId = contractId;
        ValueDate = valueDate;
        TimePeriod = timePeriod;
        IntrinsicTime = intrinsicTime;
    }

    /// <summary>
    /// Creates a new instance of the FuturesItiSignalId class using the specified contract identifier, value date, time
    /// period, and intrinsic time.
    /// </summary>
    /// <param name="contractId">The unique identifier of the contract associated with the signal.</param>
    /// <param name="valueDate">The date for which the signal is applicable.</param>
    /// <param name="timePeriod">The time period that defines the duration of the trade.</param>
    /// <param name="intrinsicTime">The date and time when the intrinsic value is determined.</param>
    /// <returns>A new FuturesItiSignalId instance initialized with the provided parameters.</returns>
    public static FuturesItiSignalId Create(string contractId, DateOnly valueDate, TradeTimePeriodType timePeriod, DateTime intrinsicTime)
        => new(contractId, valueDate, timePeriod, intrinsicTime);

    /// <summary>
    /// Formats the identifier into a stable string key: ContractId.yyyyMMdd.HH:mm:ss
    /// </summary>
    public string Format() => string.Create(null, stackalloc char[80], $"{ContractId}.{ValueDate:yyyyMMdd}.{IntrinsicTime:HH\\:mm\\:ss}");

    /// <summary>
    /// Returns a compact JSON representation (diagnostics/logging).
    /// </summary>
    public override string ToString() => JsonConvert.SerializeObject(this, Formatting.None);
}
