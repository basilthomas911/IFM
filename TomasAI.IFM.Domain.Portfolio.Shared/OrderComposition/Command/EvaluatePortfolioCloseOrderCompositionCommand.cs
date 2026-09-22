using MessagePack;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition;

/// <summary>Complete broker-neutral request to reduce one existing strategy position.</summary>
[MessagePackObject]
public sealed record PortfolioCloseOrderCandidate
{
    [Key(0)] public Guid CompositionId { get; init; }
    [Key(1)] public PortfolioExitWorkflowId WorkflowId { get; init; }
    [Key(2)] public PortfolioPositionSnapshot Position { get; init; } = new();
    [Key(3)] public PortfolioExecutionStrategyKind StrategyKind { get; init; }
    [Key(4)] public DateOnly ValueDate { get; init; }
    [Key(5)] public DateTime ValidUntilUtc { get; init; }
    [Key(6)] public string Origin { get; init; } = string.Empty;
    [Key(7)] public PortfolioExecutionComponent Component { get; init; } = new();
    [Key(8)] public string EvidenceHash { get; init; } = string.Empty;
    [Key(9)] public PortfolioExecutionPositionType PositionType { get; init; }
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
