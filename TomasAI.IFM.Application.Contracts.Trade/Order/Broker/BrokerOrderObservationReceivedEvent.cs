using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Order.Broker;

/// <summary>Transports one normalized framework observation through the BrokerOrder event mailbox.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record BrokerOrderObservationReceivedEvent : IEvent<BrokerOrderId>
{

    /// <summary>Creates an empty event for serialization and existing callers.</summary>
    public BrokerOrderObservationReceivedEvent() { }

    /// <summary>Rehydrates every published event field in permanent numeric-key order.</summary>
    /// <param name="subject">The Subject field.</param>
    /// <param name="id">The Id field.</param>
    /// <param name="entityId">The EntityId field.</param>
    /// <param name="eventId">The EventId field.</param>
    /// <param name="commandId">The CommandId field.</param>
    /// <param name="aggregateId">The AggregateId field.</param>
    /// <param name="eventSource">The EventSource field.</param>
    /// <param name="receivedOn">The ReceivedOn field.</param>
    /// <param name="observation">The Observation field.</param>
    [SerializationConstructor]
    public BrokerOrderObservationReceivedEvent(ActorSubject subject, Guid id, BrokerOrderId entityId, long eventId, Guid commandId, string aggregateId, string eventSource, DateTime receivedOn, BrokerOrderObservationEvidence observation)
    {
        Subject = subject;
        Id = id;
        EntityId = entityId;
        EventId = eventId;
        CommandId = commandId;
        AggregateId = aggregateId;
        EventSource = eventSource;
        ReceivedOn = receivedOn;
        Observation = observation;
    }
    public const string Actor = BrokerOrderActorNames.Event;
    public const string Verb = "BrokerOrderObservationReceived";
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public Guid Id { get; init; }
    [Key(2)] public BrokerOrderId EntityId { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; } = string.Empty;
    [Key(6)] public string EventSource { get; init; } = string.Empty;
    [Key(7)] public DateTime ReceivedOn { get; init; }
    [Key(8)] public BrokerOrderObservationEvidence Observation { get; init; } = new();
    [IgnoreMember] public string UserName => string.Empty;
    [IgnoreMember] public string EventName => nameof(BrokerOrderObservationReceivedEvent);
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;
}
