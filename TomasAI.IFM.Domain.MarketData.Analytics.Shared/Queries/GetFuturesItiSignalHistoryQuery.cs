using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.QueryParameters;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.Queries;

/// <summary>Retrieves every durable Futures ITI signal in the requested display timeframe.</summary>
[MessagePackObject(AllowPrivate = true)]
public record GetFuturesItiSignalHistoryQuery : IQuery<FuturesItiSignalV2ReadModel[]>
{
    [IgnoreMember] public const string Actor = "FuturesItiSignalQuery";
    [IgnoreMember] public const string Verb = "GetFuturesItiSignalHistory";
    [IgnoreMember] public const int ErrorId = 1022;

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public IActorEntityId EntityId { get; init; }
    [IgnoreMember] public int ErrorCode { get; init; }
    [IgnoreMember] public string? QueryParams { get; init; }
    [Key(2)] public string ContractId { get; init; }
    [Key(3)] public DateOnly ValueDate { get; init; }
    [Key(4)] public TimeFrameType TimePeriod { get; init; }

    public GetFuturesItiSignalHistoryQuery() { }

    public GetFuturesItiSignalHistoryQuery(
        string contractId,
        DateOnly valueDate,
        TimeFrameType timePeriod)
    {
        ContractId = contractId ?? string.Empty;
        ValueDate = valueDate;
        TimePeriod = timePeriod;
        EntityId = new GetFuturesItiSignalHistoryParameter(contractId, valueDate, timePeriod);
        ErrorCode = ErrorId;
    }

    [SerializationConstructor]
    public GetFuturesItiSignalHistoryQuery(
        ActorSubject subject,
        IActorEntityId entityId,
        string contractId,
        DateOnly valueDate,
        TimeFrameType timePeriod)
    {
        Subject = subject;
        EntityId = new GetFuturesItiSignalHistoryParameter(contractId, valueDate, timePeriod);
        ContractId = contractId ?? string.Empty;
        ValueDate = valueDate;
        TimePeriod = timePeriod;
        ErrorCode = ErrorId;
    }
}
