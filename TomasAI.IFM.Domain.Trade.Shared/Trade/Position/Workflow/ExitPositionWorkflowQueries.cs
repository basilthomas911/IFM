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

/// <summary>Reads the latest exit workflow stage for a strategy position and value date.</summary>
[MessagePackObject]
public sealed record GetPositionExitWorkflowQuery : IQuery<ExitPositionWorkflowProjection?>
{
    [IgnoreMember] public const string Actor = "PositionExitWorkflowQuery";
    [IgnoreMember] public const string Verb = "GetPositionExitWorkflow";
    [IgnoreMember] public const int ErrorId = 27231;
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public IActorEntityId EntityId { get; init; } = ActorEntityId.Default;
    [Key(2)] public StrategyPositionId PositionId { get; init; }
    [Key(3)] public DateOnly ValueDate { get; init; }
    [IgnoreMember] public int ErrorCode => ErrorId;
    [IgnoreMember] public string? QueryParams => null;
}

/// <summary>Reads the bounded stage timeline for a strategy position and value date.</summary>
[MessagePackObject]
public sealed record GetPositionExitWorkflowTimelineQuery : IQuery<PositionExitWorkflowHistoryPage>
{
    [IgnoreMember] public const string Actor = GetPositionExitWorkflowQuery.Actor;
    [IgnoreMember] public const string Verb = "GetPositionExitWorkflowTimeline";
    [IgnoreMember] public const int ErrorId = 27232;
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public IActorEntityId EntityId { get; init; } = ActorEntityId.Default;
    [Key(2)] public StrategyPositionId PositionId { get; init; }
    [Key(3)] public DateOnly ValueDate { get; init; }
    [Key(4)] public int PageSize { get; init; } = 100;
    [Key(5)] public byte[]? PagingState { get; init; }
    [IgnoreMember] public int ErrorCode => ErrorId;
    [IgnoreMember] public string? QueryParams => null;
}
