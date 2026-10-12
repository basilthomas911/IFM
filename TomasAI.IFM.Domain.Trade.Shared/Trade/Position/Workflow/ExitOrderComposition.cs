using System.Globalization;
using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;

[MessagePackObject]
public sealed record ExitOrderComposition
{
    [Key(0)] public ExitPositionWorkflowId WorkflowId { get; init; }
    [Key(1)] public TradeStrategyKind StrategyKind { get; init; }
    [Key(2)] public TradeOrderComponentDefinition Component { get; init; } = new();
    [Key(3)] public TradePlanAction ExitAction { get; init; }
    [Key(4)] public string CompositionHash { get; init; } = string.Empty;
    [Key(5)] public DateTime ComposedAtUtc { get; init; }
    [Key(6)] public StrategyPositionSnapshot Position { get; init; } = new();
    [Key(7)] public TradeOrderPositionType PositionType { get; init; } = TradeOrderPositionType.Closing;
}
