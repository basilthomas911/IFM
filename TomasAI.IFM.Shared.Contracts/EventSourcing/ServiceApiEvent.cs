using MessagePack;
using Newtonsoft.Json;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.EventSourcing;

/// <summary>
/// Represents an event coming from a service API boundary.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record ServiceApiEvent : IEvent
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
    [IgnoreMember] public string UserName => $"{Environment.UserDomainName}\\{Environment.UserName}";
    [IgnoreMember] public EventType EventType => EventSourcing.EventType.ServiceApiEvent;
    [Key(6)] public string EventSource { get => _eventSource; init => _eventSource = value; }
    [Key(7)] public DateTime ReceivedOn { get => _receivedOn; init => _receivedOn = value; }

    [IgnoreMember] public string EventName => this.GetType().Name;

    public ServiceApiEvent() { }

    [SerializationConstructor]
    public ServiceApiEvent(ActorSubject subject, string entityId, Guid id, long eventId, Guid commandId, string aggregateId, string eventSource, DateTime receivedOn)
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
            throw new InvalidOperationException($"ServiceApiEvent.CheckForEmptyCommandId: {this.GetType().Name}.CommandId is empty");
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

