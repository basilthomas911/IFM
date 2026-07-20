using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.SystemAdmin.Events;

[MessagePackObject(AllowPrivate = true)]
public record DatabaseBackupInfoMessageEvent : IEvent<ActorEntityId>
{
    [IgnoreMember] public const string Actor = "DatabaseBackup";
    [IgnoreMember] public const string Verb = "InfoMessage";
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

    [Key(8)] public string DatabaseName { get; init; }
    [Key(9)] public string InfoMessage { get; init; }
    [Key(10)] public DateTime CreatedOn { get; init; }

    [IgnoreMember] public string UserName => CachedUserName;
    [IgnoreMember] public string EventName => nameof(DatabaseBackupInfoMessageEvent);
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;

    public DatabaseBackupInfoMessageEvent() { }

    [SerializationConstructor]
    public DatabaseBackupInfoMessageEvent(
        ActorSubject subject,
        Guid id,
        ActorEntityId entityId,
        long eventId,
        Guid commandId,
        string aggregateId,
        string eventSource,
        DateTime receivedOn,
        string databaseName,
        string infoMessage,
        DateTime createdOn)
    {
        Subject = subject;
        Id = id;
        EntityId = entityId;
        EventId = eventId;
        CommandId = commandId;
        AggregateId = aggregateId ?? string.Empty;
        EventSource = eventSource ?? string.Empty;
        ReceivedOn = receivedOn;
        DatabaseName = databaseName ?? string.Empty;
        InfoMessage = infoMessage ?? string.Empty;
        CreatedOn = createdOn;
    }
}
