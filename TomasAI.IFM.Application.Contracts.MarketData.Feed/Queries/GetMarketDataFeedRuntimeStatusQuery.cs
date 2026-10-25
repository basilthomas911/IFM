using MessagePack;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.QueryParameters;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared.Queries;

/// <summary>Queries the authoritative runtime state of the application-owned market-data feed.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed class GetMarketDataFeedRuntimeStatusQuery : IQuery<MarketDataFeedRuntimeStatusReadModel>
{
    [IgnoreMember] public const string Actor = "MarketDataFeedQuery";
    [IgnoreMember] public const string Verb = "GetRuntimeStatus";
    [IgnoreMember] public const int ErrorId = 1017;

    [Key(0)] public ActorSubject Subject { get; set; }
    [Key(1)] public IActorEntityId EntityId { get; set; }
    [IgnoreMember] public int ErrorCode { get; set; } = ErrorId;
    [IgnoreMember] public string? QueryParams { get; set; }

    public GetMarketDataFeedRuntimeStatusQuery()
        => EntityId = new GetMarketDataFeedRuntimeStatusParameter();

    [SerializationConstructor]
    public GetMarketDataFeedRuntimeStatusQuery(ActorSubject subject, IActorEntityId entityId)
    {
        Subject = subject;
        EntityId = new GetMarketDataFeedRuntimeStatusParameter();
        ErrorCode = ErrorId;
    }
}
