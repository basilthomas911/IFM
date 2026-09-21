using MessagePack;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;

namespace TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition;

public enum PortfolioOrderCompositionStatus : byte { ExecuteTradeOrders = 1, NoTradeOrders = 2 }

/// <summary>Typed request boundary for the atomic Portfolio order-composition Function actor.</summary>
public interface IPortfolioOrderCompositionApi
{
    ValueTask<ServiceResult<FunctionResult<PortfolioOrderCompositionCompletedEvent,PortfolioOrderCompositionFailedEvent>>> EvaluateAsync(
        EvaluatePortfolioOrderCompositionCommand request,CancellationToken cancellationToken=default);
    ValueTask<ServiceResult<FunctionResult<PortfolioCloseOrderCompositionCompletedEvent,PortfolioCloseOrderCompositionFailedEvent>>> EvaluateCloseAsync(
        EvaluatePortfolioCloseOrderCompositionCommand request,CancellationToken cancellationToken=default);
}

[MessagePackObject]
public sealed record PortfolioOrderCandidate
{
    [Key(0)] public Guid CompositionId { get; init; }
    [Key(1)] public Guid WorkflowId { get; init; }
    [Key(2)] public string DecisionHorizon { get; init; } = string.Empty;
    [Key(3)] public TradeStrategyKind StrategyKind { get; init; }
    [Key(4)] public DateOnly ValueDate { get; init; }
    [Key(5)] public DateTime ValidUntilUtc { get; init; }
    [Key(6)] public string Origin { get; init; } = string.Empty;
    [Key(7)] public TradeOrderComponentDefinition[] Components { get; init; } = [];
    [Key(8)] public decimal RequiredCapital { get; init; }
    [Key(9)] public string EvidenceHash { get; init; } = string.Empty;
    [Key(10)] public CatalogKey DeploymentKey { get; init; }
    [Key(11)] public decimal MaximumLoss { get; init; }
    [Key(12)] public decimal StressLoss { get; init; }
    [Key(13)] public decimal Notional { get; init; }
    [Key(14)] public string ProductSymbol { get; init; } = string.Empty;
    [Key(15)] public string ProductExchange { get; init; } = string.Empty;
    [Key(16)] public string ProductCurrency { get; init; } = string.Empty;
    [Key(17)] public decimal Delta { get; init; }
    [Key(18)] public decimal Gamma { get; init; }
    [Key(19)] public decimal Vega { get; init; }
    [Key(20)] public TradeOrderPositionType PositionType { get; init; }
    [Key(21)] public string BrokerAccountAlias { get; init; } = string.Empty;
    [Key(22)] public BrokerEnvironment BrokerEnvironment { get; init; }
    [Key(23)] public Guid PortfolioApprovalId { get; init; }
    [Key(24)] public string MicroExecutionProfileId { get; init; } = string.Empty;
    [Key(25)] public int MicroExecutionProfileVersion { get; init; }
    [Key(26)] public string MicroExecutionProfileHash { get; init; } = string.Empty;
    [Key(27)] public string AccountPromotionApprovalReference { get; init; } = string.Empty;
    [Key(28)] public BrokerOrderType BrokerOrderType { get; init; } = BrokerOrderType.Limit;
    [Key(29)] public BrokerAlgorithm BrokerAlgorithm { get; init; } = BrokerAlgorithm.None;
    [Key(30)] public VolatilityWorkflowInput? VolatilityEvidence { get; init; }
}

[MessagePackObject]
public sealed record EvaluatePortfolioOrderCompositionCommand : ICommand<FinancialExecutionId>, IFinancialRequest<PortfolioOrderCandidate>
{
    public const string Actor = "PortfolioOrderCompositionFunction";
    public const string Verb = "Evaluate";
    [Key(0)] public int SchemaVersion { get; init; } = 1;
    [Key(1)] public Guid CommandId { get; init; }
    [Key(2)] public ActorSubject Subject { get; init; } = new(ActorType.Function, Actor, Verb, string.Empty);
    [Key(3)] public bool PostEvents { get; init; }
    [Key(4)] public FinancialExecutionId EntityId { get; init; } = new(0, Guid.Empty);
    [Key(5)] public int ErrorCode { get; init; } = 34130;
    [Key(6)] public BoundedContextName RouteTo { get; init; } = BoundedContextName.PortfolioOrderCompositionBoundedContext;
    [Key(7)] public Guid OperationId { get; init; }
    [Key(8)] public int PortfolioId { get; init; }
    [Key(9)] public Guid CorrelationId { get; init; }
    [Key(10)] public Guid CausationId { get; init; }
    [Key(11)] public DateTime RequestedAtUtc { get; init; }
    [Key(12)] public DateTime ExpiresAtUtc { get; init; }
    [Key(13)] public long ExpectedFinancialRevision { get; init; }
    [Key(14)] public PortfolioOrderCandidate Body { get; init; } = new();
    [Key(15)] public string InputSha256 { get; init; } = string.Empty;
    [Key(16)] public FinancialAccess Access { get; init; } = new(string.Empty, []);
    [IgnoreMember] public string CommandName => nameof(EvaluatePortfolioOrderCompositionCommand);
    [IgnoreMember] public string StreamId => Subject.StreamId;
    [IgnoreMember] public string EventSource => Actor;
}

[MessagePackObject]
public sealed record PortfolioFundOrderDecision(
    [property: Key(0)] int FundId,
    [property: Key(1)] bool Accepted,
    [property: Key(2)] string ReasonCode,
    [property: Key(3)] int? OrderId);

[MessagePackObject]
public sealed record PortfolioAcceptedCapacityEffect
{
    [Key(0)] public int PortfolioId { get; init; }
    [Key(1)] public int FundId { get; init; }
    [Key(2)] public int OrderId { get; init; }
    [Key(3)] public string UnderlyingScopeKey { get; init; } = string.Empty;
    [Key(4)] public decimal RequiredCash { get; init; }
    [Key(5)] public CapacityExposure[] Exposures { get; init; } = [];
}

[MessagePackObject]
public sealed record PortfolioFundFinancialSnapshot(
    [property: Key(0)] int FundId,
    [property: Key(1)] decimal AvailableCash,
    [property: Key(2)] CapacityUsed[] Usage);

[MessagePackObject]
public sealed record PortfolioOrderCompositionReceipt
{
    [Key(0)] public Guid CompositionId { get; init; }
    [Key(1)] public Guid WorkflowId { get; init; }
    [Key(2)] public PortfolioOrderCompositionStatus Status { get; init; }
    [Key(3)] public PortfolioFundOrderDecision[] FundDecisions { get; init; } = [];
    [Key(4)] public TradeOrderDefinition[] TradeOrders { get; init; } = [];
    [Key(5)] public long FinancialRevision { get; init; }
    [Key(6)] public int PortfolioId { get; init; }
    [Key(7)] public PortfolioAcceptedCapacityEffect[] CapacityEffects { get; init; } = [];
    [Key(8)] public VolatilityWorkflowInput? VolatilityEvidence { get; init; }
}

[MessagePackObject]
public sealed record PortfolioOrderCompositionCompletedEvent : ICompleteEvent<FinancialExecutionId>, IFinancialCompletedEvent
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
    [Key(11)] public PortfolioOrderCompositionReceipt Receipt { get; init; } = new();
    [Key(12)] public long EventId { get; init; }
    [Key(13)] public string AggregateId { get; init; } = string.Empty;
    [Key(14)] public string EventSource { get; init; } = EvaluatePortfolioOrderCompositionCommand.Actor;
    [Key(15)] public DateTime ReceivedOn { get; init; }
    [IgnoreMember] public string UserName => "Portfolio";
    [IgnoreMember] public string EventName => nameof(PortfolioOrderCompositionCompletedEvent);
    [IgnoreMember] public EventType EventType => EventType.CompletedEvent;
}

[MessagePackObject]
public sealed record PortfolioOrderCompositionFailedEvent : IErrorEvent<FinancialExecutionId>
{
    [Key(0)] public Guid Id { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; } = ActorSubject.Unknown;
    [Key(2)] public FinancialExecutionId EntityId { get; init; } = new(0, Guid.Empty);
    [Key(3)] public Guid CommandId { get; init; }
    [Key(4)] public DateTime ErrorDate { get; init; }
    [Key(5)] public int ErrorCode { get; init; } = 34130;
    [Key(6)] public string ErrorMessage { get; init; } = string.Empty;
    [Key(7)] public ErrorType ErrorType { get; init; }
    [Key(8)] public string ErrorData { get; init; } = string.Empty;
    [Key(9)] public string CommandName { get; init; } = string.Empty;
    [Key(10)] public string CommandData { get; init; } = string.Empty;
    [Key(11)] public long EventId { get; init; }
    [Key(12)] public string AggregateId { get; init; } = string.Empty;
    [Key(13)] public string EventSource { get; init; } = EvaluatePortfolioOrderCompositionCommand.Actor;
    [Key(14)] public DateTime ReceivedOn { get; init; }
    [IgnoreMember] public string UserName => "Portfolio";
    [IgnoreMember] public string EventName => nameof(PortfolioOrderCompositionFailedEvent);
    [IgnoreMember] public EventType EventType => EventType.ErrorEvent;
}
