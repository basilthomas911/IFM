using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using NewTradeOrderId = TomasAI.IFM.Domain.Trade.Shared.TradeOrderId;

namespace TomasAI.IFM.Domain.Trade.Shared.Order;

[MessagePackObject]
public sealed record ReleaseTradeOrderExecutionCommand : TradeOrderCommand
{
    public const string Verb = "ReleaseTradeOrderExecution";
    [Key(4)] public Guid ExecutionAttemptId { get; init; }
    [Key(5)] public bool ZeroExposureConfirmed { get; init; }
    [Key(6)] public DateTime EffectiveAtUtc { get; init; }
}
