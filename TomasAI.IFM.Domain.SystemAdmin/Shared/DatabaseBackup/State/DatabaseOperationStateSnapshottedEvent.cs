using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Service.Command.State;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Recovery.Command.State;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Policy.Command.State;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.BackupSet.Command.State;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Retention.Command.State;
using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.State;

/// <summary>Captures the full command state at a committed stream boundary.</summary>
[MessagePackObject]
public sealed record DatabaseOperationStateSnapshottedEvent : IEvent
{
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public Guid Id { get; init; }
    [Key(2)] public long EventId { get; init; }
    [Key(3)] public Guid CommandId { get; init; }
    [Key(4)] public string AggregateId { get; init; } = string.Empty;
    [Key(5)] public string EventSource { get; init; } = string.Empty;
    [Key(6)] public DateTime ReceivedOn { get; init; }
    [Key(7)] public string Concern { get; init; } = string.Empty;
    [Key(8)] public long LegacyStreamVersion { get; init; }
    [Key(9)] public DatabaseBackupOperationState? Operation { get; init; }
    [Key(10)] public IReadOnlyDictionary<Guid, string>? ProcessedServiceEvents { get; init; }
    [Key(11)] public DatabaseBackupServiceState? Service { get; init; }
    [Key(12)] public DatabaseRestoreOperationState? Restore { get; init; }
    [Key(13)] public DatabaseBackupPolicyState? Policy { get; init; }
    [Key(14)] public DatabaseBackupSetState? BackupSet { get; init; }
    [Key(15)] public DatabaseRetentionState? Retention { get; init; }
    [IgnoreMember] public string UserName => string.Empty;
    [IgnoreMember] public string EventName => nameof(DatabaseOperationStateSnapshottedEvent);
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;
}
