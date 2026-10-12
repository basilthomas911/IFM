using FluentValidation;
using FluentValidation.Results;
using MessagePack;
using Newtonsoft.Json;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared;

/// <summary>
/// Unique identifier for a futures RSI signal composed of a contract identifier and a value date.
/// </summary>
/// <remarks>
/// MessagePack serializable (primitive components only). Implements <see cref="IActorEntityId"/> with a dot-separated
/// format: ContractId.yyyy-MM-dd. Provides factory and formatting helpers.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record FuturesRsiSignalEntityId : IActorEntityId
{
    /// <summary>Futures contract identifier (root + contract month/year code).</summary>
    [Key(0)]
    public string ContractId { get; init; }

    [Key(1)]
    public DateOnly ValueDate { get; init; }

    /// <summary>Value date associated with the RSI signal.</summary>
    [Key(2)]
    public TimeFrameType TimePeriod { get; init; }

    [Key(3)]
    public int PeriodLength { get; init; }

    /// <summary>
    /// Parameterless constructor required for MessagePack and some serializers.
    /// </summary>
    public FuturesRsiSignalEntityId() { }

    /// <summary>
    /// Initializes a new <see cref="FuturesRsiSignalEntityId"/>.
    /// </summary>
    /// <param name="contractId">The futures contract identifier.</param>
    /// <param name="valueDate">The value date for the RSI signal.</param>
    /// <param name="timePeriod">The type of RSI signal.</param>
    /// <param name="periodLength"></param>
    public FuturesRsiSignalEntityId(string contractId, DateOnly valueDate, TimeFrameType timePeriod, int periodLength)
    {
        ContractId = contractId;
        ValueDate = valueDate;
        TimePeriod = timePeriod;
        PeriodLength = periodLength;
    }

    /// <summary>
    /// Factory method for creating a new identifier instance.
    /// </summary>
    /// <param name="contractId">The futures contract identifier.</param>
    /// <param name="valueDate">The value date for the RSI signal.</param>
    /// <param name="timePeriod">The type of RSI signal.</param>
    /// <param name="periodLength">The length of the RSI period.</param>
    public static FuturesRsiSignalEntityId Create(string contractId, DateOnly valueDate, TimeFrameType timePeriod, int periodLength) => new(contractId, valueDate, timePeriod, periodLength);

    /// <summary>
    /// Formats the identifier into a stable string key: ContractId.yyyyMMdd.SignalType
    /// </summary>
    public string Format() => string.Create(null, stackalloc char[80], $"{ContractId}.{ValueDate:yyyyMMdd}.{TimePeriod}.{PeriodLength}");

    /// <summary>
    /// Returns a compact JSON representation.
    /// </summary>
    public override string ToString() => JsonConvert.SerializeObject(this, Formatting.None);
}

