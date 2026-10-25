using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using NewTradeOrderId = TomasAI.IFM.Domain.Trade.Shared.TradeOrderId;

namespace TomasAI.IFM.Domain.Trade.Shared.Order;

[MessagePackObject]
public sealed record ExpireTradeOrderCommand : TradeOrderCommand
{
    public const string Verb = "ExpireTradeOrder";
    [Key(4)] public DateTime EffectiveAtUtc { get; init; }
}
