using MessagePack;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events.Domain;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;

namespace TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Recovery.Command.State;

[MessagePackObject]
public sealed record DatabaseRestoreOperationState
{
    [Key(0)]
    public DatabaseRestorePointId? RestorePointId { get; init; }
    [Key(1)]
    public DatabaseRestoreClass RestoreClass { get; init; }
    [Key(2)]
    public string FreshTargetProfile { get; init; } = string.Empty;
    [Key(3)]
    public DatabaseFreshTargetDescriptor? FreshTarget { get; init; }
    [Key(4)]
    public bool Approved { get; init; }
    [Key(5)]
    public DatabaseCutoverState CutoverState { get; init; }
    /// <summary>Applies this concern's accepted persisted facts without changing another concern.</summary>
    internal DatabaseRestoreOperationState Apply(DatabaseBackupEventContract e)
    {
        var source = e.Source;
        return e.RestorePointId is not null || source.OperationKind is DatabaseRecoveryOperationKind.Restore or DatabaseRecoveryOperationKind.RestoreDrill
            ? this with { RestorePointId = e.RestorePointId ?? RestorePointId, FreshTarget = e.FreshTarget ?? FreshTarget,
                RestoreClass = e.RestoreClass == default ? RestoreClass : e.RestoreClass,
                CutoverState = e.CutoverState == DatabaseCutoverState.None ? CutoverState : e.CutoverState }
            : this;
    }
}
