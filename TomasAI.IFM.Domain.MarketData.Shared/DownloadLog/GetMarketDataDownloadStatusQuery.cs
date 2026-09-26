using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.MarketData.Shared.DownloadLog;
/// <summary>Requests the latest download status for one partition.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed class GetMarketDataDownloadStatusQuery : IQuery<MarketDataDownloadStatusResult>
{

    /// <summary>Rehydrates every published query field in permanent numeric-key order.</summary>
    /// <param name="subject">The Subject field.</param>
    /// <param name="entityId">The EntityId field.</param>
    /// <param name="request">The Request field.</param>
    /// <param name="requiredImportCommandId">The RequiredImportCommandId field.</param>
    /// <param name="cursor">The Cursor field.</param>
    [SerializationConstructor]
    public GetMarketDataDownloadStatusQuery(ActorSubject subject, IActorEntityId entityId, MarketDataDownloadPartition request, Guid? requiredImportCommandId, MarketDataDownloadCursor? cursor)
    {
        Subject = subject;
        EntityId = entityId;
        Request = request;
        RequiredImportCommandId = requiredImportCommandId;
        Cursor = cursor;
    }
    public const string Actor = "DownloadLogQuery";
    public const string Verb = "GetMarketDataDownloadStatus";
    public const int ErrorId = 6051;
    [Key(0)] public ActorSubject Subject { get; set; } = default!;
    [Key(1)] public IActorEntityId EntityId { get; set; } = default!;
    [Key(2)] public MarketDataDownloadPartition Request { get; init; } = default!;
    [IgnoreMember] public int ErrorCode { get; set; } = ErrorId;
    [IgnoreMember] public string? QueryParams => null;
    [Key(3)] public Guid? RequiredImportCommandId { get; init; } = null;
    [Key(4)] public MarketDataDownloadCursor? Cursor { get; init; } = null;
    /// <summary>Creates an empty query for deserialization.</summary>
    public GetMarketDataDownloadStatusQuery() { }
    /// <summary>Creates a status query for the requested partition.</summary>
    /// <param name="request">The download partition to inspect.</param>
    public GetMarketDataDownloadStatusQuery(MarketDataDownloadPartition request)
    { Request = request; EntityId = request; Subject = new(ActorType.Query, Actor, Verb, request.Format()); }
}
