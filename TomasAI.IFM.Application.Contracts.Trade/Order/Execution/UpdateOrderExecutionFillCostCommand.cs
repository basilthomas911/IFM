using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Order.Execution;

[MessagePackObject]
public sealed record UpdateOrderExecutionFillCostCommand : OrderExecutionCommand
{
    public const string Verb = "UpdateOrderExecutionFillCost";
    [Key(4)] public string ExternalExecutionId { get; init; } = string.Empty;
    [Key(5)] public decimal Commission { get; init; }
}
