using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;

/// <summary>Notifies read-only observers after an authoritative Strategy Workflow snapshot is projected.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record IntrinsicTimeStrategyWorkflowUpdatedNotifyEvent
    : IEvent<IntrinsicTimeStrategyWorkflowEntityId>
{
    [IgnoreMember] public const string Actor = "IntrinsicTimeStrategyWorkflowNotification";
    [IgnoreMember] public const string Verb = "Updated";

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public Guid Id { get; init; }
    [Key(2)] public IntrinsicTimeStrategyWorkflowEntityId EntityId { get; init; } = new();
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; } = string.Empty;
    [Key(6)] public string EventSource { get; init; } = string.Empty;
    [Key(7)] public DateTime ReceivedOn { get; init; }
    [Key(8)] public StrategyWorkflowId WorkflowId { get; init; }
    [Key(9)] public long WorkflowRevision { get; init; }
    [Key(10)] public Guid SourceEventId { get; init; }
    [Key(11)] public IntrinsicTimeStrategyWorkflowView State { get; init; } = new();
    [Key(12)] public DateTime UpdatedAtUtc { get; init; }

    [IgnoreMember] public string UserName => string.Empty;
    [IgnoreMember] public string EventName => nameof(IntrinsicTimeStrategyWorkflowUpdatedNotifyEvent);
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;

    public IntrinsicTimeStrategyWorkflowUpdatedNotifyEvent() { }

    [SerializationConstructor]
    public IntrinsicTimeStrategyWorkflowUpdatedNotifyEvent(
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
        Guid sourceEventId,
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
        SourceEventId = sourceEventId;
        State = state ?? new IntrinsicTimeStrategyWorkflowView();
        UpdatedAtUtc = updatedAtUtc;
    }
}
