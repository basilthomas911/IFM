using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Trade.Realtime;

[MessagePackObject]
public sealed record GetTradeRealtimeSnapshotQuery : IQuery<TradeRealtimeSnapshot>
{
    public const string Actor = "FuturesOptionTradeQuery";
    public const string Verb = "GetTradeRealtimeSnapshot";
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public IActorEntityId EntityId { get; init; } = ActorEntityId.Default;
    [Key(2)] public TradeEntityId TradeId { get; init; }
    [Key(3)] public DateOnly ValueDate { get; init; }
    [IgnoreMember] public int ErrorCode => 25201;
    [IgnoreMember] public string? QueryParams => null;
}
