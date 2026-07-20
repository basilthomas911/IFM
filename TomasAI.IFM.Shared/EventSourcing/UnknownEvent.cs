using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.EventSourcing;

[MessagePackObject(AllowPrivate = true)]
public record UnknownEvent : IEvent<ActorEntityId>
{
    [IgnoreMember] public const string Actor = "Unknown";
    [IgnoreMember] public const string Verb = "Unknown";
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

    [Key(8)] public long EventSourceId { get; init; }
    [Key(9)] public long EventSourceVersion { get; init; }
    [Key(10)] public string EventTypeName { get; init; }
    [Key(11)] public string EventData { get; init; }
    [Key(12)] public DateTime EventDate { get; init; }

    [IgnoreMember] public string UserName => CachedUserName;
    [IgnoreMember] public string EventName => nameof(UnknownEvent);
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;

    public UnknownEvent() { }

    [SerializationConstructor]
    public UnknownEvent(
        ActorSubject subject,
        Guid id,
        ActorEntityId entityId,
        long eventId,
        Guid commandId,
        string aggregateId,
        string eventSource,
        DateTime receivedOn,
        long eventSourceId,
        long eventSourceVersion,
        string eventTypeName,
        string eventData,
        DateTime eventDate)
    {
        Subject = subject;
        Id = id;
        EntityId = entityId;
        EventId = eventId;
        CommandId = commandId;
        AggregateId = aggregateId ?? string.Empty;
        EventSource = eventSource ?? string.Empty;
        ReceivedOn = receivedOn;
        EventSourceId = eventSourceId;
        EventSourceVersion = eventSourceVersion;
        EventTypeName = eventTypeName ?? string.Empty;
        EventData = eventData ?? string.Empty;
        EventDate = eventDate;
    }
}
