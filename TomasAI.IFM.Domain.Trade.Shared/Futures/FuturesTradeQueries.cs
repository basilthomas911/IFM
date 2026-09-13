using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Futures;

[MessagePackObject]
public sealed record GetFuturesTradeQuery : IQuery<EstablishedTradeDefinition>
{
    public const string Actor = FuturesTradeActorNames.Query;
    public const string Verb = "GetFuturesTrade";
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public IActorEntityId EntityId { get; init; } = ActorEntityId.Default;
    [Key(2)] public TradeEntityId TradeId { get; init; }
    [IgnoreMember] public int ErrorCode => 25203;
    [IgnoreMember] public string? QueryParams => null;
}
