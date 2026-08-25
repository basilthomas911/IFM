using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;

/// <summary>Records rejection of a workflow start while another execution is active.</summary>
/// <remarks>This event is persisted in the workflow Command event log and is not routed to a durable Event actor.</remarks>
[MessagePackObject(AllowPrivate = true)]
public sealed record StrategyWorkflowStartRejectedEvent : IEvent<IntrinsicTimeStrategyWorkflowEntityId>
{
    /// <summary>Logical workflow event source name; no Event actor is registered for it.</summary>
    [IgnoreMember] public const string Actor = "IntrinsicTimeStrategyWorkflow";
    /// <summary>Stable event verb.</summary>
    [IgnoreMember] public const string Verb = "StartRejected";
    /// <summary>Stable event error identifier.</summary>
    [IgnoreMember] public const int ErrorId = 22002;

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
    /// <summary>Gets the rejected proposed workflow identity.</summary>
    [Key(8)] public StrategyWorkflowId RequestedWorkflowId { get; init; }
    /// <summary>Gets the active workflow execution identity.</summary>
    [Key(9)] public StrategyWorkflowId ActiveWorkflowId { get; init; }
    /// <summary>Gets the active workflow revision.</summary>
    [Key(10)] public long ActiveWorkflowRevision { get; init; }
    /// <summary>Gets the request correlation identity.</summary>
    [Key(11)] public Guid CorrelationId { get; init; }
    /// <summary>Gets the causative trigger identity.</summary>
    [Key(12)] public Guid CausationId { get; init; }
    /// <summary>Gets the active workflow stage.</summary>
    [Key(13)] public StrategyWorkflowStage ActiveStage { get; init; }
    /// <summary>Gets the rejected ITI trigger identity.</summary>
    [Key(14)] public Guid TriggerEventId { get; init; }
    /// <summary>Gets the stable rejection reason code.</summary>
    [Key(15)] public string ReasonCode { get; init; } = string.Empty;
    /// <summary>Gets the UTC rejection timestamp.</summary>
    [Key(16)] public DateTime RejectedAtUtc { get; init; }

    /// <summary>Gets the local event-source user for diagnostics.</summary>
    [IgnoreMember] public string UserName => $"{Environment.UserDomainName}\\{Environment.UserName}";
    /// <summary>Gets the concrete event contract name.</summary>
    [IgnoreMember] public string EventName => nameof(StrategyWorkflowStartRejectedEvent);
    /// <summary>Gets the domain-event classification.</summary>
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;

    /// <summary>Initializes an empty event for serialization.</summary>
    public StrategyWorkflowStartRejectedEvent() { }

    /// <summary>Initializes the complete keyed MessagePack event contract.</summary>
    /// <param name="subject">Persisted event subject.</param>
    /// <param name="id">Event instance identity.</param>
    /// <param name="entityId">Workflow routing identity.</param>
    /// <param name="eventId">Event-stream sequence identity.</param>
    /// <param name="commandId">Source command identity.</param>
    /// <param name="aggregateId">Workflow aggregate identity.</param>
    /// <param name="eventSource">Command event source.</param>
    /// <param name="receivedOn">UTC event receipt timestamp.</param>
    /// <param name="requestedWorkflowId">Rejected proposed workflow identity.</param>
    /// <param name="activeWorkflowId">Active workflow execution identity.</param>
    /// <param name="activeWorkflowRevision">Active workflow revision.</param>
    /// <param name="correlationId">Request correlation identity.</param>
    /// <param name="causationId">Causative trigger identity.</param>
    /// <param name="activeStage">Active workflow stage.</param>
    /// <param name="triggerEventId">Rejected ITI trigger identity.</param>
    /// <param name="reasonCode">Stable rejection reason code.</param>
    /// <param name="rejectedAtUtc">UTC rejection timestamp.</param>
    [SerializationConstructor]
    public StrategyWorkflowStartRejectedEvent(
        ActorSubject subject,
        Guid id,
        IntrinsicTimeStrategyWorkflowEntityId entityId,
        long eventId,
        Guid commandId,
        string aggregateId,
        string eventSource,
        DateTime receivedOn,
        StrategyWorkflowId requestedWorkflowId,
        StrategyWorkflowId activeWorkflowId,
        long activeWorkflowRevision,
        Guid correlationId,
        Guid causationId,
        StrategyWorkflowStage activeStage,
        Guid triggerEventId,
        string reasonCode,
        DateTime rejectedAtUtc)
    {
        Subject = subject;
        Id = id;
        EntityId = entityId;
        EventId = eventId;
        CommandId = commandId;
        AggregateId = aggregateId ?? string.Empty;
        EventSource = eventSource ?? string.Empty;
        ReceivedOn = receivedOn;
        RequestedWorkflowId = requestedWorkflowId;
        ActiveWorkflowId = activeWorkflowId;
        ActiveWorkflowRevision = activeWorkflowRevision;
        CorrelationId = correlationId;
        CausationId = causationId;
        ActiveStage = activeStage;
        TriggerEventId = triggerEventId;
        ReasonCode = reasonCode ?? string.Empty;
        RejectedAtUtc = rejectedAtUtc;
    }
}
