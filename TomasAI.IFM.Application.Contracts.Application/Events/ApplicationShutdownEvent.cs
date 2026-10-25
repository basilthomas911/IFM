using System;
using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Application.Shared.Events;

/// <summary>
/// Event published when an application shutdown is initiated.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record ApplicationShutdownEvent : IEvent<ApplicationEntityId>
{
    [IgnoreMember] public const string Actor = "ApplicationEvent";
    [IgnoreMember] public const string Verb = "Shutdown";
    [IgnoreMember] public const int ErrorCode = 10002;

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public Guid Id { get; init; }
    [Key(2)] public ApplicationEntityId EntityId { get; init; }
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

    /// <summary>Creates an empty shutdown event for serialization.</summary>
    public ApplicationShutdownEvent() { }

    /// <summary>
    /// MessagePack constructor used for deserialization.
    /// </summary>
    /// <param name="subject">The routed actor subject.</param>
    /// <param name="id">The event identifier.</param>
    /// <param name="entityId">The application value-date identity.</param>
    /// <param name="eventId">The durable event sequence identifier.</param>
    /// <param name="commandId">The originating command identifier.</param>
    /// <param name="aggregateId">The aggregate identifier.</param>
    /// <param name="eventSource">The event source.</param>
    /// <param name="receivedOn">The receive timestamp.</param>
    /// <param name="createdOn">The creation timestamp.</param>
    /// <param name="createdBy">The initiating principal.</param>
    [SerializationConstructor]
    public ApplicationShutdownEvent(
        ActorSubject subject,
        Guid id,
        ApplicationEntityId entityId,
        long eventId,
        Guid commandId,
        string aggregateId,
        string eventSource,
        DateTime receivedOn,
        DateTime createdOn,
        string createdBy)
    {
        Subject = subject;
        Id = id;
        EntityId = entityId;
        EventId = eventId;
        CommandId = commandId;
        AggregateId = aggregateId ?? string.Empty;
        EventSource = eventSource ?? string.Empty;
        ReceivedOn = receivedOn;
        CreatedOn = createdOn;
        CreatedBy = createdBy ?? string.Empty;
    }

    /// <summary>
    /// Convert this denormalize event into a completed event which indicates successful handling.
    /// </summary>
    /// <typeparam name="TComplete">The requested completion contract.</typeparam>
    /// <typeparam name="TEntityId">The requested entity-identifier type.</typeparam>
    /// <returns>The correlated shutdown completion event.</returns>
    /// <exception cref="InvalidOperationException">The requested entity-identifier type is not the application identity.</exception>
    public ICompleteEvent<TEntityId> ToCompleteEvent<TComplete, TEntityId>()
        where TComplete : ICompleteEvent<TEntityId>
        where TEntityId : IActorEntityId
    {
        if (typeof(TEntityId) != typeof(ApplicationEntityId))
            throw new InvalidOperationException($"ToCompletedEvent: unsupported entity id type {typeof(TEntityId).FullName}. Expected {typeof(ApplicationEntityId).FullName}.");

        var completed = new ApplicationShutdownCompleteEvent
        {
            Subject = new ActorSubject(ActorType.Event, ApplicationShutdownCompleteEvent.Actor, ApplicationShutdownCompleteEvent.Verb, Subject.EntityId),
            EntityId = this.EntityId,
            Id = this.Id,
            EventId = this.EventId,
            CommandId = this.CommandId,
            AggregateId = this.AggregateId,
            EventSource = this.EventSource,
            ReceivedOn = this.ReceivedOn,
            CreatedOn = this.CreatedOn,
            CreatedBy = this.CreatedBy
        };

        return (ICompleteEvent<TEntityId>)(object)completed;
    }

    /// <summary>
    /// Convert this denormalize event into a failed error event describing the provided exception.
    /// </summary>
    /// <typeparam name="TFail">The requested failure contract.</typeparam>
    /// <typeparam name="TEntityId">The requested entity-identifier type.</typeparam>
    /// <param name="ex">The originating failure.</param>
    /// <returns>The correlated shutdown failure event.</returns>
    public IErrorEvent<TEntityId> ToFailEvent<TFail, TEntityId>(Exception ex)
        where TFail : IErrorEvent<TEntityId>
        where TEntityId : IActorEntityId
    {
        var failed = new ApplicationShutdownFailEvent
        {
            Subject = new ActorSubject(ActorType.Event, ApplicationShutdownFailEvent.Actor, ApplicationShutdownFailEvent.Verb, Subject.EntityId),
            EntityId = this.EntityId,
            Id = this.Id,
            ErrorDate = DateTime.Now,
            EventId = this.EventId,
            CommandId = this.CommandId,
            EventSource = this.EventSource,
            ErrorMessage = ex?.Message ?? string.Empty,
            ErrorType = ErrorType.System,
            ErrorCode = ErrorCode,
            ReceivedOn = this.ReceivedOn,
            AggregateId = this.AggregateId
        };

        return (IErrorEvent<TEntityId>)(object)failed;
    }
}
