using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;

namespace TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Command.State;

public sealed record DatabaseBackupServiceState
{
    public BackupSource Source { get; init; }
    public DatabaseBackupHostId? HostId { get; init; }
    public DatabaseServiceCapabilityState CapabilityState { get; init; }
    public long LastServiceSequence { get; init; }
    public bool Reconciled { get; init; }
}
