using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.MarketData.Shared.DownloadLog;
[MessagePackObject]
public sealed class GetMarketDataDownloadLogQuery : IQuery<MarketDataDownloadLogResult>
{
    public const string Actor = "DownloadLogQuery";
    public const string Verb = "GetMarketDataDownloadLog";
    public const int ErrorId = 6051;
    [Key(0)] public ActorSubject Subject { get; set; } = default!;
    [Key(1)] public IActorEntityId EntityId { get; set; } = default!;
    [Key(2)] public MarketDataDownloadPartition Request { get; init; } = default!;
    [IgnoreMember] public int ErrorCode { get; set; } = ErrorId;
    [IgnoreMember] public string? QueryParams => null;
    [Key(3)] public MarketDataDownloadCursor Attempt { get; init; } = default!;
    public GetMarketDataDownloadLogQuery() { }
    public GetMarketDataDownloadLogQuery(MarketDataDownloadPartition request)
    { Request = request; EntityId = request; Subject = new(ActorType.Query, Actor, Verb, request.Format()); }
}
