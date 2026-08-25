using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;

/// <summary>Records a terminal non-successful Intrinsic Time Strategy workflow outcome.</summary>
/// <remarks>This event is persisted in the workflow Command event log and is not routed to a durable Event actor.</remarks>
[MessagePackObject(AllowPrivate = true)]
public sealed record IntrinsicTimeStrategyWorkflowStoppedEvent : IEvent<IntrinsicTimeStrategyWorkflowEntityId>
{
    /// <summary>Logical workflow event source name; no Event actor is registered for it.</summary>
    [IgnoreMember] public const string Actor = "IntrinsicTimeStrategyWorkflow";
    /// <summary>Stable event verb.</summary>
    [IgnoreMember] public const string Verb = "Stopped";
    /// <summary>Stable event error identifier.</summary>
    [IgnoreMember] public const int ErrorId = 22029;

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
    /// <summary>Gets the workflow execution identity.</summary>
    [Key(8)] public StrategyWorkflowId WorkflowId { get; init; }
    /// <summary>Gets the resulting workflow revision.</summary>
    [Key(9)] public long WorkflowRevision { get; init; }
    /// <summary>Gets the workflow correlation identity.</summary>
    [Key(10)] public Guid CorrelationId { get; init; }
    /// <summary>Gets the causative command or event identity.</summary>
    [Key(11)] public Guid CausationId { get; init; }
    /// <summary>Gets the terminal workflow stage.</summary>
    [Key(12)] public StrategyWorkflowStage Stage { get; init; }
    /// <summary>Gets the terminal workflow outcome.</summary>
    [Key(13)] public StrategyWorkflowOutcome Outcome { get; init; }
    /// <summary>Gets the stable stop reason code.</summary>
    [Key(14)] public string ReasonCode { get; init; } = string.Empty;
    /// <summary>Gets the UTC workflow stop timestamp.</summary>
    [Key(15)] public DateTime StoppedAtUtc { get; init; }

    /// <summary>Gets the local event-source user for diagnostics.</summary>
    [IgnoreMember] public string UserName => $"{Environment.UserDomainName}\\{Environment.UserName}";
    /// <summary>Gets the concrete event contract name.</summary>
    [IgnoreMember] public string EventName => nameof(IntrinsicTimeStrategyWorkflowStoppedEvent);
    /// <summary>Gets the domain-event classification.</summary>
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;

    /// <summary>Initializes an empty event for serialization.</summary>
    public IntrinsicTimeStrategyWorkflowStoppedEvent() { }

    /// <summary>Initializes the complete keyed MessagePack event contract.</summary>
    /// <param name="subject">Persisted event subject.</param>
    /// <param name="id">Event instance identity.</param>
    /// <param name="entityId">Workflow routing identity.</param>
    /// <param name="eventId">Event-stream sequence identity.</param>
    /// <param name="commandId">Source command identity.</param>
    /// <param name="aggregateId">Workflow aggregate identity.</param>
    /// <param name="eventSource">Command event source.</param>
    /// <param name="receivedOn">UTC event receipt timestamp.</param>
    /// <param name="workflowId">Workflow execution identity.</param>
    /// <param name="workflowRevision">Resulting workflow revision.</param>
    /// <param name="correlationId">Workflow correlation identity.</param>
    /// <param name="causationId">Causative command or event identity.</param>
    /// <param name="stage">Terminal workflow stage.</param>
    /// <param name="outcome">Terminal workflow outcome.</param>
    /// <param name="reasonCode">Stable stop reason code.</param>
    /// <param name="stoppedAtUtc">UTC workflow stop timestamp.</param>
    [SerializationConstructor]
    public IntrinsicTimeStrategyWorkflowStoppedEvent(
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
        StrategyWorkflowOutcome outcome,
        string reasonCode,
        DateTime stoppedAtUtc)
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
        Outcome = outcome;
        ReasonCode = reasonCode ?? string.Empty;
        StoppedAtUtc = stoppedAtUtc;
    }
}
