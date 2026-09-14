using System.Globalization;
using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;

public enum ExitPositionWorkflowState : byte
{
    Started = 1,
    OrderComposed = 2,
    RiskAccepted = 3,
    NoTradeOrders = 4,
    Failed = 5,
    Completed = 6
}

/// <summary>Stable actor names for each strategy-specific exit pipeline.</summary>
public static class ExitPositionWorkflowActorNames
{
    public const string IronCondorOrderComposer = "IronCondorExitOrderCompositionFunction";
    public const string IronCondorRiskManager = "IronCondorPositionExitRiskFunction";
    public const string VerticalSpreadOrderComposer = "VerticalSpreadExitOrderCompositionFunction";
    public const string VerticalSpreadRiskManager = "VerticalSpreadPositionExitRiskFunction";
    public const string FuturesOrderComposer = "FuturesExitOrderCompositionFunction";
    public const string FuturesRiskManager = "FuturesPositionExitRiskFunction";
}

[MessagePackObject]
public readonly record struct ExitPositionWorkflowId(
    [property: Key(0)] StrategyPositionId Position,
    [property: Key(1)] DateOnly ValueDate,
    [property: Key(2)] Guid ExitDecisionId) : IActorEntityId
{
    public string Format() => string.Create(CultureInfo.InvariantCulture,
        $"{Position.Format()}.{ValueDate:yyyyMMdd}.{ExitDecisionId:N}");
    [IgnoreMember] public bool IsValid => Position.IsValid && ValueDate != default && ExitDecisionId != Guid.Empty;
}

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

/// <summary>The durable start snapshot for one strategy-position exit decision.</summary>
[MessagePackObject]
public sealed record ExitPositionWorkflowStartedEvent : IEvent<ExitPositionWorkflowId>
{
    public const string Verb = "ExitPositionWorkflowStarted";
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public Guid Id { get; init; }
    [Key(2)] public ExitPositionWorkflowId EntityId { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; } = string.Empty;
    [Key(6)] public string EventSource { get; init; } = string.Empty;
    [Key(7)] public DateTime ReceivedOn { get; init; }
    [Key(8)] public StrategyTradePlanSnapshot ExitPlan { get; init; } = new();
    [Key(9)] public TradeStrategyKind StrategyKind { get; init; }
    [Key(10)] public Guid SourcePlanEventId { get; init; }
    [IgnoreMember] public string UserName => "TradePlan";
    [IgnoreMember] public string EventName => nameof(ExitPositionWorkflowStartedEvent);
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;
}

[MessagePackObject]
public sealed record ComposeExitOrderCommand : ICommand<ExitPositionWorkflowId>
{
    public const string Verb = "ComposeExitOrder";
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; } = true;
    [Key(3)] public ExitPositionWorkflowId EntityId { get; init; }
    [Key(4)] public TradeStrategyKind StrategyKind { get; init; }
    [Key(5)] public ExitPositionWorkflowStartedEvent Started { get; init; } = new();
    [Key(6)] public string InputHash { get; init; } = string.Empty;
    [IgnoreMember] public string CommandName => nameof(ComposeExitOrderCommand);
    [IgnoreMember] public BoundedContextName RouteTo => BoundedContextName.StrategyPositionExitWorkflowBoundedContext;
    [IgnoreMember] public string StreamId => Subject.StreamId;
    [IgnoreMember] public string EventSource => Subject.Name;
    [IgnoreMember] public int ErrorCode => 27211;
}

[MessagePackObject]
public sealed record ExitOrderCompositionCompletedEvent : ICompleteEvent<ExitPositionWorkflowId>
{
    public const string Verb = "ExitOrderCompositionCompleted";
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public Guid Id { get; init; }
    [Key(2)] public ExitPositionWorkflowId EntityId { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; } = string.Empty;
    [Key(6)] public string EventSource { get; init; } = string.Empty;
    [Key(7)] public DateTime ReceivedOn { get; init; }
    [Key(8)] public ExitOrderComposition Composition { get; init; } = new();
    [Key(9)] public string RequestFingerprint { get; init; } = string.Empty;
    [IgnoreMember] public string UserName => "TradePlan";
    [IgnoreMember] public string EventName => nameof(ExitOrderCompositionCompletedEvent);
    [IgnoreMember] public EventType EventType => EventType.CompletedEvent;
}

[MessagePackObject]
public sealed record EvaluatePositionExitRiskCommand : ICommand<ExitPositionWorkflowId>
{
    public const string Verb = "EvaluatePositionExitRisk";
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; } = true;
    [Key(3)] public ExitPositionWorkflowId EntityId { get; init; }
    [Key(4)] public TradeStrategyKind StrategyKind { get; init; }
    [Key(5)] public ExitOrderComposition Composition { get; init; } = new();
    [Key(6)] public Guid CompositionEventId { get; init; }
    [Key(7)] public string InputHash { get; init; } = string.Empty;
    [IgnoreMember] public string CommandName => nameof(EvaluatePositionExitRiskCommand);
    [IgnoreMember] public BoundedContextName RouteTo => BoundedContextName.StrategyPositionExitWorkflowBoundedContext;
    [IgnoreMember] public string StreamId => Subject.StreamId;
    [IgnoreMember] public string EventSource => Subject.Name;
    [IgnoreMember] public int ErrorCode => 27221;
}

[MessagePackObject]
public sealed record PositionExitRiskCompletedEvent : ICompleteEvent<ExitPositionWorkflowId>
{
    public const string Verb = "PositionExitRiskCompleted";
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public Guid Id { get; init; }
    [Key(2)] public ExitPositionWorkflowId EntityId { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; } = string.Empty;
    [Key(6)] public string EventSource { get; init; } = string.Empty;
    [Key(7)] public DateTime ReceivedOn { get; init; }
    [Key(8)] public PortfolioCloseRiskDecision Decision { get; init; } = new();
    [Key(9)] public string RequestFingerprint { get; init; } = string.Empty;
    [IgnoreMember] public string UserName => "TradePlan";
    [IgnoreMember] public string EventName => nameof(PositionExitRiskCompletedEvent);
    [IgnoreMember] public EventType EventType => EventType.CompletedEvent;
}

[MessagePackObject]
public sealed record PortfolioCloseRiskDecision
{
    [Key(0)] public Guid PortfolioCompletedEventId { get; init; }
    [Key(1)] public TradeOrderDefinition? TradeOrder { get; init; }
    [Key(2)] public bool ExecuteTradeOrder { get; init; }
    [Key(3)] public string ReasonCode { get; init; } = string.Empty;
    [Key(4)] public long FinancialRevision { get; init; }
}

[MessagePackObject]
public sealed record ExitPositionWorkflowFailedEvent : IErrorEvent<ExitPositionWorkflowId>
{
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public Guid Id { get; init; }
    [Key(2)] public ExitPositionWorkflowId EntityId { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; } = string.Empty;
    [Key(6)] public string EventSource { get; init; } = string.Empty;
    [Key(7)] public DateTime ReceivedOn { get; init; }
    [Key(8)] public DateTime ErrorDate { get; init; }
    [Key(9)] public int ErrorCode { get; init; }
    [Key(10)] public string ErrorMessage { get; init; } = string.Empty;
    [Key(11)] public ErrorType ErrorType { get; init; } = ErrorType.Command;
    [Key(12)] public string ErrorData { get; init; } = string.Empty;
    [Key(13)] public string CommandName { get; init; } = string.Empty;
    [Key(14)] public string CommandData { get; init; } = string.Empty;
    [IgnoreMember] public string UserName => "TradePlan";
    [IgnoreMember] public string EventName => nameof(ExitPositionWorkflowFailedEvent);
    [IgnoreMember] public EventType EventType => EventType.ErrorEvent;
}

[MessagePackObject]
public sealed record StartIronCondorExitPositionWorkflowCommand : ICommand<ExitPositionWorkflowId>
{
    public const string Actor = "IronCondorExitPositionWorkflow";
    public const string Verb = "StartIronCondorExitPositionWorkflow";
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; } = true;
    [Key(3)] public ExitPositionWorkflowId EntityId { get; init; }
    [Key(4)] public IronCondorTradePlanUpdatedEvent ExitPlan { get; init; } = new();
    [IgnoreMember] public string CommandName => nameof(StartIronCondorExitPositionWorkflowCommand);
    [IgnoreMember] public BoundedContextName RouteTo => BoundedContextName.StrategyPositionExitWorkflowBoundedContext;
    [IgnoreMember] public string StreamId => Subject.StreamId;
    [IgnoreMember] public string EventSource => Actor;
    [IgnoreMember] public int ErrorCode => 27201;
}

[MessagePackObject]
public sealed record StartVerticalSpreadExitPositionWorkflowCommand : ICommand<ExitPositionWorkflowId>
{
    public const string Actor = "VerticalSpreadExitPositionWorkflow";
    public const string Verb = "StartVerticalSpreadExitPositionWorkflow";
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; } = true;
    [Key(3)] public ExitPositionWorkflowId EntityId { get; init; }
    [Key(4)] public VerticalSpreadTradePlanUpdatedEvent ExitPlan { get; init; } = new();
    [IgnoreMember] public string CommandName => nameof(StartVerticalSpreadExitPositionWorkflowCommand);
    [IgnoreMember] public BoundedContextName RouteTo => BoundedContextName.StrategyPositionExitWorkflowBoundedContext;
    [IgnoreMember] public string StreamId => Subject.StreamId;
    [IgnoreMember] public string EventSource => Actor;
    [IgnoreMember] public int ErrorCode => 27202;
}

[MessagePackObject]
public sealed record StartFuturesExitPositionWorkflowCommand : ICommand<ExitPositionWorkflowId>
{
    public const string Actor = "FuturesExitPositionWorkflow";
    public const string Verb = "StartFuturesExitPositionWorkflow";
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; } = true;
    [Key(3)] public ExitPositionWorkflowId EntityId { get; init; }
    [Key(4)] public FuturesTradePlanUpdatedEvent ExitPlan { get; init; } = new();
    [IgnoreMember] public string CommandName => nameof(StartFuturesExitPositionWorkflowCommand);
    [IgnoreMember] public BoundedContextName RouteTo => BoundedContextName.StrategyPositionExitWorkflowBoundedContext;
    [IgnoreMember] public string StreamId => Subject.StreamId;
    [IgnoreMember] public string EventSource => Actor;
    [IgnoreMember] public int ErrorCode => 27203;
}
