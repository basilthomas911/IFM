using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.Queries;

/// <summary>
/// MessagePack-serializable query to retrieve the Traders Dynamic Index (TDI) signal
/// for a specific futures contract on a given value date.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record GetFuturesTdiSignalQuery : IQuery<FuturesTdiSignalReadModel>
{
    [IgnoreMember] public const string Actor = "FuturesTdiSignalQuery";
    [IgnoreMember] public const string Verb = "GetFuturesTdiSignal";
    [IgnoreMember] public const int ErrorId = 1021;

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public IActorEntityId EntityId { get; init; }
    [IgnoreMember] public int ErrorCode { get; init; }
    [IgnoreMember] public string? QueryParams { get; init; }

    [Key(2)]
    public string ContractId { get; init; }

    [Key(3)]
    public DateOnly ValueDate { get; init; }

    [Key(4)]
    public TimeFrameType TimePeriod { get; init; }

    [Key(5)]
    public string ConfigurationId { get; init; } = FuturesTdiConfiguration.StandardConfigurationId;

    public GetFuturesTdiSignalQuery() { }

    public GetFuturesTdiSignalQuery(
        string contractId,
        DateOnly valueDate,
        TimeFrameType timePeriod = TimeFrameType.OneMinute,
        string configurationId = FuturesTdiConfiguration.StandardConfigurationId)
    {
        ContractId = contractId ?? string.Empty;
        ValueDate = valueDate;
        TimePeriod = timePeriod;
        ConfigurationId = configurationId;
        EntityId = new FuturesTdiSignalEntityId(contractId, valueDate, timePeriod, configurationId);
        ErrorCode = ErrorId;
    }

    /// <summary>
    /// MessagePack serialization constructor.
    /// </summary>
    [SerializationConstructor]
    public GetFuturesTdiSignalQuery(
        ActorSubject subject,              // Key(0)
        IActorEntityId entityId,           // Key(1)
        string contractId,                 // Key(2)
        DateOnly valueDate,                // Key(3)
        TimeFrameType timePeriod,  // Key(4)
        string? configurationId)   // Key(5)
    {
        Subject = subject;
        ContractId = contractId ?? string.Empty;
        ValueDate = valueDate;
        TimePeriod = timePeriod;
        ConfigurationId = configurationId ?? FuturesTdiConfiguration.StandardConfigurationId;
        EntityId = new FuturesTdiSignalEntityId(contractId, valueDate, timePeriod, ConfigurationId);
        ErrorCode = ErrorId;
    }
}
