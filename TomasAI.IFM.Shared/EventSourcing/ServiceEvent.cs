using MessagePack;
using Newtonsoft.Json;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.EventSourcing;

/// <summary>
/// Represents a generic service-level event emitted by internal services.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record ServiceEvent : IEvent
{
    [IgnoreMember] private ActorSubject _subject;
    [IgnoreMember] private string _entityId;
    [IgnoreMember] private long _eventId;
    [IgnoreMember] private Guid _commandId;
    [IgnoreMember] private string _aggregateId;
    [IgnoreMember] private string _eventSource;
    [IgnoreMember] private DateTime _receivedOn;
    [IgnoreMember] private Guid _id;

    /// <summary>Actor subject that produced the event.</summary>
    [Key(0)] public ActorSubject Subject { get => _subject; init => _subject = value; }

    /// <summary>Associated entity identifier for the event.</summary>
    [Key(1)] public string EntityId { get => _entityId; init => _entityId = value; }

    /// <summary>Event sequence identifier.</summary>
    [Key(2)] public long EventId { get => _eventId; init => _eventId = value; }

    /// <summary>Correlation command identifier.</summary>
    [Key(3)] public Guid CommandId { get => _commandId; init => _commandId = value; }

    /// <summary>Aggregate identifier for routing/partitioning.</summary>
    [Key(4)] public string AggregateId { get => _aggregateId; init => _aggregateId = value; }

    /// <summary>Event source name.</summary>
    [Key(5)] public string EventSource { get => _eventSource; init => _eventSource = value; }

    /// <summary>Time the event was received.</summary>
    [Key(6)] public DateTime ReceivedOn { get => _receivedOn; init => _receivedOn = value; }

    /// <summary>Unique identifier for the event.</summary>
    [Key(7)] public Guid Id { get => _id; init => _id = value; }

    [IgnoreMember] public string EventName => this.GetType().Name;
    [IgnoreMember] public string UserName => $"{Environment.UserDomainName}\\{Environment.UserName}";
    [IgnoreMember] public EventType EventType => EventType.ServiceEvent;

    public ServiceEvent() { }

    /// <summary>MessagePack serialization constructor.</summary>
    [SerializationConstructor]
    public ServiceEvent(ActorSubject subject, string entityId, long eventId, Guid commandId, string aggregateId, string eventSource, DateTime receivedOn, Guid id)
    {
        _subject = subject;
        _entityId = entityId;
        _eventId = eventId;
        _commandId = commandId;
        _aggregateId = aggregateId ?? string.Empty;
        _eventSource = eventSource ?? string.Empty;
        _receivedOn = receivedOn;
        _id = id;
    }

    public void CheckForEmptyCommandId()
    {
        if (this.CommandId == Guid.Empty)
            throw new InvalidOperationException($"ServiceEvent.CheckForEmptyCommandId: {this.GetType().Name}.CommandId is empty");
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

    public IEvent RoutedFrom(Guid correlationId, string eventSource)
    {
        _commandId = correlationId;
        _eventSource = eventSource;
        return this;
    }

    public IEvent RoutedFrom(IEvent @event) 
    {
        ArgumentNullException.ThrowIfNull(@event);
        return RoutedFrom(@event.CommandId, @event.EventSource);
    }
    public IEvent RoutedFrom(ICommand command) 
    {
        ArgumentNullException.ThrowIfNull(command);
        return RoutedFrom(command.CommandId, $"{command.RouteTo}");
    }

    public override string ToString() => $"{this.GetType().Name}: {JsonConvert.SerializeObject(this, Formatting.Indented)}";
}

