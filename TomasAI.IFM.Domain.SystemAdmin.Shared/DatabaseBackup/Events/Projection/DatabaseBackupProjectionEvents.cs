using MessagePack;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events.Projection;

[MessagePackObject]
public sealed record DatabaseBackupProjectionCompletedEvent : ICompleteEvent<DatabaseRecoveryOperationId>
{
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public DatabaseRecoveryOperationId EntityId { get; init; }
    [Key(2)] public Guid Id { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; } = string.Empty;
    [Key(6)] public string EventSource { get; init; } = string.Empty;
    [Key(7)] public DateTime ReceivedOn { get; init; }
    [IgnoreMember] public string UserName => "DatabaseBackup";
    [IgnoreMember] public string EventName => nameof(DatabaseBackupProjectionCompletedEvent);
    [IgnoreMember] public EventType EventType => EventType.CompletedEvent;
}

[MessagePackObject]
public sealed record DatabaseBackupProjectionFailedEvent : IErrorEvent<DatabaseRecoveryOperationId>
{
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public DatabaseRecoveryOperationId EntityId { get; init; }
    [Key(2)] public Guid Id { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; } = string.Empty;
    [Key(6)] public string EventSource { get; init; } = string.Empty;
    [Key(7)] public DateTime ReceivedOn { get; init; }
    [Key(8)] public DateTime ErrorDate { get; init; }
    [Key(9)] public int ErrorCode { get; init; } = 9290;
    [Key(10)] public string ErrorMessage { get; init; } = string.Empty;
    [Key(11)] public ErrorType ErrorType { get; init; } = ErrorType.Storage;
    [Key(12)] public string ErrorData { get; init; } = string.Empty;
    [Key(13)] public string CommandName { get; init; } = string.Empty;
    [Key(14)] public string CommandData { get; init; } = string.Empty;
    [IgnoreMember] public string UserName => "DatabaseBackup";
    [IgnoreMember] public string EventName => nameof(DatabaseBackupProjectionFailedEvent);
    [IgnoreMember] public EventType EventType => EventType.ErrorEvent;
}
