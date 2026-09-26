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
