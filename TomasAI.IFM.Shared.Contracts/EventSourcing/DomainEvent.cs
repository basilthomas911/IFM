using MessagePack;
using Newtonsoft.Json;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.EventSourcing;

/// <summary>
/// Represents the base class for all domain events in the system.
/// </summary>
/// <remarks>A domain event captures an occurrence within the domain that is of significance to the business. This
/// class provides common properties and methods for handling domain events, such as unique identification, event
/// metadata, and routing information. Derived classes should represent specific types of domain events.</remarks>
[MessagePackObject(AllowPrivate = true)]
public record DomainEvent : IEvent
{
    [IgnoreMember] private ActorSubject _subject;
    [IgnoreMember] private string _entityId;
    [IgnoreMember] private Guid _id;
    [IgnoreMember] private long _eventId;
    [IgnoreMember] private Guid _commandId;
    [IgnoreMember] private string _aggregateId;
    [IgnoreMember] private string _eventSource;
    [IgnoreMember] private DateTime _receivedOn;

    [Key(0)] public ActorSubject Subject { get => _subject; init => _subject = value; }
    [Key(1)] public string EntityId { get => _entityId; init => _entityId = value; }
    [Key(2)] public Guid Id { get => _id; init => _id = value; }
    [Key(3)] public long EventId { get => _eventId; init => _eventId = value; }
    [Key(4)] public Guid CommandId { get => _commandId; init => _commandId = value; }
    [Key(5)] public string AggregateId { get => _aggregateId; init => _aggregateId = value; }
    [Key(6)] public string EventSource { get => _eventSource; init => _eventSource = value; }
    [Key(7)] public DateTime ReceivedOn { get => _receivedOn; init => _receivedOn = value; }

    [IgnoreMember]
    public string UserName => $"{Environment.UserDomainName}\\{Environment.UserName}";

    [IgnoreMember]
    public string EventName => GetType().Name;

    [IgnoreMember]
    public EventType EventType => EventType.DomainEvent;

    public DomainEvent() { }

    /// <summary>
    /// MessagePack serialization constructor.
    /// </summary>
    //[SerializationConstructor]
    public DomainEvent(ActorSubject subject, string entityId, Guid id, long eventId, Guid commandId, string aggregateId, string eventSource, DateTime receivedOn)
    {
        _subject = subject;
        _entityId = entityId;
        _id = id;
        _eventId = eventId;
        _commandId = commandId;
        _aggregateId = aggregateId ?? string.Empty;
        _eventSource = eventSource ?? string.Empty;
        _receivedOn = receivedOn;
    }

    public void CheckForEmptyCommandId()
    {
        if (this.CommandId == Guid.Empty)
            throw new InvalidOperationException($"DomainEvent.CheckForEmptyCommandId: {GetType().Name}.CommandId is empty");
    }


    public IEvent SetEventSource(string eventSource)
    {
        _eventSource = eventSource;
        return this;
    }

    public IEvent SetReceivedOn(DateTime receivedOn)
    {
        _receivedOn = receivedOn;
        return this;
    }

    public DomainEvent SetSubject(string actorName, string actorVerb, string entityId)
    {
        _subject = new ActorSubject(ActorType.Event, actorName, actorVerb, entityId);
        _entityId = entityId;
        return this;
    }


    public IEvent RoutedFrom(Guid correlationId, string aggregateId, string eventSource)
    {
        _commandId = correlationId;
        _aggregateId = aggregateId;
        _eventSource = eventSource;
        return this;
    }

    public IEvent RoutedFrom(IEvent @event) 
    {
        ArgumentNullException.ThrowIfNull(@event);
        return RoutedFrom(@event.CommandId, @event.AggregateId, @event.EventSource);
    }

    public IEvent RoutedFrom(ICommand command) 
    {
        ArgumentNullException.ThrowIfNull(command);
        return RoutedFrom(command.CommandId, command.StreamId, command.EventSource);
    }

    public virtual ICompleteEvent? ToCompletedEvent() => null;

    public virtual IErrorEvent? ToFailedEvent(Exception ex) => null;

    public override string ToString() => JsonConvert.SerializeObject(this, Formatting.Indented);

}

