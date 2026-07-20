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

    public IEvent RoutedFrom(IEvent @event) => RoutedFrom(@event.CommandId, @event.AggregateId, @event.EventSource);

    public IEvent RoutedFrom(ICommand command) => RoutedFrom(command.CommandId, command.StreamId, command.EventSource);

    public virtual ICompleteEvent? ToCompletedEvent() => null;

    public virtual IErrorEvent? ToFailedEvent(Exception ex) => null;

    public override string ToString() => JsonConvert.SerializeObject(this, Formatting.Indented);

}

/// <summary>
/// Lightweight record form of a domain event suitable for storage/transport.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record struct DomainEventRecord : IEvent
{
    public DomainEventRecord() => Id = Guid.NewGuid();

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public string EntityId { get; init; }
    [Key(2)] public Guid Id { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; }
    [Key(6)] public string EventSource { get; init; }
    [Key(7)] public DateTime ReceivedOn { get; init; }

    [IgnoreMember]
    public string UserName => $"{Environment.UserDomainName}\\{Environment.UserName}";

    [IgnoreMember]
    public string EventName => this.GetType().Name;

    [IgnoreMember]
    public EventType EventType => EventType.DomainEvent;

    [SerializationConstructor]
    public DomainEventRecord(ActorSubject subject, string entityId, Guid id, long eventId, Guid commandId, string aggregateId, string eventSource, DateTime receivedOn)
    {
        Subject = subject;
        EntityId = entityId;
        Id = id;
        EventId = eventId;
        CommandId = commandId;
        AggregateId = aggregateId ?? string.Empty;
        EventSource = eventSource ?? string.Empty;
        ReceivedOn = receivedOn;
    }

    public void CheckForEmptyCommandId()
    {
        if (this.CommandId == Guid.Empty)
            throw new InvalidOperationException($"DomainEvent.CheckForEmptyCommandId: {GetType().Name}.CommandId is empty");
    }

    public readonly IEvent SetEventSource(string eventSource)
    {
        return (IEvent)(this with { EventSource = eventSource });
    }

    public readonly IEvent SetReceivedOn(DateTime receivedOn)
    {
        return (IEvent)(this with { ReceivedOn = receivedOn });
    }

    public readonly IEvent RoutedFrom(Guid correlationId, string aggregateId, string eventSource)
    {
        return (IEvent)(this with { CommandId = correlationId, AggregateId = aggregateId, EventSource = eventSource });
    }

    public readonly IEvent RoutedFrom(ICommand command) => RoutedFrom(command.CommandId, command.StreamId, command.EventSource);

    public override readonly string ToString() => JsonConvert.SerializeObject(this, Formatting.Indented);

}

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

    public IEvent RoutedFrom(IEvent @event) => RoutedFrom(@event.CommandId, @event.EventSource);
    public IEvent RoutedFrom(ICommand command) => RoutedFrom(command.CommandId, $"{command.RouteTo}");

    public override string ToString() => $"{this.GetType().Name}: {JsonConvert.SerializeObject(this, Formatting.Indented)}";
}

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

    public IEvent RoutedFrom(IEvent @event) => RoutedFrom(@event.CommandId, @event.EventSource);
    public IEvent RoutedFrom(ICommand command) => RoutedFrom(command.CommandId, $"{command.RouteTo}");

    public override string ToString() => $"{this.GetType().Name}: {JsonConvert.SerializeObject(this, Formatting.Indented)}";
}

/// <summary>
/// Represents a completed/finished domain action event.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record CompleteEvent : ICompleteEvent
{
    [IgnoreMember] private ActorSubject _subject;
    [IgnoreMember] private string _entityId;
    [IgnoreMember] private Guid _id;
    [IgnoreMember] private Guid _commandId;
    [IgnoreMember] private string _aggregateId;
    [IgnoreMember] private long _eventId;
    [IgnoreMember] private string _eventSource;
    [IgnoreMember] private DateTime _receivedOn;

    [Key(0)] public ActorSubject Subject { get => _subject; init => _subject = value; }
    [Key(1)] public string EntityId { get => _entityId; init => _entityId = value; }
    [Key(2)] public Guid Id { get => _id; init => _id = value; }
    [Key(3)] public Guid CommandId { get => _commandId; init => _commandId = value; }
    [Key(4)] public string AggregateId { get => _aggregateId; init => _aggregateId = value; }
    [Key(5)] public long EventId { get => _eventId; init => _eventId = value; }
    [Key(6)] public string EventSource { get => _eventSource; init => _eventSource = value; }
    [Key(7)] public DateTime ReceivedOn { get => _receivedOn; init => _receivedOn = value; }
    [IgnoreMember] public string EventName => this.GetType().Name;
    [IgnoreMember] public string UserName => $"{Environment.UserDomainName}\\{Environment.UserName}";
    [IgnoreMember] public EventType EventType => EventType.CompletedEvent;

    public CompleteEvent() { }  

    [SerializationConstructor]
    public CompleteEvent(ActorSubject subject, string entityId, Guid id, Guid commandId, string aggregateId, long eventId, string eventSource, DateTime receivedOn)
    {
        _subject = subject;
        _entityId = entityId;
        _id = id;
        _commandId = commandId;
        _aggregateId = aggregateId ?? string.Empty;
        _eventId = eventId;
        _eventSource = eventSource ?? string.Empty;
        _receivedOn = receivedOn;
    }

    public void CheckForEmptyCommandId()
    {
        if (this.CommandId == Guid.Empty)
            throw new InvalidOperationException($"CompleteEvent.CheckForEmptyCommandId: {this.GetType().Name}.CommandId is empty");
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

    public ICompleteEvent RoutedFrom(Guid correlationId, string eventSource)
    {
        _commandId = correlationId;
        _eventSource = eventSource;
        return this;
    }

    public ICompleteEvent RoutedFrom(IEvent @event) => RoutedFrom(@event.CommandId, @event.EventSource);
    public ICompleteEvent RoutedFrom(ICommand command) => RoutedFrom(command.CommandId, $"{command.RouteTo}");

    public override string ToString() => JsonConvert.SerializeObject(this, Formatting.Indented);
}

[MessagePackObject(AllowPrivate = true)]
public record ErrorEvent : IErrorEvent
{
    [IgnoreMember] private ActorSubject _subject;
    [IgnoreMember] private string _entityId;
    [IgnoreMember] private Guid _id;
    [IgnoreMember] private DateTime _errorDate;
    [IgnoreMember] private long _eventId;
    [IgnoreMember] private Guid _commandId;
    [IgnoreMember] private string _eventSource;
    [IgnoreMember] private string _errorMessage;
    [IgnoreMember] private int _errorCode;
    [IgnoreMember] private ErrorType _errorType;
    [IgnoreMember] private string _errorData;
    [IgnoreMember] private DateTime _receivedOn;
    [IgnoreMember] private string _aggregateId;
    [IgnoreMember] private string _commandName;
    [IgnoreMember] private string _commandData;
    [IgnoreMember] private string _routeTo;

    [Key(0)] public ActorSubject Subject { get => _subject; init => _subject = value; }
    [Key(1)] public string EntityId { get => _entityId; init => _entityId = value; }
    [Key(2)] public Guid Id { get => _id; init => _id = value; }
    [Key(3)] public DateTime ErrorDate { get => _errorDate; init => _errorDate = value; }
    [Key(4)] public long EventId { get => _eventId; init => _eventId = value; }
    [Key(5)] public Guid CommandId { get => _commandId; init => _commandId = value; }
    [Key(6)] public string EventSource { get => _eventSource; init => _eventSource = value; }
    [Key(7)] public string ErrorMessage { get => _errorMessage; init => _errorMessage = value; }
    [Key(8)] public int ErrorCode { get => _errorCode; init => _errorCode = value; }
    [Key(9)] public ErrorType ErrorType { get => _errorType; init => _errorType = value; }
    [Key(10)] public string ErrorData { get => _errorData; init => _errorData = value; }
    [Key(11)] public DateTime ReceivedOn { get => _receivedOn; init => _receivedOn = value; }
    [Key(12)] public string AggregateId { get => _aggregateId; init => _aggregateId = value; }
    [Key(13)] public string CommandName { get => _commandName; init => _commandName = value; }
    [Key(14)] public string CommandData { get => _commandData; init => _commandData = value; }
    [Key(15)] public string RouteTo { get => _routeTo; init => _routeTo = value; }
    [IgnoreMember] public string EventName => this.GetType().Name;
    [IgnoreMember] public string UserName => $"{Environment.UserDomainName}\\{Environment.UserName}";
    [IgnoreMember] public EventType EventType => EventType.ErrorEvent;

    public ErrorEvent() { }

    [SerializationConstructor]
    public ErrorEvent(ActorSubject subject, string entityId, Guid id, DateTime errorDate, long eventId, Guid commandId, string eventSource, string errorMessage, int errorCode, ErrorType errorType, string errorData, DateTime receivedOn, string aggregateId, string commandName, string commandData, string routeTo)
    {
        _subject = subject;
        _entityId = entityId;
        _id = id;
        _errorDate = errorDate;
        _eventId = eventId;
        _commandId = commandId;
        _eventSource = eventSource ?? string.Empty;
        _errorMessage = errorMessage ?? string.Empty;
        _errorCode = errorCode;
        _errorType = errorType;
        _errorData = errorData ?? string.Empty;
        _receivedOn = receivedOn;
        _aggregateId = aggregateId ?? string.Empty;
        _commandName = commandName ?? string.Empty;
        _commandData = commandData ?? string.Empty;
        _routeTo = routeTo ?? string.Empty;
    }

    public void CheckForEmptyCommandId()
    {
        if (this.CommandId == Guid.Empty)
            throw new InvalidOperationException($"ErrorEvent.CheckForEmptyCommandId: {this.GetType().Name}.CommandId is empty");
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

    public IErrorEvent RoutedFrom(Guid correlationId, string eventSource)
    {
        _commandId = correlationId;
        _eventSource = eventSource;
        return this;
    }

    public IErrorEvent RoutedFrom(IEvent @event) => RoutedFrom(@event.CommandId, @event.EventSource);
    public IErrorEvent RoutedFrom(ICommand command) => RoutedFrom(command.CommandId, $"{command.RouteTo}");

    public override string ToString() => JsonConvert.SerializeObject(this, Formatting.Indented);
}

public record CommandExceptionEvent : ErrorEvent, IExceptionEvent, IErrorEventConverter
{

    public IErrorEvent ToErrorEvent(ICommand command, Exception ex)
        => new CommandExceptionEvent
        {
            CommandId = command.CommandId,
            CommandName = command.GetType().Name,
            RouteTo = $"{command.RouteTo}",
            ErrorType = ErrorType.Command,
            ErrorMessage = ex.Message,
            ErrorCode = command.ErrorCode,
            ErrorData = $"{ex}",
            AggregateId = command.StreamId,
            CommandData = JsonConvert.SerializeObject(command, Formatting.Indented)
        };
}

public record QueryExceptionEvent : ErrorEvent, IExceptionEvent
{

    public QueryExceptionEvent() { }

    public QueryExceptionEvent(IQuery query, Exception ex)
    {
        ErrorType = ErrorType.Query;
        ErrorMessage = ex.Message;
        ErrorCode = query.ErrorCode;
        ErrorData = JsonConvert.SerializeObject(query, Formatting.Indented);
    }

}

public record EventServiceExceptionEvent : ErrorEvent, IExceptionEvent
{
    public string EventData { get; init; }
}

