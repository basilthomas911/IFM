using MessagePack;
using Newtonsoft.Json;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared;

/// <summary>
/// Represents a unique identifier for a futures ADX signal, encapsulating the contract ID, value date, time period, and
/// timestamp associated with the signal.
/// </summary>
/// <remarks>This record is used to distinguish individual ADX signals for futures contracts. It provides
/// essential information for analyzing market conditions and contract performance, and is compatible with serialization
/// frameworks such as MessagePack. The identifier can be formatted as a stable string key for storage or lookup
/// scenarios.</remarks>
[MessagePackObject(AllowPrivate = true)]
public record FuturesAdxSignalId : IActorEntityId
{
    /// <summary>Futures contract identifier (root + contract month/year code).</summary>
    [Key(0)]
    public string ContractId { get; init; }

    /// <summary>Value date associated with the ADX signal.</summary>
    [Key(1)]
    public DateOnly ValueDate { get; init; }

    [Key(2)]
    public TimeFrameType TimePeriod { get; init; }

    [Key(3)]
    public int PeriodLength { get; init; }

    /// <summary>Timestamp (intraday time component) linked to the signal generation.</summary>
    [Key(4)]
    public TimeOnly Timestamp { get; init; }

    /// <summary>
    /// Parameterless constructor required for MessagePack and some serializers.
    /// </summary>
    public FuturesAdxSignalId() { }

    /// <summary>
    /// Initializes a new <see cref="FuturesAdxSignalId"/>.
    /// </summary>
    /// <param name="contractId">Futures contract identifier.</param>
    /// <param name="valueDate">Value date of the signal.</param>
    /// <param name="timePeriod">Futures time period type.</param>
    /// <param name="periodLength">The length of the time period.</param>
    /// <param name="timestamp">Intraday timestamp component.</param>
    public FuturesAdxSignalId(string contractId, DateOnly valueDate, TimeFrameType timePeriod, int periodLength, TimeOnly timestamp)
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
    public static FuturesAdxSignalId Create(string contractId, DateOnly valueDate, TimeFrameType timePeriod, int periodLength, TimeOnly timestamp)
        => new(contractId, valueDate, timePeriod, periodLength, timestamp);

    /// <summary>
    /// Formats the identifier into a stable string key: ContractId.yyyyMMdd.Timestamp
    /// </summary>
    public string Format() => string.Create(null, stackalloc char[100], $"{ContractId}.{ValueDate:yyyyMMdd}.{TimePeriod}.{PeriodLength}.{Timestamp:HHmmss}");

    /// <summary>
    /// Returns a compact JSON representation.
    /// </summary>
    public override string ToString() => JsonConvert.SerializeObject(this, Formatting.None);

    public FuturesAdxSignalEntityId ToEntityId() => new(ContractId, ValueDate, TimePeriod, PeriodLength);
    public FuturesAdxDailySignalEntityId ToDailyEntityId() => new(ContractId, TimePeriod, PeriodLength);
}

