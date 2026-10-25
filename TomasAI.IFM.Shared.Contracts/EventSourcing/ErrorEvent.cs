using MessagePack;
using Newtonsoft.Json;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.EventSourcing;

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

    public IErrorEvent RoutedFrom(IEvent @event) 
    {
        ArgumentNullException.ThrowIfNull(@event);
        return RoutedFrom(@event.CommandId, @event.EventSource);
    }
    public IErrorEvent RoutedFrom(ICommand command) 
    {
        ArgumentNullException.ThrowIfNull(command);
        return RoutedFrom(command.CommandId, $"{command.RouteTo}");
    }

    public override string ToString() => JsonConvert.SerializeObject(this, Formatting.Indented);
}

