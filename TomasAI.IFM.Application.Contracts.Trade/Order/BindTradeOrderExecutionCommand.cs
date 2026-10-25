using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using NewTradeOrderId = TomasAI.IFM.Domain.Trade.Shared.TradeOrderId;

namespace TomasAI.IFM.Domain.Trade.Shared.Order;

[MessagePackObject]
public sealed record BindTradeOrderExecutionCommand : TradeOrderCommand
{
    public const string Verb = "BindTradeOrderExecution";
    [Key(4)] public Guid ExecutionAttemptId { get; init; }
    [Key(5)] public ExecutionChannel ExecutionChannel { get; init; }
    [Key(6)] public DateTime EffectiveAtUtc { get; init; }
}
