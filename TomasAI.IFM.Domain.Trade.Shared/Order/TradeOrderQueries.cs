using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using NewTradeOrderId = TomasAI.IFM.Domain.Trade.Shared.TradeOrderId;

namespace TomasAI.IFM.Domain.Trade.Shared.Order;

[MessagePackObject]
public sealed record GetTradeOrderQuery : IQuery<TradeOrderDefinition>
{
    public const string Actor = TradeOrderActorNames.Query;
    public const string Verb = "GetTradeOrder";
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public IActorEntityId EntityId { get; init; } = ActorEntityId.Default;
    [Key(2)] public NewTradeOrderId TradeOrderId { get; init; }
    [IgnoreMember] public int ErrorCode => 25209;
    [IgnoreMember] public string? QueryParams => null;
}
