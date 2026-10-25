using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Supervisor.Shared.Recovery;

/// <summary>Private committed outcome for a generation-correlated recovery canary.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record RecoveryCanaryRecordedEvent : IEvent<ActorEntityId>
{
    public const string Actor = "SupervisorRecoveryCanaryProjector";
    public const string Verb = "Recorded";
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
    [IgnoreMember] public string EventName => nameof(RecoveryCanaryRecordedEvent);
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;

    /// <summary>Creates a record for serializers and state application.</summary>
    public RecoveryCanaryRecordedEvent() { }

    /// <summary>Reconstructs the complete permanent source-event schema.</summary>
    /// <param name="subject">The routed actor subject.</param>
    /// <param name="id">The event identity.</param>
    /// <param name="entityId">The aggregate identity.</param>
    /// <param name="eventId">The event-log sequence.</param>
    /// <param name="commandId">The originating command identity.</param>
    /// <param name="aggregateId">The aggregate stream identity.</param>
    /// <param name="eventSource">The originating actor.</param>
    /// <param name="receivedOn">The event receipt time.</param>
    /// <param name="correlationId">The proof correlation identity.</param>
    /// <param name="generationId">The publisher generation identity.</param>
    /// <param name="valueDate">The trading value date.</param>
    /// <param name="dataset">The Databento dataset.</param>
    [SerializationConstructor]
    public RecoveryCanaryRecordedEvent(ActorSubject subject, Guid id, ActorEntityId entityId,
        long eventId, Guid commandId, string aggregateId, string eventSource, DateTime receivedOn,
        Guid correlationId, Guid generationId, DateOnly valueDate, string dataset)
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
