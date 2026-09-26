using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.MarketData.Shared.DownloadLog;
/// <summary>Requests a download-log attempt for one partition.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed class GetMarketDataDownloadLogQuery : IQuery<MarketDataDownloadLogResult>
{

    /// <summary>Rehydrates every published query field in permanent numeric-key order.</summary>
    /// <param name="subject">The Subject field.</param>
    /// <param name="entityId">The EntityId field.</param>
    /// <param name="request">The Request field.</param>
    /// <param name="attempt">The Attempt field.</param>
    [SerializationConstructor]
    public GetMarketDataDownloadLogQuery(ActorSubject subject, IActorEntityId entityId, MarketDataDownloadPartition request, MarketDataDownloadCursor attempt)
    {
        Subject = subject;
        EntityId = entityId;
        Request = request;
        Attempt = attempt;
    }
    public const string Actor = "DownloadLogQuery";
    public const string Verb = "GetMarketDataDownloadLog";
    public const int ErrorId = 6051;
    [Key(0)] public ActorSubject Subject { get; set; } = default!;
    [Key(1)] public IActorEntityId EntityId { get; set; } = default!;
    [Key(2)] public MarketDataDownloadPartition Request { get; init; } = default!;
    [IgnoreMember] public int ErrorCode { get; set; } = ErrorId;
    [IgnoreMember] public string? QueryParams => null;
    [Key(3)] public MarketDataDownloadCursor Attempt { get; init; } = default!;
    /// <summary>Creates an empty query for deserialization.</summary>
    public GetMarketDataDownloadLogQuery() { }
    /// <summary>Creates an attempt query for the requested partition.</summary>
    /// <param name="request">The download partition to inspect.</param>
    public GetMarketDataDownloadLogQuery(MarketDataDownloadPartition request)
    { Request = request; EntityId = request; Subject = new(ActorType.Query, Actor, Verb, request.Format()); }
}
