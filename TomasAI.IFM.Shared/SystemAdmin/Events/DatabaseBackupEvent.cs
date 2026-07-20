using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.SystemAdmin.Events;

[MessagePackObject(AllowPrivate = true)]
public record DatabaseBackupEvent : IEvent<DatabaseBackupId>
{
    [IgnoreMember] public const string Actor = "DatabaseBackupEvent";
    [IgnoreMember] public const string Verb = "Backup";
    [IgnoreMember] public const int ErrorCode = 8001;
    [IgnoreMember] static readonly string CachedUserName = $"{Environment.UserDomainName}\\{Environment.UserName}";

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public Guid Id { get; init; }
    [Key(2)] public DatabaseBackupId EntityId { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; }
    [Key(6)] public string EventSource { get; init; }
    [Key(7)] public DateTime ReceivedOn { get; init; }

    [Key(8)] public string DatabaseName { get; init; }
    [Key(9)] public DatabaseBackupType BackupType { get; init; }
    [Key(10)] public int CommandTimeout { get; init; }
    [Key(11)] public DateTime CreatedOn { get; init; }
    [Key(12)] public string CreatedBy { get; init; }

    [IgnoreMember] public string UserName => CachedUserName;
    [IgnoreMember] public string EventName => nameof(DatabaseBackupEvent);
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;

    public DatabaseBackupEvent() { }

    [SerializationConstructor]
    public DatabaseBackupEvent(
        ActorSubject subject,
        Guid id,
        DatabaseBackupId entityId,
        long eventId,
        Guid commandId,
        string aggregateId,
        string eventSource,
        DateTime receivedOn,
        string databaseName,
        DatabaseBackupType backupType,
        int commandTimeout,
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
        DatabaseName = databaseName ?? string.Empty;
        BackupType = backupType;
        CommandTimeout = commandTimeout;
        CreatedOn = createdOn;
        CreatedBy = createdBy ?? string.Empty;
    }

    public ICompleteEvent<TEntityId> ToCompleteEvent<TComplete, TEntityId>()
        where TComplete : ICompleteEvent<TEntityId>
        where TEntityId : IActorEntityId
    {
        if (typeof(TEntityId) != typeof(DatabaseBackupId))
            throw new InvalidOperationException($"{EventName}.ToCompleteEvent: unsupported entity id type {typeof(TEntityId).FullName}. Expected {typeof(DatabaseBackupId).FullName}.");

        ICompleteEvent<DatabaseBackupId> completed = new DatabaseBackupCompleteEvent
        {
            Subject = new ActorSubject(ActorType.Event, DatabaseBackupCompleteEvent.Actor, DatabaseBackupCompleteEvent.Verb, this.Subject.EntityId),
            EntityId = this.EntityId,
            Id = this.Id,
            EventId = this.EventId,
            CommandId = this.CommandId,
            AggregateId = this.AggregateId,
            EventSource = this.EventSource,
            ReceivedOn = this.ReceivedOn,
            DatabaseName = this.DatabaseName,
            BackupType = this.BackupType,
            CommandTimeout = this.CommandTimeout,
            CreatedOn = this.CreatedOn,
            CreatedBy = this.CreatedBy
        };

        return (ICompleteEvent<TEntityId>)completed;
    }

    public IErrorEvent<TEntityId> ToFailEvent<TFail, TEntityId>(Exception ex)
        where TFail : IErrorEvent<TEntityId>
        where TEntityId : IActorEntityId
    {
        if (typeof(TEntityId) != typeof(DatabaseBackupId))
            throw new InvalidOperationException($"{EventName}.ToFailEvent: unsupported entity id type {typeof(TEntityId).FullName}. Expected {typeof(DatabaseBackupId).FullName}.");

        IErrorEvent<DatabaseBackupId> failed = new DatabaseBackupFailEvent
        {
            Subject = new ActorSubject(ActorType.Event, DatabaseBackupFailEvent.Actor, DatabaseBackupFailEvent.Verb, this.Subject.EntityId),
            EntityId = this.EntityId,
            Id = this.Id,
            ErrorDate = DateTime.UtcNow,
            EventId = this.EventId,
            CommandId = this.CommandId == Guid.Empty ? Guid.NewGuid() : this.CommandId,
            EventSource = this.EventSource,
            ErrorMessage = ex.Message,
            ErrorType = ErrorType.EventService,
            ErrorCode = ErrorCode,
            ErrorData = ex.ToString(),
            ReceivedOn = this.ReceivedOn,
            AggregateId = this.AggregateId,
            DatabaseName = this.DatabaseName
        };

        return (IErrorEvent<TEntityId>)failed;
    }
}

[MessagePackObject(AllowPrivate = true)]
public record DatabaseBackupCompleteEvent : ICompleteEvent<DatabaseBackupId>
{
    [IgnoreMember] public const string Actor = "DatabaseBackupEvent";
    [IgnoreMember] public const string Verb = "BackupComplete";
    [IgnoreMember] static readonly string CachedUserName = $"{Environment.UserDomainName}\\{Environment.UserName}";

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public DatabaseBackupId EntityId { get; init; }
    [Key(2)] public Guid Id { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; }
    [Key(6)] public string EventSource { get; init; }
    [Key(7)] public DateTime ReceivedOn { get; init; }

    [Key(8)] public string DatabaseName { get; init; }
    [Key(9)] public DatabaseBackupType BackupType { get; init; }
    [Key(10)] public int CommandTimeout { get; init; }
    [Key(11)] public DateTime CreatedOn { get; init; }
    [Key(12)] public string CreatedBy { get; init; }

    [IgnoreMember] public string UserName => CachedUserName;
    [IgnoreMember] public string EventName => nameof(DatabaseBackupCompleteEvent);
    [IgnoreMember] public EventType EventType => EventType.CompletedEvent;

    public DatabaseBackupCompleteEvent() { }

    [SerializationConstructor]
    public DatabaseBackupCompleteEvent(
        ActorSubject subject,
        DatabaseBackupId entityId,
        Guid id,
        long eventId,
        Guid commandId,
        string aggregateId,
        string eventSource,
        DateTime receivedOn,
        string databaseName,
        DatabaseBackupType backupType,
        int commandTimeout,
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
        DatabaseName = databaseName ?? string.Empty;
        BackupType = backupType;
        CommandTimeout = commandTimeout;
        CreatedOn = createdOn;
        CreatedBy = createdBy ?? string.Empty;
    }
}

[MessagePackObject(AllowPrivate = true)]
public record DatabaseBackupFailEvent : IErrorEvent<DatabaseBackupId>
{
    [IgnoreMember] public const string Actor = "DatabaseBackupEvent";
    [IgnoreMember] public const string Verb = "BackupFail";
    [IgnoreMember] static readonly string CachedUserName = $"{Environment.UserDomainName}\\{Environment.UserName}";

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public DatabaseBackupId EntityId { get; init; }
    [Key(2)] public Guid Id { get; init; }
    [Key(3)] public DateTime ErrorDate { get; init; }
    [Key(4)] public long EventId { get; init; }
    [Key(5)] public Guid CommandId { get; init; }
    [Key(6)] public string EventSource { get; init; }
    [Key(7)] public string ErrorMessage { get; init; }
    [Key(8)] public int ErrorCode { get; init; }
    [Key(9)] public ErrorType ErrorType { get; init; }
    [Key(10)] public string ErrorData { get; init; }
    [Key(11)] public DateTime ReceivedOn { get; init; }
    [Key(12)] public string AggregateId { get; init; }
    [Key(13)] public string CommandName { get; init; }
    [Key(14)] public string CommandData { get; init; }
    [Key(15)] public string DatabaseName { get; init; }

    [IgnoreMember] public string EventName => nameof(DatabaseBackupFailEvent);
    [IgnoreMember] public string UserName => CachedUserName;
    [IgnoreMember] public EventType EventType => EventType.ErrorEvent;

    public DatabaseBackupFailEvent() { }

    [SerializationConstructor]
    public DatabaseBackupFailEvent(
        ActorSubject subject,
        DatabaseBackupId entityId,
        Guid id,
        DateTime errorDate,
        long eventId,
        Guid commandId,
        string eventSource,
        string errorMessage,
        int errorCode,
        ErrorType errorType,
        string errorData,
        DateTime receivedOn,
        string aggregateId,
        string commandName,
        string commandData,
        string databaseName)
    {
        Subject = subject;
        EntityId = entityId;
        Id = id;
        ErrorDate = errorDate;
        EventId = eventId;
        CommandId = commandId;
        EventSource = eventSource ?? string.Empty;
        ErrorMessage = errorMessage ?? string.Empty;
        ErrorCode = errorCode;
        ErrorType = errorType;
        ErrorData = errorData ?? string.Empty;
        ReceivedOn = receivedOn;
        AggregateId = aggregateId ?? string.Empty;
        CommandName = commandName ?? string.Empty;
        CommandData = commandData ?? string.Empty;
        DatabaseName = databaseName ?? string.Empty;
    }
}
