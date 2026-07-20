using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.StatusConsole.ViewModels;

namespace TomasAI.IFM.Shared.StatusConsole.Events;

[MessagePackObject(AllowPrivate = true)]
public record StatusConsoleLoggedEvent : IEvent<ActorEntityId>
{
    [IgnoreMember] public const string Actor = "StatusConsoleEvent";
    [IgnoreMember] public const string Verb = "Logged";
    [IgnoreMember] public const int ErrorCode = 0;
    [IgnoreMember] static readonly string CachedUserName = $"{Environment.UserDomainName}\\{Environment.UserName}";

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public Guid Id { get; init; }
    [Key(2)] public ActorEntityId EntityId { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; }
    [Key(6)] public string EventSource { get; init; }
    [Key(7)] public DateTime ReceivedOn { get; init; }

    [Key(8)] public StatusConsoleLogReadModel StatusConsoleLog { get; init; }

    [IgnoreMember] public string UserName => CachedUserName;
    [IgnoreMember] public string EventName => nameof(StatusConsoleLoggedEvent);
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;

    public StatusConsoleLoggedEvent() { }

    [SerializationConstructor]
    public StatusConsoleLoggedEvent(
        ActorSubject subject,
        Guid id,
        ActorEntityId entityId,
        long eventId,
        Guid commandId,
        string aggregateId,
        string eventSource,
        DateTime receivedOn,
        StatusConsoleLogReadModel statusConsoleLog)
    {
        Subject = subject;
        Id = id;
        EntityId = entityId;
        EventId = eventId;
        CommandId = commandId;
        AggregateId = aggregateId ?? string.Empty;
        EventSource = eventSource ?? string.Empty;
        ReceivedOn = receivedOn;
        StatusConsoleLog = statusConsoleLog;
    }

    [IgnoreMember]
    public bool IsValid
        => true;
    
}
