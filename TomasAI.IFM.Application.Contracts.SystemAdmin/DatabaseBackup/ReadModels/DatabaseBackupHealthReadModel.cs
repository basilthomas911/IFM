using MessagePack;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;

namespace TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.ReadModels;

[MessagePackObject]
public sealed record DatabaseBackupHealthReadModel
{
    [Key(0)] public BackupSource Source { get; init; }
    [Key(1)] public DatabaseBackupHostId HostId { get; init; }
    [Key(2)] public DatabaseServiceCapabilityState CapabilityState { get; init; }
    [Key(3)] public bool Ready { get; init; }
    [Key(4)] public long LastServiceSequence { get; init; }
    [Key(5)] public DateTimeOffset ObservedUtc { get; init; }
    [Key(6)] public string SafeDiagnosticReference { get; init; } = string.Empty;
}
