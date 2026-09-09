using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;

/// <summary>Principal asserted through the authenticated NATS subject ACL boundary; domain permissions remain explicit.</summary>
[MessagePackObject]
public sealed record FinancialAccess([property: Key(0)] string Principal, [property: Key(1)] string[] Roles,
    [property: Key(2)] int[]? PortfolioIds=null);

public interface IFinancialRequest : ICommand
{
    bool PostEvents { get; }
    int SchemaVersion { get; }
    Guid OperationId { get; }
    int PortfolioId { get; }
    Guid CorrelationId { get; }
    Guid CausationId { get; }
    DateTime RequestedAtUtc { get; }
    DateTime ExpiresAtUtc { get; }
    long ExpectedFinancialRevision { get; }
    string InputSha256 { get; }
    FinancialAccess Access { get; }
}
public interface IFinancialRequest<out TBody> : IFinancialRequest { TBody Body { get; } }

/// <summary>Post through the GeneralLedgerCommand actor; retries preserve operation identity and semantic input hash.</summary>
[MessagePackObject]
public sealed record PostFundTransactionCommand : ICommand<LedgerPortfolioId>, IFinancialRequest<LedgerPostingRequest>
{
    public const string Actor = "GeneralLedgerCommand";
    public const string Verb = "Post";
    [Key(0)] public int SchemaVersion { get; init; } = 1;
    [Key(1)] public Guid CommandId { get; init; } = Guid.Empty;
    [Key(2)] public ActorSubject Subject { get; init; } = new(ActorType.Command, Actor, Verb, string.Empty);
    [Key(3)] public bool PostEvents { get; init; } = true;
    [Key(4)] public LedgerPortfolioId EntityId { get; init; } = new(0);
    [Key(5)] public int ErrorCode { get; init; } = 34120;
    [Key(6)] public BoundedContextName RouteTo { get; init; } = BoundedContextName.GeneralLedgerBoundedContext;
    [Key(7)] public Guid OperationId { get; init; } = Guid.Empty;
    [Key(8)] public int PortfolioId { get; init; } = 0;
    [Key(9)] public Guid CorrelationId { get; init; } = Guid.Empty;
    [Key(10)] public Guid CausationId { get; init; } = Guid.Empty;
    [Key(11)] public DateTime RequestedAtUtc { get; init; } = default;
    [Key(12)] public DateTime ExpiresAtUtc { get; init; } = default;
    [Key(13)] public long ExpectedFinancialRevision { get; init; } = 0;
    [Key(14)] public LedgerPostingRequest Body { get; init; } = new();
    [Key(15)] public string InputSha256 { get; init; } = string.Empty;
    [Key(16)] public FinancialAccess Access { get; init; } = new(string.Empty, []);
    [IgnoreMember] public string CommandName => nameof(PostFundTransactionCommand);
    [IgnoreMember] public string StreamId => Subject.StreamId;
    [IgnoreMember] public string EventSource => Actor;
}

/// <summary>PostBatch through the GeneralLedgerCommand actor; retries preserve operation identity and semantic input hash.</summary>
[MessagePackObject]
public sealed record PostFundTransactionsCommand : ICommand<LedgerPortfolioId>, IFinancialRequest<LedgerPostingBatchRequest>
{
    public const string Actor = "GeneralLedgerCommand";
    public const string Verb = "PostBatch";
    [Key(0)] public int SchemaVersion { get; init; } = 1;
    [Key(1)] public Guid CommandId { get; init; } = Guid.Empty;
    [Key(2)] public ActorSubject Subject { get; init; } = new(ActorType.Command, Actor, Verb, string.Empty);
    [Key(3)] public bool PostEvents { get; init; } = true;
    [Key(4)] public LedgerPortfolioId EntityId { get; init; } = new(0);
    [Key(5)] public int ErrorCode { get; init; } = 34121;
    [Key(6)] public BoundedContextName RouteTo { get; init; } = BoundedContextName.GeneralLedgerBoundedContext;
    [Key(7)] public Guid OperationId { get; init; } = Guid.Empty;
    [Key(8)] public int PortfolioId { get; init; } = 0;
    [Key(9)] public Guid CorrelationId { get; init; } = Guid.Empty;
    [Key(10)] public Guid CausationId { get; init; } = Guid.Empty;
    [Key(11)] public DateTime RequestedAtUtc { get; init; } = default;
    [Key(12)] public DateTime ExpiresAtUtc { get; init; } = default;
    [Key(13)] public long ExpectedFinancialRevision { get; init; } = 0;
    [Key(14)] public LedgerPostingBatchRequest Body { get; init; } = new();
    [Key(15)] public string InputSha256 { get; init; } = string.Empty;
    [Key(16)] public FinancialAccess Access { get; init; } = new(string.Empty, []);
    [IgnoreMember] public string CommandName => nameof(PostFundTransactionsCommand);
    [IgnoreMember] public string StreamId => Subject.StreamId;
    [IgnoreMember] public string EventSource => Actor;
}

/// <summary>Reserve through the CapacityReservationFunction actor; retries preserve operation identity and semantic input hash.</summary>
[MessagePackObject]
public sealed record ReservePortfolioTradeRiskCommand : ICommand<FinancialExecutionId>, IFinancialRequest<CapacityReservationRequest>
{
    public const string Actor = "CapacityReservationFunction";
    public const string Verb = "Reserve";
    [Key(0)] public int SchemaVersion { get; init; } = 1;
    [Key(1)] public Guid CommandId { get; init; } = Guid.Empty;
    [Key(2)] public ActorSubject Subject { get; init; } = new(ActorType.Function, Actor, Verb, string.Empty);
    [Key(3)] public bool PostEvents { get; init; } = false;
    [Key(4)] public FinancialExecutionId EntityId { get; init; } = new(0, Guid.Empty);
    [Key(5)] public int ErrorCode { get; init; } = 34122;
    [Key(6)] public BoundedContextName RouteTo { get; init; } = BoundedContextName.CapacityReservationBoundedContext;
    [Key(7)] public Guid OperationId { get; init; } = Guid.Empty;
    [Key(8)] public int PortfolioId { get; init; } = 0;
    [Key(9)] public Guid CorrelationId { get; init; } = Guid.Empty;
    [Key(10)] public Guid CausationId { get; init; } = Guid.Empty;
    [Key(11)] public DateTime RequestedAtUtc { get; init; } = default;
    [Key(12)] public DateTime ExpiresAtUtc { get; init; } = default;
    [Key(13)] public long ExpectedFinancialRevision { get; init; } = 0;
    [Key(14)] public CapacityReservationRequest Body { get; init; } = new();
    [Key(15)] public string InputSha256 { get; init; } = string.Empty;
    [Key(16)] public FinancialAccess Access { get; init; } = new(string.Empty, []);
    [IgnoreMember] public string CommandName => nameof(ReservePortfolioTradeRiskCommand);
    [IgnoreMember] public string StreamId => Subject.StreamId;
    [IgnoreMember] public string EventSource => Actor;
}

/// <summary>Consume through the CapacityConsumptionFunction actor; retries preserve operation identity and semantic input hash.</summary>
[MessagePackObject]
public sealed record ConsumeCapacityReservationCommand : ICommand<FinancialExecutionId>, IFinancialRequest<CapacityLifecycleRequest>
{
    public const string Actor = "CapacityConsumptionFunction";
    public const string Verb = "Consume";
    [Key(0)] public int SchemaVersion { get; init; } = 1;
    [Key(1)] public Guid CommandId { get; init; } = Guid.Empty;
    [Key(2)] public ActorSubject Subject { get; init; } = new(ActorType.Function, Actor, Verb, string.Empty);
    [Key(3)] public bool PostEvents { get; init; } = false;
    [Key(4)] public FinancialExecutionId EntityId { get; init; } = new(0, Guid.Empty);
    [Key(5)] public int ErrorCode { get; init; } = 34123;
    [Key(6)] public BoundedContextName RouteTo { get; init; } = BoundedContextName.CapacityReservationBoundedContext;
    [Key(7)] public Guid OperationId { get; init; } = Guid.Empty;
    [Key(8)] public int PortfolioId { get; init; } = 0;
    [Key(9)] public Guid CorrelationId { get; init; } = Guid.Empty;
    [Key(10)] public Guid CausationId { get; init; } = Guid.Empty;
    [Key(11)] public DateTime RequestedAtUtc { get; init; } = default;
    [Key(12)] public DateTime ExpiresAtUtc { get; init; } = default;
    [Key(13)] public long ExpectedFinancialRevision { get; init; } = 0;
    [Key(14)] public CapacityLifecycleRequest Body { get; init; } = new();
    [Key(15)] public string InputSha256 { get; init; } = string.Empty;
    [Key(16)] public FinancialAccess Access { get; init; } = new(string.Empty, []);
    [IgnoreMember] public string CommandName => nameof(ConsumeCapacityReservationCommand);
    [IgnoreMember] public string StreamId => Subject.StreamId;
    [IgnoreMember] public string EventSource => Actor;
}

/// <summary>Change through the CapacityReservationCommand actor; retries preserve operation identity and semantic input hash.</summary>
[MessagePackObject]
public sealed record ChangeCapacityReservationCommand : ICommand<CapacityReservationEntityId>, IFinancialRequest<CapacityLifecycleRequest>
{
    public const string Actor = "CapacityReservationCommand";
    public const string Verb = "Change";
    [Key(0)] public int SchemaVersion { get; init; } = 1;
    [Key(1)] public Guid CommandId { get; init; } = Guid.Empty;
    [Key(2)] public ActorSubject Subject { get; init; } = new(ActorType.Command, Actor, Verb, string.Empty);
    [Key(3)] public bool PostEvents { get; init; } = true;
    [Key(4)] public CapacityReservationEntityId EntityId { get; init; } = new(0, Guid.Empty);
    [Key(5)] public int ErrorCode { get; init; } = 34124;
    [Key(6)] public BoundedContextName RouteTo { get; init; } = BoundedContextName.CapacityReservationBoundedContext;
    [Key(7)] public Guid OperationId { get; init; } = Guid.Empty;
    [Key(8)] public int PortfolioId { get; init; } = 0;
    [Key(9)] public Guid CorrelationId { get; init; } = Guid.Empty;
    [Key(10)] public Guid CausationId { get; init; } = Guid.Empty;
    [Key(11)] public DateTime RequestedAtUtc { get; init; } = default;
    [Key(12)] public DateTime ExpiresAtUtc { get; init; } = default;
    [Key(13)] public long ExpectedFinancialRevision { get; init; } = 0;
    [Key(14)] public CapacityLifecycleRequest Body { get; init; } = new();
    [Key(15)] public string InputSha256 { get; init; } = string.Empty;
    [Key(16)] public FinancialAccess Access { get; init; } = new(string.Empty, []);
    [IgnoreMember] public string CommandName => nameof(ChangeCapacityReservationCommand);
    [IgnoreMember] public string StreamId => Subject.StreamId;
    [IgnoreMember] public string EventSource => Actor;
}
