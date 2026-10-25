using System;
using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Application.Shared.Events;

/// <summary>
/// Event published when the application shutdown has completed successfully.
/// Carries metadata from the original shutdown event.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record ApplicationShutdownCompleteEvent : ICompleteEvent<ApplicationEntityId>
{
    [IgnoreMember] public const string Actor = "ApplicationEvent";
    [IgnoreMember] public const string Verb = "ShutdownComplete";
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public ApplicationEntityId EntityId { get; init; }
    [Key(2)] public Guid Id { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; }
    [Key(6)] public string EventSource { get; init; }
    [Key(7)] public DateTime ReceivedOn { get; init; }
    [Key(8)] public DateTime CreatedOn { get; init; }
    [Key(9)] public string CreatedBy { get; init; }

    [IgnoreMember] public string UserName => $"{Environment.UserDomainName}\\{Environment.UserName}";
    [IgnoreMember] public string EventName => GetType().Name;
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;

    /// <summary>Creates an empty shutdown complete event for serialization.</summary>
    public ApplicationShutdownCompleteEvent() { }

    /// <summary>Rehydrates the application completion notification from its wire fields.</summary>
    /// <param name="subject">The routed actor subject.</param>
    /// <param name="entityId">The application value-date identity.</param>
    /// <param name="id">The event identifier.</param>
    /// <param name="eventId">The durable event sequence identifier.</param>
    /// <param name="commandId">The originating command identifier.</param>
    /// <param name="aggregateId">The aggregate identifier.</param>
    /// <param name="eventSource">The event source.</param>
    /// <param name="receivedOn">The receive timestamp.</param>
    /// <param name="createdOn">The creation timestamp.</param>
    /// <param name="createdBy">The initiating principal.</param>
    [SerializationConstructor]
    public ApplicationShutdownCompleteEvent(
        ActorSubject subject,
        ApplicationEntityId entityId,
        Guid id,
        long eventId,
        Guid commandId,
        string aggregateId,
        string eventSource,
        DateTime receivedOn,
        DateTime createdOn,
        string createdBy)
    {
        Subject = subject;
        EntityId = entityId;
        Id = id;
        EventId = eventId;
        CommandId = commandId;
        AggregateId = aggregateId ?? string.Empty;
        EventSource = eventSource ?? string.Empty;
        ReceivedOn = receivedOn;
        CreatedOn = createdOn;
        CreatedBy = createdBy ?? string.Empty;
    }
}
