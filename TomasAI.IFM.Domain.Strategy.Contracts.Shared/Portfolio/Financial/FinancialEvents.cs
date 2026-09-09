using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventProjector;
namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;

public interface IFinancialCompletedEvent : IEvent
{
    Guid OperationId { get; }
    int PortfolioId { get; }
    string InputHash { get; }
    DateTime CommittedAtUtc { get; }
}

/// <summary>Immutable committed LedgerPosting receipt; retries return its original identity and financial revision.</summary>
[MessagePackObject]
public sealed record LedgerPostingCompletedEvent : ICompleteEvent<LedgerPortfolioId>, IFinancialCompletedEvent, IRequireDurableProjection
{
    [Key(0)] public int SchemaVersion { get; init; } = 1;
    [Key(1)] public Guid Id { get; init; } = Guid.Empty;
    [Key(2)] public ActorSubject Subject { get; init; } = ActorSubject.Unknown;
    [Key(3)] public LedgerPortfolioId EntityId { get; init; } = new(0);
    [Key(4)] public Guid CommandId { get; init; } = Guid.Empty;
    [Key(5)] public Guid OperationId { get; init; } = Guid.Empty;
    [Key(6)] public int PortfolioId { get; init; } = 0;
    [Key(7)] public Guid CorrelationId { get; init; } = Guid.Empty;
    [Key(8)] public Guid CausationId { get; init; } = Guid.Empty;
    [Key(9)] public DateTime CommittedAtUtc { get; init; } = default;
    [Key(10)] public string InputHash { get; init; } = string.Empty;
    [Key(11)] public LedgerPostingReceipt Receipt { get; init; } = new();
    [Key(12)] public long EventId { get; init; } = 0;
    [Key(13)] public string AggregateId { get; init; } = string.Empty;
    [Key(14)] public string EventSource { get; init; } = "GeneralLedgerCommand";
    [Key(15)] public DateTime ReceivedOn { get; init; }
    [IgnoreMember] public string UserName => "PortfolioFinancial";
    [IgnoreMember] public string EventName => nameof(LedgerPostingCompletedEvent);
    [IgnoreMember] public EventType EventType => EventType.CompletedEvent;
    [IgnoreMember] public DurableProjectionRequirement RequiredProjection => new("GeneralLedgerCommandActor", "GeneralLedgerProjector", EventProjectorStageType.ApplyProjection);
}

/// <summary>Classified LedgerPosting refusal/uncertain outcome; never evidence of a successful financial mutation.</summary>
[MessagePackObject]
public sealed record LedgerPostingFailedEvent : IErrorEvent<LedgerPortfolioId>
{
    [Key(0)] public int SchemaVersion { get; init; } = 1;
    [Key(1)] public Guid Id { get; init; } = Guid.Empty;
    [Key(2)] public ActorSubject Subject { get; init; } = ActorSubject.Unknown;
    [Key(3)] public LedgerPortfolioId EntityId { get; init; } = new(0);
    [Key(4)] public Guid CommandId { get; init; } = Guid.Empty;
    [Key(5)] public Guid OperationId { get; init; } = Guid.Empty;
    [Key(6)] public int PortfolioId { get; init; } = 0;
    [Key(7)] public Guid CorrelationId { get; init; } = Guid.Empty;
    [Key(8)] public Guid CausationId { get; init; } = Guid.Empty;
    [Key(9)] public DateTime FailedAtUtc { get; init; } = default;
    [Key(10)] public int ErrorCode { get; init; } = FinancialReasons.InvalidContract;
    [Key(11)] public string ReasonCode { get; init; } = string.Empty;
    [Key(12)] public string FailureClass { get; init; } = string.Empty;
    [Key(13)] public FinancialCommitDisposition CommitDisposition { get; init; } = FinancialCommitDisposition.NotCommitted;
    [Key(14)] public long ExpectedRevision { get; init; } = 0;
    [Key(15)] public long? ObservedRevision { get; init; } = null;
    [Key(16)] public string Message { get; init; } = string.Empty;
    [Key(17)] public Guid? ExistingOperationId { get; init; } = null;
    [Key(18)] public long EventId { get; init; } = 0;
    [Key(19)] public string AggregateId { get; init; } = string.Empty;
    [Key(20)] public string EventSource { get; init; } = "GeneralLedgerCommand";
    [Key(21)] public DateTime ReceivedOn { get; init; }
    [IgnoreMember] public DateTime ErrorDate => FailedAtUtc;
    [IgnoreMember] public string ErrorMessage { get => Message; init => Message = value; }
    [Key(22)] public ErrorType ErrorType { get; init; }
    [IgnoreMember] public string ErrorData { get => ReasonCode; init => ReasonCode = value; }
    [Key(23)] public string CommandName { get; init; } = string.Empty;
    [Key(24)] public string CommandData { get; init; } = string.Empty;
    [IgnoreMember] public string UserName => "PortfolioFinancial";
    [IgnoreMember] public string EventName => nameof(LedgerPostingFailedEvent);
    [IgnoreMember] public EventType EventType => EventType.ErrorEvent;
}

/// <summary>Immutable committed LedgerPostingBatch receipt; retries return its original identity and financial revision.</summary>
[MessagePackObject]
public sealed record LedgerPostingBatchCompletedEvent : ICompleteEvent<LedgerPortfolioId>, IFinancialCompletedEvent, IRequireDurableProjection
{
    [Key(0)] public int SchemaVersion { get; init; } = 1;
    [Key(1)] public Guid Id { get; init; } = Guid.Empty;
    [Key(2)] public ActorSubject Subject { get; init; } = ActorSubject.Unknown;
    [Key(3)] public LedgerPortfolioId EntityId { get; init; } = new(0);
    [Key(4)] public Guid CommandId { get; init; } = Guid.Empty;
    [Key(5)] public Guid OperationId { get; init; } = Guid.Empty;
    [Key(6)] public int PortfolioId { get; init; } = 0;
    [Key(7)] public Guid CorrelationId { get; init; } = Guid.Empty;
    [Key(8)] public Guid CausationId { get; init; } = Guid.Empty;
    [Key(9)] public DateTime CommittedAtUtc { get; init; } = default;
    [Key(10)] public string InputHash { get; init; } = string.Empty;
    [Key(11)] public LedgerPostingBatchReceipt Receipt { get; init; } = new();
    [Key(12)] public long EventId { get; init; } = 0;
    [Key(13)] public string AggregateId { get; init; } = string.Empty;
    [Key(14)] public string EventSource { get; init; } = "GeneralLedgerCommand";
    [Key(15)] public DateTime ReceivedOn { get; init; }
    [IgnoreMember] public string UserName => "PortfolioFinancial";
    [IgnoreMember] public string EventName => nameof(LedgerPostingBatchCompletedEvent);
    [IgnoreMember] public EventType EventType => EventType.CompletedEvent;
    [IgnoreMember] public DurableProjectionRequirement RequiredProjection => new("GeneralLedgerCommandActor", "GeneralLedgerProjector", EventProjectorStageType.ApplyProjection);
}

/// <summary>Classified LedgerPostingBatch refusal/uncertain outcome; never evidence of a successful financial mutation.</summary>
[MessagePackObject]
public sealed record LedgerPostingBatchFailedEvent : IErrorEvent<LedgerPortfolioId>
{
    [Key(0)] public int SchemaVersion { get; init; } = 1;
    [Key(1)] public Guid Id { get; init; } = Guid.Empty;
    [Key(2)] public ActorSubject Subject { get; init; } = ActorSubject.Unknown;
    [Key(3)] public LedgerPortfolioId EntityId { get; init; } = new(0);
    [Key(4)] public Guid CommandId { get; init; } = Guid.Empty;
    [Key(5)] public Guid OperationId { get; init; } = Guid.Empty;
    [Key(6)] public int PortfolioId { get; init; } = 0;
    [Key(7)] public Guid CorrelationId { get; init; } = Guid.Empty;
    [Key(8)] public Guid CausationId { get; init; } = Guid.Empty;
    [Key(9)] public DateTime FailedAtUtc { get; init; } = default;
    [Key(10)] public int ErrorCode { get; init; } = FinancialReasons.InvalidContract;
    [Key(11)] public string ReasonCode { get; init; } = string.Empty;
    [Key(12)] public string FailureClass { get; init; } = string.Empty;
    [Key(13)] public FinancialCommitDisposition CommitDisposition { get; init; } = FinancialCommitDisposition.NotCommitted;
    [Key(14)] public long ExpectedRevision { get; init; } = 0;
    [Key(15)] public long? ObservedRevision { get; init; } = null;
    [Key(16)] public string Message { get; init; } = string.Empty;
    [Key(17)] public Guid? ExistingOperationId { get; init; } = null;
    [Key(18)] public long EventId { get; init; } = 0;
    [Key(19)] public string AggregateId { get; init; } = string.Empty;
    [Key(20)] public string EventSource { get; init; } = "GeneralLedgerCommand";
    [Key(21)] public DateTime ReceivedOn { get; init; }
    [IgnoreMember] public DateTime ErrorDate => FailedAtUtc;
    [IgnoreMember] public string ErrorMessage { get => Message; init => Message = value; }
    [Key(22)] public ErrorType ErrorType { get; init; }
    [IgnoreMember] public string ErrorData { get => ReasonCode; init => ReasonCode = value; }
    [Key(23)] public string CommandName { get; init; } = string.Empty;
    [Key(24)] public string CommandData { get; init; } = string.Empty;
    [IgnoreMember] public string UserName => "PortfolioFinancial";
    [IgnoreMember] public string EventName => nameof(LedgerPostingBatchFailedEvent);
    [IgnoreMember] public EventType EventType => EventType.ErrorEvent;
}

/// <summary>Immutable committed CapacityReservation receipt; retries return its original identity and financial revision.</summary>
[MessagePackObject]
public sealed record CapacityReservationCompletedEvent : ICompleteEvent<FinancialExecutionId>, IFinancialCompletedEvent
{
    [Key(0)] public int SchemaVersion { get; init; } = 1;
    [Key(1)] public Guid Id { get; init; } = Guid.Empty;
    [Key(2)] public ActorSubject Subject { get; init; } = ActorSubject.Unknown;
    [Key(3)] public FinancialExecutionId EntityId { get; init; } = new(0, Guid.Empty);
    [Key(4)] public Guid CommandId { get; init; } = Guid.Empty;
    [Key(5)] public Guid OperationId { get; init; } = Guid.Empty;
    [Key(6)] public int PortfolioId { get; init; } = 0;
    [Key(7)] public Guid CorrelationId { get; init; } = Guid.Empty;
    [Key(8)] public Guid CausationId { get; init; } = Guid.Empty;
    [Key(9)] public DateTime CommittedAtUtc { get; init; } = default;
    [Key(10)] public string InputHash { get; init; } = string.Empty;
    [Key(11)] public CapacityReservationReceipt Receipt { get; init; } = new();
    [Key(12)] public long EventId { get; init; } = 0;
    [Key(13)] public string AggregateId { get; init; } = string.Empty;
    [Key(14)] public string EventSource { get; init; } = "CapacityReservationFunction";
    [Key(15)] public DateTime ReceivedOn { get; init; }
    [IgnoreMember] public string UserName => "PortfolioFinancial";
    [IgnoreMember] public string EventName => nameof(CapacityReservationCompletedEvent);
    [IgnoreMember] public EventType EventType => EventType.CompletedEvent;
}

/// <summary>Classified CapacityReservation refusal/uncertain outcome; never evidence of a successful financial mutation.</summary>
[MessagePackObject]
public sealed record CapacityReservationFailedEvent : IErrorEvent<FinancialExecutionId>
{
    [Key(0)] public int SchemaVersion { get; init; } = 1;
    [Key(1)] public Guid Id { get; init; } = Guid.Empty;
    [Key(2)] public ActorSubject Subject { get; init; } = ActorSubject.Unknown;
    [Key(3)] public FinancialExecutionId EntityId { get; init; } = new(0, Guid.Empty);
    [Key(4)] public Guid CommandId { get; init; } = Guid.Empty;
    [Key(5)] public Guid OperationId { get; init; } = Guid.Empty;
    [Key(6)] public int PortfolioId { get; init; } = 0;
    [Key(7)] public Guid CorrelationId { get; init; } = Guid.Empty;
    [Key(8)] public Guid CausationId { get; init; } = Guid.Empty;
    [Key(9)] public DateTime FailedAtUtc { get; init; } = default;
    [Key(10)] public int ErrorCode { get; init; } = FinancialReasons.InvalidContract;
    [Key(11)] public string ReasonCode { get; init; } = string.Empty;
    [Key(12)] public string FailureClass { get; init; } = string.Empty;
    [Key(13)] public FinancialCommitDisposition CommitDisposition { get; init; } = FinancialCommitDisposition.NotCommitted;
    [Key(14)] public long ExpectedRevision { get; init; } = 0;
    [Key(15)] public long? ObservedRevision { get; init; } = null;
    [Key(16)] public string Message { get; init; } = string.Empty;
    [Key(17)] public Guid? ExistingOperationId { get; init; } = null;
    [Key(18)] public long EventId { get; init; } = 0;
    [Key(19)] public string AggregateId { get; init; } = string.Empty;
    [Key(20)] public string EventSource { get; init; } = "CapacityReservationFunction";
    [Key(21)] public DateTime ReceivedOn { get; init; }
    [IgnoreMember] public DateTime ErrorDate => FailedAtUtc;
    [IgnoreMember] public string ErrorMessage { get => Message; init => Message = value; }
    [Key(22)] public ErrorType ErrorType { get; init; }
    [IgnoreMember] public string ErrorData { get => ReasonCode; init => ReasonCode = value; }
    [Key(23)] public string CommandName { get; init; } = string.Empty;
    [Key(24)] public string CommandData { get; init; } = string.Empty;
    [IgnoreMember] public string UserName => "PortfolioFinancial";
    [IgnoreMember] public string EventName => nameof(CapacityReservationFailedEvent);
    [IgnoreMember] public EventType EventType => EventType.ErrorEvent;
}

/// <summary>Immutable committed CapacityConsumption receipt; retries return its original identity and financial revision.</summary>
[MessagePackObject]
public sealed record CapacityConsumptionCompletedEvent : ICompleteEvent<FinancialExecutionId>, IFinancialCompletedEvent
{
    [Key(0)] public int SchemaVersion { get; init; } = 1;
    [Key(1)] public Guid Id { get; init; } = Guid.Empty;
    [Key(2)] public ActorSubject Subject { get; init; } = ActorSubject.Unknown;
    [Key(3)] public FinancialExecutionId EntityId { get; init; } = new(0, Guid.Empty);
    [Key(4)] public Guid CommandId { get; init; } = Guid.Empty;
    [Key(5)] public Guid OperationId { get; init; } = Guid.Empty;
    [Key(6)] public int PortfolioId { get; init; } = 0;
    [Key(7)] public Guid CorrelationId { get; init; } = Guid.Empty;
    [Key(8)] public Guid CausationId { get; init; } = Guid.Empty;
    [Key(9)] public DateTime CommittedAtUtc { get; init; } = default;
    [Key(10)] public string InputHash { get; init; } = string.Empty;
    [Key(11)] public CapacityLifecycleReceipt Receipt { get; init; } = new();
    [Key(12)] public long EventId { get; init; } = 0;
    [Key(13)] public string AggregateId { get; init; } = string.Empty;
    [Key(14)] public string EventSource { get; init; } = "CapacityConsumptionFunction";
    [Key(15)] public DateTime ReceivedOn { get; init; }
    [IgnoreMember] public string UserName => "PortfolioFinancial";
    [IgnoreMember] public string EventName => nameof(CapacityConsumptionCompletedEvent);
    [IgnoreMember] public EventType EventType => EventType.CompletedEvent;
}

/// <summary>Classified CapacityConsumption refusal/uncertain outcome; never evidence of a successful financial mutation.</summary>
[MessagePackObject]
public sealed record CapacityConsumptionFailedEvent : IErrorEvent<FinancialExecutionId>
{
    [Key(0)] public int SchemaVersion { get; init; } = 1;
    [Key(1)] public Guid Id { get; init; } = Guid.Empty;
    [Key(2)] public ActorSubject Subject { get; init; } = ActorSubject.Unknown;
    [Key(3)] public FinancialExecutionId EntityId { get; init; } = new(0, Guid.Empty);
    [Key(4)] public Guid CommandId { get; init; } = Guid.Empty;
    [Key(5)] public Guid OperationId { get; init; } = Guid.Empty;
    [Key(6)] public int PortfolioId { get; init; } = 0;
    [Key(7)] public Guid CorrelationId { get; init; } = Guid.Empty;
    [Key(8)] public Guid CausationId { get; init; } = Guid.Empty;
    [Key(9)] public DateTime FailedAtUtc { get; init; } = default;
    [Key(10)] public int ErrorCode { get; init; } = FinancialReasons.InvalidContract;
    [Key(11)] public string ReasonCode { get; init; } = string.Empty;
    [Key(12)] public string FailureClass { get; init; } = string.Empty;
    [Key(13)] public FinancialCommitDisposition CommitDisposition { get; init; } = FinancialCommitDisposition.NotCommitted;
    [Key(14)] public long ExpectedRevision { get; init; } = 0;
    [Key(15)] public long? ObservedRevision { get; init; } = null;
    [Key(16)] public string Message { get; init; } = string.Empty;
    [Key(17)] public Guid? ExistingOperationId { get; init; } = null;
    [Key(18)] public long EventId { get; init; } = 0;
    [Key(19)] public string AggregateId { get; init; } = string.Empty;
    [Key(20)] public string EventSource { get; init; } = "CapacityConsumptionFunction";
    [Key(21)] public DateTime ReceivedOn { get; init; }
    [IgnoreMember] public DateTime ErrorDate => FailedAtUtc;
    [IgnoreMember] public string ErrorMessage { get => Message; init => Message = value; }
    [Key(22)] public ErrorType ErrorType { get; init; }
    [IgnoreMember] public string ErrorData { get => ReasonCode; init => ReasonCode = value; }
    [Key(23)] public string CommandName { get; init; } = string.Empty;
    [Key(24)] public string CommandData { get; init; } = string.Empty;
    [IgnoreMember] public string UserName => "PortfolioFinancial";
    [IgnoreMember] public string EventName => nameof(CapacityConsumptionFailedEvent);
    [IgnoreMember] public EventType EventType => EventType.ErrorEvent;
}

/// <summary>Immutable committed CapacityLifecycle receipt; retries return its original identity and financial revision.</summary>
[MessagePackObject]
public sealed record CapacityLifecycleCompletedEvent : ICompleteEvent<CapacityReservationEntityId>, IFinancialCompletedEvent, IRequireDurableProjection
{
    [Key(0)] public int SchemaVersion { get; init; } = 1;
    [Key(1)] public Guid Id { get; init; } = Guid.Empty;
    [Key(2)] public ActorSubject Subject { get; init; } = ActorSubject.Unknown;
    [Key(3)] public CapacityReservationEntityId EntityId { get; init; } = new(0, Guid.Empty);
    [Key(4)] public Guid CommandId { get; init; } = Guid.Empty;
    [Key(5)] public Guid OperationId { get; init; } = Guid.Empty;
    [Key(6)] public int PortfolioId { get; init; } = 0;
    [Key(7)] public Guid CorrelationId { get; init; } = Guid.Empty;
    [Key(8)] public Guid CausationId { get; init; } = Guid.Empty;
    [Key(9)] public DateTime CommittedAtUtc { get; init; } = default;
    [Key(10)] public string InputHash { get; init; } = string.Empty;
    [Key(11)] public CapacityLifecycleReceipt Receipt { get; init; } = new();
    [Key(12)] public long EventId { get; init; } = 0;
    [Key(13)] public string AggregateId { get; init; } = string.Empty;
    [Key(14)] public string EventSource { get; init; } = "CapacityReservationCommand";
    [Key(15)] public DateTime ReceivedOn { get; init; }
    [IgnoreMember] public string UserName => "PortfolioFinancial";
    [IgnoreMember] public string EventName => nameof(CapacityLifecycleCompletedEvent);
    [IgnoreMember] public EventType EventType => EventType.CompletedEvent;
    [IgnoreMember] public DurableProjectionRequirement RequiredProjection => new("CapacityReservationCommandActor", "CapacityLifecycleProjector", EventProjectorStageType.ApplyProjection);
}

/// <summary>Classified CapacityLifecycle refusal/uncertain outcome; never evidence of a successful financial mutation.</summary>
[MessagePackObject]
public sealed record CapacityLifecycleFailedEvent : IErrorEvent<CapacityReservationEntityId>
{
    [Key(0)] public int SchemaVersion { get; init; } = 1;
    [Key(1)] public Guid Id { get; init; } = Guid.Empty;
    [Key(2)] public ActorSubject Subject { get; init; } = ActorSubject.Unknown;
    [Key(3)] public CapacityReservationEntityId EntityId { get; init; } = new(0, Guid.Empty);
    [Key(4)] public Guid CommandId { get; init; } = Guid.Empty;
    [Key(5)] public Guid OperationId { get; init; } = Guid.Empty;
    [Key(6)] public int PortfolioId { get; init; } = 0;
    [Key(7)] public Guid CorrelationId { get; init; } = Guid.Empty;
    [Key(8)] public Guid CausationId { get; init; } = Guid.Empty;
    [Key(9)] public DateTime FailedAtUtc { get; init; } = default;
    [Key(10)] public int ErrorCode { get; init; } = FinancialReasons.InvalidContract;
    [Key(11)] public string ReasonCode { get; init; } = string.Empty;
    [Key(12)] public string FailureClass { get; init; } = string.Empty;
    [Key(13)] public FinancialCommitDisposition CommitDisposition { get; init; } = FinancialCommitDisposition.NotCommitted;
    [Key(14)] public long ExpectedRevision { get; init; } = 0;
    [Key(15)] public long? ObservedRevision { get; init; } = null;
    [Key(16)] public string Message { get; init; } = string.Empty;
    [Key(17)] public Guid? ExistingOperationId { get; init; } = null;
    [Key(18)] public long EventId { get; init; } = 0;
    [Key(19)] public string AggregateId { get; init; } = string.Empty;
    [Key(20)] public string EventSource { get; init; } = "CapacityReservationCommand";
    [Key(21)] public DateTime ReceivedOn { get; init; }
    [IgnoreMember] public DateTime ErrorDate => FailedAtUtc;
    [IgnoreMember] public string ErrorMessage { get => Message; init => Message = value; }
    [Key(22)] public ErrorType ErrorType { get; init; }
    [IgnoreMember] public string ErrorData { get => ReasonCode; init => ReasonCode = value; }
    [Key(23)] public string CommandName { get; init; } = string.Empty;
    [Key(24)] public string CommandData { get; init; } = string.Empty;
    [IgnoreMember] public string UserName => "PortfolioFinancial";
    [IgnoreMember] public string EventName => nameof(CapacityLifecycleFailedEvent);
    [IgnoreMember] public EventType EventType => EventType.ErrorEvent;
}
