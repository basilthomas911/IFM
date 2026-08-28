using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;

/// <summary>Records one authoritative complete Strategy Workflow state snapshot.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record WorkflowStrategyStateUpdatedEvent : IEvent<IntrinsicTimeStrategyWorkflowEntityId>
{
    /// <summary>Logical Strategy Workflow event source.</summary>
    [IgnoreMember] public const string Actor = "IntrinsicTimeStrategyWorkflow";
    /// <summary>Stable snapshot notification verb.</summary>
    [IgnoreMember] public const string Verb = "StateUpdated";
    /// <summary>Stable event error identifier.</summary>
    [IgnoreMember] public const int ErrorId = 22032;

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public Guid Id { get; init; }
    [Key(2)] public IntrinsicTimeStrategyWorkflowEntityId EntityId { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; } = string.Empty;
    [Key(6)] public string EventSource { get; init; } = string.Empty;
    [Key(7)] public DateTime ReceivedOn { get; init; }
    [Key(8)] public StrategyWorkflowId WorkflowId { get; init; }
    [Key(9)] public long WorkflowRevision { get; init; }
    [Key(10)] public Guid CorrelationId { get; init; }
    [Key(11)] public Guid CausationId { get; init; }
    [Key(12)] public WorkflowStrategyMachineStatus PreviousStatus { get; init; }
    [Key(13)] public IntrinsicTimeStrategyWorkflowView State { get; init; } = new();
    [Key(14)] public DateTime UpdatedAtUtc { get; init; }

    [IgnoreMember] public string UserName => $"{Environment.UserDomainName}\\{Environment.UserName}";
    [IgnoreMember] public string EventName => nameof(WorkflowStrategyStateUpdatedEvent);
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;

    /// <summary>Initializes an empty event for serialization.</summary>
    public WorkflowStrategyStateUpdatedEvent() { }

    /// <summary>Initializes the complete keyed snapshot event.</summary>
    [SerializationConstructor]
    public WorkflowStrategyStateUpdatedEvent(
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
        WorkflowStrategyMachineStatus previousStatus,
        IntrinsicTimeStrategyWorkflowView state,
        DateTime updatedAtUtc)
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
        PreviousStatus = previousStatus;
        State = state ?? new IntrinsicTimeStrategyWorkflowView();
        UpdatedAtUtc = updatedAtUtc;
    }
}
