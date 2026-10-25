using MessagePack;
using Newtonsoft.Json;
using FluentValidation;
using FluentValidation.Results;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared;

/// <summary>
/// Unique identifier for a futures Traders Dynamic Index (TDI) signal composed of a contract identifier,
/// a value date, and a timestamp component for intraday distinction.
/// </summary>
/// <remarks>
/// MessagePack serializable (primitive components only). Implements <see cref="IActorEntityId"/> with a dot
/// separated format: ContractId.yyyy-MM-dd.HH:mm:ss. Use <see cref="Create(string, DateOnly, TimeOnly)"/> when
/// explicit construction improves clarity.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record FuturesTdiSignalId : IActorEntityId
{
    /// <summary>Futures contract identifier (root + contract month/year code).</summary>
    [Key(0)]
    public string ContractId { get; init; }

    /// <summary>Value date associated with the TDI signal.</summary>
    [Key(1)]
    public DateOnly ValueDate { get; init; }

    /// <summary>Timestamp (intraday time component) linked to the signal generation.</summary>
    [Key(2)]
    public TimeOnly Timestamp { get; init; }

    /// <summary>Sampling period represented by this signal.</summary>
    [Key(3)]
    public TimeFrameType TimePeriod { get; init; }

    /// <summary>Stable identifier of the calculation contract.</summary>
    [Key(4)]
    public string ConfigurationId { get; init; } = FuturesTdiConfiguration.StandardConfigurationId;

    /// <summary>
    /// Parameterless constructor required for MessagePack and some serializers.
    /// </summary>
    public FuturesTdiSignalId() { }

    /// <summary>
    /// Initializes a new <see cref="FuturesTdiSignalId"/>.
    /// </summary>
    /// <param name="contractId">Futures contract identifier.</param>
    /// <param name="valueDate">Value date of the signal.</param>
    /// <param name="timestamp">Intraday timestamp component.</param>
    public FuturesTdiSignalId(string contractId, DateOnly valueDate, TimeOnly timestamp)
        : this(contractId, valueDate, TimeFrameType.OneMinute, timestamp)
    {
    }

    /// <summary>Initializes a configuration-aware intraday TDI signal identifier.</summary>
    public FuturesTdiSignalId(
        string contractId,
        DateOnly valueDate,
        TimeFrameType timePeriod,
        TimeOnly timestamp,
        string configurationId = FuturesTdiConfiguration.StandardConfigurationId)
    {
        ContractId = contractId;
        ValueDate = valueDate;
        TimePeriod = timePeriod;
        Timestamp = timestamp;
        ConfigurationId = configurationId;
    }

    /// <summary>
    /// Factory method for creating a new identifier instance.
    /// </summary>
    public static FuturesTdiSignalId Create(string contractId, DateOnly valueDate, TimeOnly timestamp)
        => new(contractId, valueDate, timestamp);

    public static FuturesTdiSignalId Create(
        string contractId,
        DateOnly valueDate,
        TimeFrameType timePeriod,
        TimeOnly timestamp,
        string configurationId = FuturesTdiConfiguration.StandardConfigurationId)
        => new(contractId, valueDate, timePeriod, timestamp, configurationId);

    /// <summary>
    /// Formats the identifier into a stable string key: ContractId.yyyy-MM-dd.HH:mm:ss
    /// </summary>
    public string Format() => $"{ContractId}.{ValueDate:yyyy-MM-dd}.{TimePeriod}.{Timestamp:HH:mm:ss}.{ConfigurationId}";

    /// <summary>
    /// Returns a compact JSON representation of the identifier.
    /// </summary>
    public override string ToString() => JsonConvert.SerializeObject(this, Formatting.None);
}
