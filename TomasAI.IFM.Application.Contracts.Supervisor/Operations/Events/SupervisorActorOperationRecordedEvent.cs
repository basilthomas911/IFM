using MessagePack;
using TomasAI.IFM.Domain.Supervisor.Shared.Enums;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Supervisor.Shared.Operations.Events;

/// <summary>Private source event committed after a Supervisor lifecycle command reaches a terminal outcome.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record SupervisorActorOperationRecordedEvent : IEvent<ActorEntityId>
{
    public const string Actor = "SupervisorEvent";
    public const string Verb = "ActorOperationRecorded";
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public Guid Id { get; init; }
    [Key(2)] public ActorEntityId EntityId { get; init; } = ActorEntityId.Default;
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; } = string.Empty;
    [Key(6)] public string EventSource { get; init; } = "SupervisorCommandActor";
    [Key(7)] public DateTime ReceivedOn { get; init; }
    [Key(8)] public ActorThreadId Target { get; init; }
    [Key(9)] public long ExpectedGeneration { get; init; }
    [Key(10)] public SupervisorActorOperationKind Operation { get; init; }
    [Key(11)] public string Requester { get; init; } = string.Empty;
    [Key(12)] public string Reason { get; init; } = string.Empty;
    [Key(13)] public long TimeoutTicks { get; init; }
    [Key(14)] public SupervisorOperationOutcome Outcome { get; init; }
    [Key(15)] public string Stage { get; init; } = string.Empty;
    [Key(16)] public string FailureReason { get; init; } = string.Empty;
    [IgnoreMember] public string UserName => Requester;
    [IgnoreMember] public string EventName => nameof(SupervisorActorOperationRecordedEvent);
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;

    /// <summary>Creates the event for serialization and object-initializer callers.</summary>
    public SupervisorActorOperationRecordedEvent() { }

    /// <summary>Reconstructs every permanent MessagePack field in numeric key order.</summary>
    /// <param name="subject">The serialized Subject value.</param>
    /// <param name="id">The serialized Id value.</param>
    /// <param name="entityId">The serialized EntityId value.</param>
    /// <param name="eventId">The serialized EventId value.</param>
    /// <param name="commandId">The serialized CommandId value.</param>
    /// <param name="aggregateId">The serialized AggregateId value.</param>
    /// <param name="eventSource">The serialized EventSource value.</param>
    /// <param name="receivedOn">The serialized ReceivedOn value.</param>
    /// <param name="target">The serialized Target value.</param>
    /// <param name="expectedGeneration">The serialized ExpectedGeneration value.</param>
    /// <param name="operation">The serialized Operation value.</param>
    /// <param name="requester">The serialized Requester value.</param>
    /// <param name="reason">The serialized Reason value.</param>
    /// <param name="timeoutTicks">The serialized TimeoutTicks value.</param>
    /// <param name="outcome">The serialized Outcome value.</param>
    /// <param name="stage">The serialized Stage value.</param>
    /// <param name="failureReason">The serialized FailureReason value.</param>
    [SerializationConstructor]
    public SupervisorActorOperationRecordedEvent(ActorSubject subject,
        Guid id,
        ActorEntityId entityId,
        long eventId,
        Guid commandId,
        string aggregateId,
        string eventSource,
        DateTime receivedOn,
        ActorThreadId target,
        long expectedGeneration,
        SupervisorActorOperationKind operation,
        string requester,
        string reason,
        long timeoutTicks,
        SupervisorOperationOutcome outcome,
        string stage,
        string failureReason)
    {
        Subject = subject;
        Id = id;
        EntityId = entityId;
        EventId = eventId;
        CommandId = commandId;
        AggregateId = aggregateId;
        EventSource = eventSource;
        ReceivedOn = receivedOn;
        Target = target;
        ExpectedGeneration = expectedGeneration;
        Operation = operation;
        Requester = requester;
        Reason = reason;
        TimeoutTicks = timeoutTicks;
        Outcome = outcome;
        Stage = stage;
        FailureReason = failureReason;
    }
}
