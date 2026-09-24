using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.MarketData.Shared.DownloadLog;
/// <summary>Requests paged download history for one partition.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed class GetMarketDataDownloadHistoryQuery : IQuery<MarketDataDownloadHistoryResult>
{
    public const string Actor = "DownloadLogQuery";
    public const string Verb = "GetMarketDataDownloadHistory";
    public const int ErrorId = 6051;
    [Key(0)] public ActorSubject Subject { get; set; } = default!;
    [Key(1)] public IActorEntityId EntityId { get; set; } = default!;
    [Key(2)] public MarketDataDownloadPartition Request { get; init; } = default!;
    [IgnoreMember] public int ErrorCode { get; set; } = ErrorId;
    [IgnoreMember] public string? QueryParams => null;
    [Key(3)] public int PageSize { get; init; } = 100;
    [Key(4)] public MarketDataDownloadCursor? Cursor { get; init; } = null;
    /// <summary>Creates an empty query for deserialization.</summary>
    public GetMarketDataDownloadHistoryQuery() { }
    /// <summary>Creates a history query for the requested partition.</summary>
    /// <param name="request">The download partition to inspect.</param>
    public GetMarketDataDownloadHistoryQuery(MarketDataDownloadPartition request)
    { Request = request; EntityId = request; Subject = new(ActorType.Query, Actor, Verb, request.Format()); }
}
