using MessagePack;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.QueryParameters;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared.Queries;

/// <summary>
/// MessagePack-serializable query to retrieve the normal curve table.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public class GetNormalCurveTableQuery : IQuery<NormalCurveTableReadModel>
{
    [IgnoreMember] public const string Actor = "MarketDataFeedQuery";
    [IgnoreMember] public const string Verb = "GetNormalCurveTable";
    [IgnoreMember] public const int ErrorId = 1015;

    [Key(0)] public ActorSubject Subject { get; set; }
    [Key(1)] public IActorEntityId EntityId { get; set; }
    [IgnoreMember] public int ErrorCode { get; set; }
    [IgnoreMember] public string? QueryParams { get; set; }

    public GetNormalCurveTableQuery()
    {
        EntityId = new GetNormalCurveTableParameter();
        ErrorCode = ErrorId;
    }

    /// <summary>
    /// MessagePack serialization constructor.
    /// </summary>
    [SerializationConstructor]
    public GetNormalCurveTableQuery(ActorSubject subject, IActorEntityId entityId)
    {
        Subject = subject;
        EntityId = new GetNormalCurveTableParameter();
        ErrorCode = ErrorId;
    }
}
