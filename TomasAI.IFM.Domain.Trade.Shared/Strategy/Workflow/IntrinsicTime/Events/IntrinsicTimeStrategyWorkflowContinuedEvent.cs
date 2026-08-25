using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;

/// <summary>Records a committed workflow continuation and the next deterministic pipeline dispatch.</summary>
/// <remarks>
/// The conventional EventProjector publishes this event realtime only after the authoritative workflow transition is
/// committed and the ScyllaDB read model is updated. No durable Event actor consumes it.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public sealed record IntrinsicTimeStrategyWorkflowContinuedEvent : IEvent<IntrinsicTimeStrategyWorkflowEntityId>
{
    /// <summary>Logical workflow event source name.</summary>
    [IgnoreMember] public const string Actor = "IntrinsicTimeStrategyWorkflow";
    /// <summary>Stable workflow lifecycle verb.</summary>
    [IgnoreMember] public const string Verb = "Continued";
    /// <summary>Stable event error identifier.</summary>
    [IgnoreMember] public const int ErrorId = 22031;

    [IgnoreMember]
    string[] _continuationReasonCodes = [];

    /// <summary>Gets the persisted event subject.</summary>
    [Key(0)] public ActorSubject Subject { get; init; }
    /// <summary>Gets the event identity.</summary>
    [Key(1)] public Guid Id { get; init; }
    /// <summary>Gets the workflow routing identity.</summary>
    [Key(2)] public IntrinsicTimeStrategyWorkflowEntityId EntityId { get; init; }
    /// <summary>Gets the event-stream sequence identity.</summary>
    [Key(3)] public long EventId { get; init; }
    /// <summary>Gets the source command identity.</summary>
    [Key(4)] public Guid CommandId { get; init; }
    /// <summary>Gets the workflow aggregate identity.</summary>
    [Key(5)] public string AggregateId { get; init; } = string.Empty;
    /// <summary>Gets the workflow Command event source.</summary>
    [Key(6)] public string EventSource { get; init; } = string.Empty;
    /// <summary>Gets the UTC event receipt timestamp.</summary>
    [Key(7)] public DateTime ReceivedOn { get; init; }
    /// <summary>Gets the workflow execution identity.</summary>
    [Key(8)] public StrategyWorkflowId WorkflowId { get; init; }
    /// <summary>Gets the resulting workflow revision.</summary>
    [Key(9)] public long WorkflowRevision { get; init; }
    /// <summary>Gets the workflow correlation identity.</summary>
    [Key(10)] public Guid CorrelationId { get; init; }
    /// <summary>Gets the causative pipeline result identity.</summary>
    [Key(11)] public Guid CausationId { get; init; }
    /// <summary>Gets the pipeline stage whose result was accepted.</summary>
    [Key(12)] public StrategyWorkflowStage CompletedPipelineStage { get; init; }
    /// <summary>Gets the next pipeline stage.</summary>
    [Key(13)] public StrategyWorkflowStage NextPipelineStage { get; init; }
    /// <summary>Gets the target pipeline actor type.</summary>
    [Key(14)] public ActorType NextPipelineActorType { get; init; }
    /// <summary>Gets the target pipeline actor name.</summary>
    [Key(15)] public string NextPipelineActorName { get; init; } = string.Empty;
    /// <summary>Gets the target pipeline bounded context.</summary>
    [Key(16)] public BoundedContextName NextPipelineBoundedContext { get; init; }
    /// <summary>Gets the deterministic pipeline command identity.</summary>
    [Key(17)] public Guid NextPipelineCommandId { get; init; }
    /// <summary>Gets the immutable next-pipeline workflow snapshot.</summary>
    [Key(18)] public IntrinsicTimeStrategyWorkflowState WorkflowState { get; init; } = new();
    /// <summary>Gets the original ITI signal event.</summary>
    [Key(19)] public FuturesItiSignalGeneratedEvent TriggerEvent { get; init; } = new();
    /// <summary>Gets the continuation rule-set identity.</summary>
    [Key(20)] public string ContinuationRuleSetId { get; init; } = string.Empty;
    /// <summary>Gets the continuation rule-set version.</summary>
    [Key(21)] public int ContinuationRuleSetVersion { get; init; }
    /// <summary>Gets a defensive copy of the continuation reason codes.</summary>
    [Key(22)]
    public string[] ContinuationReasonCodes
    {
        get => [.. _continuationReasonCodes];
        init => _continuationReasonCodes = value is null ? [] : [.. value];
    }
    /// <summary>Gets the UTC next-pipeline request timestamp.</summary>
    [Key(23)] public DateTime RequestedAtUtc { get; init; }
    /// <summary>Gets the optional UTC next-pipeline completion deadline.</summary>
    [Key(24)] public DateTime? ExpectedCompletionAtUtc { get; init; }
    /// <summary>Gets the UTC workflow continuation timestamp.</summary>
    [Key(25)] public DateTime ContinuedAtUtc { get; init; }

    /// <summary>Gets the local event-source user for diagnostics.</summary>
    [IgnoreMember] public string UserName => $"{Environment.UserDomainName}\\{Environment.UserName}";
    /// <summary>Gets the concrete event contract name.</summary>
    [IgnoreMember] public string EventName => nameof(IntrinsicTimeStrategyWorkflowContinuedEvent);
    /// <summary>Gets the domain-event classification.</summary>
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;

    /// <summary>Initializes an empty event for serialization.</summary>
    public IntrinsicTimeStrategyWorkflowContinuedEvent() { }

    /// <summary>Initializes the complete keyed MessagePack event contract.</summary>
    /// <param name="subject">Persisted event subject.</param>
    /// <param name="id">Event identity.</param>
    /// <param name="entityId">Workflow routing identity.</param>
    /// <param name="eventId">Event-stream sequence identity.</param>
    /// <param name="commandId">Source command identity.</param>
    /// <param name="aggregateId">Workflow aggregate identity.</param>
    /// <param name="eventSource">Workflow Command event source.</param>
    /// <param name="receivedOn">UTC event receipt timestamp.</param>
    /// <param name="workflowId">Workflow execution identity.</param>
    /// <param name="workflowRevision">Resulting workflow revision.</param>
    /// <param name="correlationId">Workflow correlation identity.</param>
    /// <param name="causationId">Causative pipeline result identity.</param>
    /// <param name="completedPipelineStage">Pipeline stage whose result was accepted.</param>
    /// <param name="nextPipelineStage">Next pipeline stage.</param>
    /// <param name="nextPipelineActorType">Target pipeline actor type.</param>
    /// <param name="nextPipelineActorName">Target pipeline actor name.</param>
    /// <param name="nextPipelineBoundedContext">Target pipeline bounded context.</param>
    /// <param name="nextPipelineCommandId">Deterministic pipeline command identity.</param>
    /// <param name="workflowState">Immutable next-pipeline workflow snapshot.</param>
    /// <param name="triggerEvent">Original ITI signal event.</param>
    /// <param name="continuationRuleSetId">Continuation rule-set identity.</param>
    /// <param name="continuationRuleSetVersion">Continuation rule-set version.</param>
    /// <param name="continuationReasonCodes">Continuation reason codes.</param>
    /// <param name="requestedAtUtc">UTC next-pipeline request timestamp.</param>
    /// <param name="expectedCompletionAtUtc">Optional UTC next-pipeline completion deadline.</param>
    /// <param name="continuedAtUtc">UTC workflow continuation timestamp.</param>
    [SerializationConstructor]
    public IntrinsicTimeStrategyWorkflowContinuedEvent(
        ActorSubject subject,
        Guid id,
        IntrinsicTimeStrategyWorkflowEntityId entityId,
        long eventId,
        Guid commandId,
        string aggregateId,
        string eventSource,
        DateTime receivedOn,
        StrategyWorkflowId workflowId,
        long workflowRevision,
        Guid correlationId,
        Guid causationId,
        StrategyWorkflowStage completedPipelineStage,
        StrategyWorkflowStage nextPipelineStage,
        ActorType nextPipelineActorType,
        string nextPipelineActorName,
        BoundedContextName nextPipelineBoundedContext,
        Guid nextPipelineCommandId,
        IntrinsicTimeStrategyWorkflowState workflowState,
        FuturesItiSignalGeneratedEvent triggerEvent,
        string continuationRuleSetId,
        int continuationRuleSetVersion,
        string[] continuationReasonCodes,
        DateTime requestedAtUtc,
        DateTime? expectedCompletionAtUtc,
        DateTime continuedAtUtc)
    {
        Subject = subject;
        Id = id;
        EntityId = entityId;
        EventId = eventId;
        CommandId = commandId;
        AggregateId = aggregateId ?? string.Empty;
        EventSource = eventSource ?? string.Empty;
        ReceivedOn = receivedOn;
        WorkflowId = workflowId;
        WorkflowRevision = workflowRevision;
        CorrelationId = correlationId;
        CausationId = causationId;
        CompletedPipelineStage = completedPipelineStage;
        NextPipelineStage = nextPipelineStage;
        NextPipelineActorType = nextPipelineActorType;
        NextPipelineActorName = nextPipelineActorName ?? string.Empty;
        NextPipelineBoundedContext = nextPipelineBoundedContext;
        NextPipelineCommandId = nextPipelineCommandId;
        WorkflowState = workflowState;
        TriggerEvent = triggerEvent;
        ContinuationRuleSetId = continuationRuleSetId ?? string.Empty;
        ContinuationRuleSetVersion = continuationRuleSetVersion;
        ContinuationReasonCodes = continuationReasonCodes;
        RequestedAtUtc = requestedAtUtc;
        ExpectedCompletionAtUtc = expectedCompletionAtUtc;
        ContinuedAtUtc = continuedAtUtc;
    }
}
