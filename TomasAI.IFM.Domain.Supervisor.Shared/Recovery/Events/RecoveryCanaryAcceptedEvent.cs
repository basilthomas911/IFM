using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Supervisor.Shared.Recovery;

/// <summary>Canary event acknowledged by JetStream before the projector processes it.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record RecoveryCanaryAcceptedEvent : IEvent<ActorEntityId>
{
    public const string Actor = "SupervisorRecoveryCanaryProjector";
    public const string Verb = "Accepted";
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public Guid Id { get; init; }
    [Key(2)] public ActorEntityId EntityId { get; init; } = ActorEntityId.Default;
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; } = string.Empty;
    [Key(6)] public string EventSource { get; init; } = RecoveryCanaryCommand.Actor;
    [Key(7)] public DateTime ReceivedOn { get; init; }
    [Key(8)] public Guid CorrelationId { get; init; }
    [Key(9)] public Guid GenerationId { get; init; }
    [Key(10)] public DateOnly ValueDate { get; init; }
    [Key(11)] public string Dataset { get; init; } = string.Empty;
    [IgnoreMember] public string UserName => string.Empty;
    [IgnoreMember] public string EventName => nameof(RecoveryCanaryAcceptedEvent);
    [IgnoreMember] public EventType EventType => EventType.ServiceApiEvent;

    /// <summary>Creates the event for serialization and object-initializer callers.</summary>
    public RecoveryCanaryAcceptedEvent() { }

    /// <summary>Reconstructs every permanent MessagePack field in numeric key order.</summary>
    /// <param name="subject">The serialized Subject value.</param>
    /// <param name="id">The serialized Id value.</param>
    /// <param name="entityId">The serialized EntityId value.</param>
    /// <param name="eventId">The serialized EventId value.</param>
    /// <param name="commandId">The serialized CommandId value.</param>
    /// <param name="aggregateId">The serialized AggregateId value.</param>
    /// <param name="eventSource">The serialized EventSource value.</param>
    /// <param name="receivedOn">The serialized ReceivedOn value.</param>
    /// <param name="correlationId">The serialized CorrelationId value.</param>
    /// <param name="generationId">The serialized GenerationId value.</param>
    /// <param name="valueDate">The serialized ValueDate value.</param>
    /// <param name="dataset">The serialized Dataset value.</param>
    [SerializationConstructor]
    public RecoveryCanaryAcceptedEvent(ActorSubject subject,
        Guid id,
        ActorEntityId entityId,
        long eventId,
        Guid commandId,
        string aggregateId,
        string eventSource,
        DateTime receivedOn,
        Guid correlationId,
        Guid generationId,
        DateOnly valueDate,
        string dataset)
    {
        Subject = subject;
        Id = id;
        EntityId = entityId;
        EventId = eventId;
        CommandId = commandId;
        AggregateId = aggregateId;
        EventSource = eventSource;
        ReceivedOn = receivedOn;
        CorrelationId = correlationId;
        GenerationId = generationId;
        ValueDate = valueDate;
        Dataset = dataset;
    }
}
