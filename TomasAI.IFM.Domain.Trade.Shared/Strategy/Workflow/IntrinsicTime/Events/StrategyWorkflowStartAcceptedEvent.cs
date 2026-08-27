using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;

/// <summary>Records acceptance of a new Intrinsic Time Strategy workflow execution.</summary>
/// <remarks>This event is persisted in the workflow Command event log and is not routed to a durable Event actor.</remarks>
[MessagePackObject(AllowPrivate = true)]
public sealed record StrategyWorkflowStartAcceptedEvent : IEvent<IntrinsicTimeStrategyWorkflowEntityId>
{
    /// <summary>Logical workflow event source name; no Event actor is registered for it.</summary>
    [IgnoreMember] public const string Actor = "IntrinsicTimeStrategyWorkflow";
    /// <summary>Stable event verb.</summary>
    [IgnoreMember] public const string Verb = "StartAccepted";
    /// <summary>Stable event error identifier.</summary>
    [IgnoreMember] public const int ErrorId = 22001;

    /// <summary>Gets the persisted event subject.</summary>
    [Key(0)] public ActorSubject Subject { get; init; }
    /// <summary>Gets the event instance identity.</summary>
    [Key(1)] public Guid Id { get; init; }
    /// <summary>Gets the workflow routing entity identity.</summary>
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
    /// <summary>Gets the accepted workflow execution identity.</summary>
    [Key(8)] public StrategyWorkflowId WorkflowId { get; init; }
    /// <summary>Gets the resulting workflow revision.</summary>
    [Key(9)] public long WorkflowRevision { get; init; }
    /// <summary>Gets the workflow correlation identity.</summary>
    [Key(10)] public Guid CorrelationId { get; init; }
    /// <summary>Gets the causative trigger identity.</summary>
    [Key(11)] public Guid CausationId { get; init; }
    /// <summary>Gets the first workflow stage.</summary>
    [Key(12)] public StrategyWorkflowStage Stage { get; init; }
    /// <summary>Gets the source ITI event identity.</summary>
    [Key(13)] public Guid TriggerEventId { get; init; }
    /// <summary>Gets the original ITI signal event retained for command-state replay.</summary>
    [Key(14)] public FuturesItiSignalGeneratedEvent TriggerEvent { get; init; } = new();
    /// <summary>Gets the workflow definition version.</summary>
    [Key(15)] public int WorkflowDefinitionVersion { get; init; }
    /// <summary>Gets the UTC workflow start timestamp.</summary>
    [Key(16)] public DateTime StartedAtUtc { get; init; }
    /// <summary>Gets the immutable Regime Discovery parameters selected at workflow acceptance.</summary>
    [Key(17)] public RegimeDiscoveryParameterSet RegimeDiscoveryParameterSet { get; init; } = new();
    /// <summary>Gets the canonical selected parameter payload hash.</summary>
    [Key(18)] public string RegimeDiscoveryParameterPayloadSha256 { get; init; } = string.Empty;

    /// <summary>Gets the local event-source user for diagnostics.</summary>
    [IgnoreMember] public string UserName => $"{Environment.UserDomainName}\\{Environment.UserName}";
    /// <summary>Gets the concrete event contract name.</summary>
    [IgnoreMember] public string EventName => nameof(StrategyWorkflowStartAcceptedEvent);
    /// <summary>Gets the domain-event classification.</summary>
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;

    /// <summary>Initializes an empty event for serialization.</summary>
    public StrategyWorkflowStartAcceptedEvent() { }

    /// <summary>Initializes the complete keyed MessagePack event contract.</summary>
    /// <param name="subject">Persisted event subject.</param>
    /// <param name="id">Event instance identity.</param>
    /// <param name="entityId">Workflow routing identity.</param>
    /// <param name="eventId">Event-stream sequence identity.</param>
    /// <param name="commandId">Source command identity.</param>
    /// <param name="aggregateId">Workflow aggregate identity.</param>
    /// <param name="eventSource">Command event source.</param>
    /// <param name="receivedOn">UTC event receipt timestamp.</param>
    /// <param name="workflowId">Accepted workflow execution identity.</param>
    /// <param name="workflowRevision">Resulting workflow revision.</param>
    /// <param name="correlationId">Workflow correlation identity.</param>
    /// <param name="causationId">Causative trigger identity.</param>
    /// <param name="stage">First workflow stage.</param>
    /// <param name="triggerEventId">Source ITI event identity.</param>
    /// <param name="triggerEvent">Original ITI signal event retained for command-state replay.</param>
    /// <param name="workflowDefinitionVersion">Workflow definition version.</param>
    /// <param name="startedAtUtc">UTC workflow start timestamp.</param>
    [SerializationConstructor]
    public StrategyWorkflowStartAcceptedEvent(
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
        StrategyWorkflowStage stage,
        Guid triggerEventId,
        FuturesItiSignalGeneratedEvent triggerEvent,
        int workflowDefinitionVersion,
        DateTime startedAtUtc,
        RegimeDiscoveryParameterSet regimeDiscoveryParameterSet,
        string regimeDiscoveryParameterPayloadSha256)
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
        Stage = stage;
        TriggerEventId = triggerEventId;
        TriggerEvent = triggerEvent;
        WorkflowDefinitionVersion = workflowDefinitionVersion;
        StartedAtUtc = startedAtUtc;
        RegimeDiscoveryParameterSet = regimeDiscoveryParameterSet;
        RegimeDiscoveryParameterPayloadSha256 = regimeDiscoveryParameterPayloadSha256 ?? string.Empty;
    }
}
