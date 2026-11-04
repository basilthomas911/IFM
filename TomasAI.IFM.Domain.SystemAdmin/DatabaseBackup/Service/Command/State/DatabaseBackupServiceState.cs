using MessagePack;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events.Domain;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;

namespace TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Service.Command.State;

[MessagePackObject]
public sealed record DatabaseBackupServiceState
{
    [Key(0)]
    public BackupSource Source { get; init; }
    [Key(1)]
    public DatabaseBackupHostId? HostId { get; init; }
    [Key(2)]
    public DatabaseServiceCapabilityState CapabilityState { get; init; }
    [Key(3)]
    public long LastServiceSequence { get; init; }
    [Key(4)]
    public bool Reconciled { get; init; }
    /// <summary>Applies this concern's accepted persisted facts without changing another concern.</summary>
    internal DatabaseBackupServiceState Apply(DatabaseBackupEventContract e)
    {
        return e.Source.ProducingHostId is null ? this : this with
        {
            Source = e.Source.Source, HostId = e.Source.ProducingHostId, LastServiceSequence = e.Source.SourceRevisionOrSequence,
            CapabilityState = e.CapabilityState == DatabaseServiceCapabilityState.None ? CapabilityState : e.CapabilityState,
            Reconciled = e is DatabaseBackupServiceReconciledEvent || Reconciled
        };
    }
}
