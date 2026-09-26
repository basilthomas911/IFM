using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;

/// <summary>A queryable stage snapshot for one strategy-position exit workflow.</summary>
[MessagePackObject]
public sealed record ExitPositionWorkflowProjection
{
    [Key(0)] public ushort SchemaVersion { get; init; } = 1;
    [Key(1)] public ExitPositionWorkflowId WorkflowId { get; init; }
    [Key(2)] public TradeStrategyKind StrategyKind { get; init; }
    [Key(3)] public ExitPositionWorkflowState State { get; init; }
    [Key(4)] public long StageRevision { get; init; }
    [Key(5)] public DateTime UpdatedAtUtc { get; init; }
    [Key(6)] public Guid SourcePlanEventId { get; init; }
    [Key(7)] public StrategyTradePlanSnapshot ExitPlan { get; init; } = new();
    [Key(8)] public ExitOrderComposition? Composition { get; init; }
    [Key(9)] public PortfolioCloseRiskDecision? RiskDecision { get; init; }
}

/// <summary>A bounded page of exit-workflow stage snapshots.</summary>
[MessagePackObject]
public sealed record PositionExitWorkflowHistoryPage(
    [property: Key(0)] ExitPositionWorkflowProjection[] Items,
    [property: Key(1)] byte[]? PagingState);
