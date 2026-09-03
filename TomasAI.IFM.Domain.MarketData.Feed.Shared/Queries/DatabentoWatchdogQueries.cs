using MessagePack;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.QueryParameters;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared.Queries;

[MessagePackObject(AllowPrivate = true)]
public sealed class GetDatabentoReadinessQuery : IQuery<DatabentoReadinessReadModel>
{
    [IgnoreMember] public const string Actor = "MarketDataFeedQuery";
    [IgnoreMember] public const string Verb = "GetDatabentoReadiness";
    [IgnoreMember] public const int ErrorId = 1018;
    [Key(0)] public ActorSubject Subject { get; set; }
    [Key(1)] public IActorEntityId EntityId { get; set; } = new GetDatabentoReadinessParameter();
    [IgnoreMember] public int ErrorCode { get; set; } = ErrorId;
    [IgnoreMember] public string? QueryParams { get; set; }
    public GetDatabentoReadinessQuery() { }
    [SerializationConstructor] public GetDatabentoReadinessQuery(ActorSubject subject, IActorEntityId entityId)
    { Subject = subject; EntityId = new GetDatabentoReadinessParameter(); }
}

[MessagePackObject(AllowPrivate = true)]
public sealed class GetDatabentoCurrentContractsQuery : IQuery<DatabentoContractAssignmentReadModel[]>
{
    [IgnoreMember] public const string Actor = "MarketDataFeedQuery";
    [IgnoreMember] public const string Verb = "GetDatabentoCurrentContracts";
    [IgnoreMember] public const int ErrorId = 1019;
    [Key(0)] public ActorSubject Subject { get; set; }
    [Key(1)] public IActorEntityId EntityId { get; set; } = new GetDatabentoCurrentContractsParameter();
    [IgnoreMember] public int ErrorCode { get; set; } = ErrorId;
    [IgnoreMember] public string? QueryParams { get; set; }
    public GetDatabentoCurrentContractsQuery() { }
    [SerializationConstructor] public GetDatabentoCurrentContractsQuery(ActorSubject subject, IActorEntityId entityId)
    { Subject = subject; EntityId = new GetDatabentoCurrentContractsParameter(); }
}

[MessagePackObject(AllowPrivate = true)]
public sealed class GetDatabentoWatchdogHistoryQuery : IQuery<DatabentoWatchdogObservationReadModel[]>
{
    [IgnoreMember] public const string Actor = "MarketDataFeedQuery";
    [IgnoreMember] public const string Verb = "GetDatabentoWatchdogHistory";
    [IgnoreMember] public const int ErrorId = 1020;
    [Key(0)] public ActorSubject Subject { get; set; }
    [Key(1)] public IActorEntityId EntityId { get; set; }
    [Key(2)] public DateOnly? ValueDate { get; set; }
    [Key(3)] public string? MajorStatus { get; set; }
    [Key(4)] public int PageSize { get; set; }
    [IgnoreMember] public int ErrorCode { get; set; } = ErrorId;
    [IgnoreMember] public string? QueryParams { get; set; }
    public GetDatabentoWatchdogHistoryQuery(DateOnly? valueDate = null, string? majorStatus = null, int pageSize = 100)
    {
        ValueDate = valueDate; MajorStatus = majorStatus; PageSize = pageSize;
        EntityId = new GetDatabentoWatchdogHistoryParameter(valueDate, majorStatus, pageSize);
    }
    [SerializationConstructor] public GetDatabentoWatchdogHistoryQuery(
        ActorSubject subject, IActorEntityId entityId, DateOnly? valueDate, string? majorStatus, int pageSize)
    {
        Subject = subject; ValueDate = valueDate; MajorStatus = majorStatus; PageSize = pageSize;
        EntityId = new GetDatabentoWatchdogHistoryParameter(valueDate, majorStatus, pageSize);
    }
}
