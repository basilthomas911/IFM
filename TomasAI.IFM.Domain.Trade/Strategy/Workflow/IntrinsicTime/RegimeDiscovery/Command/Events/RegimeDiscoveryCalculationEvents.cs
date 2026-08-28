using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Command.Events;

/// <summary>Records the private durable success of one Regime Discovery calculation.</summary>
[MessagePackObject]
public sealed record RegimeDiscoveryCalculationCompletedEvent : IEvent<IntrinsicTimeStrategyWorkflowEntityId>
{
    /// <summary>Gets the private event family name.</summary>
    public const string Actor = "RegimeDiscoveryPipelineCommand";
    /// <summary>Gets the private completion verb.</summary>
    public const string Verb = "CalculationCompleted";
    /// <summary>Gets the stable error code used when this transition is rejected.</summary>
    public const int ErrorCode = 23101;
    /// <inheritdoc />
    [Key(0)] public ActorSubject Subject { get; init; }
    /// <inheritdoc />
    [Key(1)] public Guid Id { get; init; }
    /// <inheritdoc />
    [Key(2)] public IntrinsicTimeStrategyWorkflowEntityId EntityId { get; init; }
    /// <inheritdoc />
    [Key(3)] public long EventId { get; init; }
    /// <inheritdoc />
    [Key(4)] public Guid CommandId { get; init; }
    /// <inheritdoc />
    [Key(5)] public string AggregateId { get; init; } = string.Empty;
    /// <inheritdoc />
    [Key(6)] public string EventSource { get; init; } = string.Empty;
    /// <inheritdoc />
    [Key(7)] public DateTime ReceivedOn { get; init; }
    /// <summary>Gets the workflow execution identity.</summary>
    [Key(8)] public StrategyWorkflowId WorkflowId { get; init; }
    /// <summary>Gets the immutable workflow revision supplied to the pipeline.</summary>
    [Key(9)] public long InputWorkflowRevision { get; init; }
    /// <summary>Gets the exact parameter payload hash.</summary>
    [Key(10)] public string ParameterPayloadSha256 { get; init; } = string.Empty;
    /// <summary>Gets the frozen signal-snapshot identity.</summary>
    [Key(11)] public Guid SignalSnapshotId { get; init; }
    /// <summary>Gets the captured cache revision.</summary>
    [Key(12)] public long SignalSnapshotRevision { get; init; }
    /// <summary>Gets the complete typed calculation result.</summary>
    [Key(13)] public RegimeDiscoveryResult Result { get; init; } = new();
    /// <summary>Gets the SHA-256 hash of the MessagePack result payload.</summary>
    [Key(14)] public string ResultPayloadSha256 { get; init; } = string.Empty;
    /// <summary>Gets the UTC terminal timestamp.</summary>
    [Key(15)] public DateTime CompletedAtUtc { get; init; }
    /// <summary>Gets the workflow correlation identity.</summary>
    [Key(16)] public Guid CorrelationId { get; init; }
    /// <summary>Gets the causative workflow lifecycle event identity.</summary>
    [Key(17)] public Guid CausationId { get; init; }
    /// <summary>Gets the fixed workflow execution deadline.</summary>
    [Key(18)] public DateTime ExpiresAtUtc { get; init; }
    /// <inheritdoc />
    [IgnoreMember] public string UserName => string.Empty;
    /// <inheritdoc />
    [IgnoreMember] public string EventName => nameof(RegimeDiscoveryCalculationCompletedEvent);
    /// <inheritdoc />
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;
}

/// <summary>Records the private durable failure of one accepted Regime Discovery calculation.</summary>
[MessagePackObject]
public sealed record RegimeDiscoveryCalculationFailedEvent : IEvent<IntrinsicTimeStrategyWorkflowEntityId>
{
    /// <summary>Gets the private event family name.</summary>
    public const string Actor = "RegimeDiscoveryPipelineCommand";
    /// <summary>Gets the private failure verb.</summary>
    public const string Verb = "CalculationFailed";
    /// <summary>Gets the stable error code used for durable calculation failure.</summary>
    public const int ErrorCode = 23102;
    /// <summary>Gets the stable failure code used when the fixed workflow deadline wins.</summary>
    public const int TimeoutErrorCode = 23103;
    /// <inheritdoc />
    [Key(0)] public ActorSubject Subject { get; init; }
    /// <inheritdoc />
    [Key(1)] public Guid Id { get; init; }
    /// <inheritdoc />
    [Key(2)] public IntrinsicTimeStrategyWorkflowEntityId EntityId { get; init; }
    /// <inheritdoc />
    [Key(3)] public long EventId { get; init; }
    /// <inheritdoc />
    [Key(4)] public Guid CommandId { get; init; }
    /// <inheritdoc />
    [Key(5)] public string AggregateId { get; init; } = string.Empty;
    /// <inheritdoc />
    [Key(6)] public string EventSource { get; init; } = string.Empty;
    /// <inheritdoc />
    [Key(7)] public DateTime ReceivedOn { get; init; }
    /// <summary>Gets the workflow execution identity.</summary>
    [Key(8)] public StrategyWorkflowId WorkflowId { get; init; }
    /// <summary>Gets the immutable workflow revision supplied to the pipeline.</summary>
    [Key(9)] public long InputWorkflowRevision { get; init; }
    /// <summary>Gets the exact parameter payload hash.</summary>
    [Key(10)] public string ParameterPayloadSha256 { get; init; } = string.Empty;
    /// <summary>Gets the snapshot identity when capture produced one.</summary>
    [Key(11)] public Guid SignalSnapshotId { get; init; }
    /// <summary>Gets the standard pipeline failure.</summary>
    [Key(12)] public StrategyPipelineFailure Failure { get; init; } = new();
    /// <summary>Gets stable failure reasons in deterministic order.</summary>
    [Key(13)] public RegimeDiscoveryReason[] Reasons { get; init; } = [];
    /// <summary>Gets the UTC terminal timestamp.</summary>
    [Key(14)] public DateTime FailedAtUtc { get; init; }
    /// <summary>Gets the workflow correlation identity.</summary>
    [Key(15)] public Guid CorrelationId { get; init; }
    /// <summary>Gets the causative workflow lifecycle event identity.</summary>
    [Key(16)] public Guid CausationId { get; init; }
    /// <summary>Gets the fixed workflow execution deadline.</summary>
    [Key(17)] public DateTime ExpiresAtUtc { get; init; }
    /// <inheritdoc />
    [IgnoreMember] public string UserName => string.Empty;
    /// <inheritdoc />
    [IgnoreMember] public string EventName => nameof(RegimeDiscoveryCalculationFailedEvent);
    /// <inheritdoc />
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;
}
