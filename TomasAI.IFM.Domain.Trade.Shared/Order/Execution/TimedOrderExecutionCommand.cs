using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Order.Execution;

[MessagePackObject]
[Union(0, typeof(AcceptOrderExecutionCommand))]
[Union(1, typeof(CancelOrderExecutionCommand))]
public abstract record TimedOrderExecutionCommand : OrderExecutionCommand
{
    [Key(4)] public DateTime EffectiveAtUtc { get; init; }
}
