using MessagePack;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition;

public enum PortfolioCloseOrderCompositionStatus : byte
{
    ExecuteTradeOrder = 1,
    NoTradeOrder = 2
}

/// <summary>Complete broker-neutral request to reduce one existing strategy position.</summary>
[MessagePackObject]
public sealed record PortfolioCloseOrderCandidate
{
    [Key(0)] public Guid CompositionId { get; init; }
    [Key(1)] public ExitPositionWorkflowId WorkflowId { get; init; }
    [Key(2)] public StrategyPositionSnapshot Position { get; init; } = new();
    [Key(3)] public TradeStrategyKind StrategyKind { get; init; }
    [Key(4)] public DateOnly ValueDate { get; init; }
    [Key(5)] public DateTime ValidUntilUtc { get; init; }
    [Key(6)] public string Origin { get; init; } = string.Empty;
    [Key(7)] public TradeOrderComponentDefinition Component { get; init; } = new();
    [Key(8)] public string EvidenceHash { get; init; } = string.Empty;
    [Key(9)] public TradeOrderPositionType PositionType { get; init; }
}

[MessagePackObject]
public sealed record EvaluatePortfolioCloseOrderCompositionCommand :
    ICommand<FinancialExecutionId>, IFinancialRequest<PortfolioCloseOrderCandidate>
{
    public const string Actor = "PortfolioCloseOrderCompositionFunction";
    public const string Verb = "EvaluateClose";
    [Key(0)] public int SchemaVersion { get; init; } = 1;
    [Key(1)] public Guid CommandId { get; init; }
    [Key(2)] public ActorSubject Subject { get; init; } = new(ActorType.Function, Actor, Verb, string.Empty);
    [Key(3)] public bool PostEvents { get; init; }
    [Key(4)] public FinancialExecutionId EntityId { get; init; } = new(0, Guid.Empty);
    [Key(5)] public int ErrorCode { get; init; } = 34131;
    [Key(6)] public BoundedContextName RouteTo { get; init; } = BoundedContextName.PortfolioOrderCompositionBoundedContext;
    [Key(7)] public Guid OperationId { get; init; }
    [Key(8)] public int PortfolioId { get; init; }
    [Key(9)] public Guid CorrelationId { get; init; }
    [Key(10)] public Guid CausationId { get; init; }
    [Key(11)] public DateTime RequestedAtUtc { get; init; }
    [Key(12)] public DateTime ExpiresAtUtc { get; init; }
    [Key(13)] public long ExpectedFinancialRevision { get; init; }
    [Key(14)] public PortfolioCloseOrderCandidate Body { get; init; } = new();
    [Key(15)] public string InputSha256 { get; init; } = string.Empty;
    [Key(16)] public FinancialAccess Access { get; init; } = new(string.Empty, []);
    [IgnoreMember] public string CommandName => nameof(EvaluatePortfolioCloseOrderCompositionCommand);
    [IgnoreMember] public string StreamId => Subject.StreamId;
    [IgnoreMember] public string EventSource => Actor;
}

[MessagePackObject]
public sealed record PortfolioCloseOrderCompositionReceipt
{
    [Key(0)] public Guid CompositionId { get; init; }
    [Key(1)] public ExitPositionWorkflowId WorkflowId { get; init; }
    [Key(2)] public PortfolioCloseOrderCompositionStatus Status { get; init; }
    [Key(3)] public TradeOrderDefinition? TradeOrder { get; init; }
    [Key(4)] public long FinancialRevision { get; init; }
    [Key(5)] public int PortfolioId { get; init; }
    [Key(6)] public string ReasonCode { get; init; } = string.Empty;
}

[MessagePackObject]
public sealed record PortfolioCloseOrderCompositionCompletedEvent :
    ICompleteEvent<FinancialExecutionId>, IFinancialCompletedEvent
{
    [Key(0)] public int SchemaVersion { get; init; } = 1;
    [Key(1)] public Guid Id { get; init; }
    [Key(2)] public ActorSubject Subject { get; init; } = ActorSubject.Unknown;
    [Key(3)] public FinancialExecutionId EntityId { get; init; } = new(0, Guid.Empty);
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public Guid OperationId { get; init; }
    [Key(6)] public int PortfolioId { get; init; }
    [Key(7)] public Guid CorrelationId { get; init; }
    [Key(8)] public Guid CausationId { get; init; }
    [Key(9)] public DateTime CommittedAtUtc { get; init; }
    [Key(10)] public string InputHash { get; init; } = string.Empty;
    [Key(11)] public PortfolioCloseOrderCompositionReceipt Receipt { get; init; } = new();
    [Key(12)] public long EventId { get; init; }
    [Key(13)] public string AggregateId { get; init; } = string.Empty;
    [Key(14)] public string EventSource { get; init; } = EvaluatePortfolioCloseOrderCompositionCommand.Actor;
    [Key(15)] public DateTime ReceivedOn { get; init; }
    [IgnoreMember] public string UserName => "Portfolio";
    [IgnoreMember] public string EventName => nameof(PortfolioCloseOrderCompositionCompletedEvent);
    [IgnoreMember] public EventType EventType => EventType.CompletedEvent;
}

[MessagePackObject]
public sealed record PortfolioCloseOrderCompositionFailedEvent : IErrorEvent<FinancialExecutionId>
{
    [Key(0)] public Guid Id { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; } = ActorSubject.Unknown;
    [Key(2)] public FinancialExecutionId EntityId { get; init; } = new(0, Guid.Empty);
    [Key(3)] public Guid CommandId { get; init; }
    [Key(4)] public DateTime ErrorDate { get; init; }
    [Key(5)] public int ErrorCode { get; init; } = 34131;
    [Key(6)] public string ErrorMessage { get; init; } = string.Empty;
    [Key(7)] public ErrorType ErrorType { get; init; }
    [Key(8)] public string ErrorData { get; init; } = string.Empty;
    [Key(9)] public string CommandName { get; init; } = string.Empty;
    [Key(10)] public string CommandData { get; init; } = string.Empty;
    [Key(11)] public long EventId { get; init; }
    [Key(12)] public string AggregateId { get; init; } = string.Empty;
    [Key(13)] public string EventSource { get; init; } = EvaluatePortfolioCloseOrderCompositionCommand.Actor;
    [Key(14)] public DateTime ReceivedOn { get; init; }
    [IgnoreMember] public string UserName => "Portfolio";
    [IgnoreMember] public string EventName => nameof(PortfolioCloseOrderCompositionFailedEvent);
    [IgnoreMember] public EventType EventType => EventType.ErrorEvent;
}
