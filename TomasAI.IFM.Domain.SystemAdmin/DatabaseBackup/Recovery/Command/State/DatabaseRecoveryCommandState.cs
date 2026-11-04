using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.State;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events;
namespace TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Recovery.Command.State;
/// <summary>Owns the Recovery operation and only its concern-specific facts.</summary>
public sealed class DatabaseRecoveryCommandState : DatabaseOperationCommandState<DatabaseRecoveryCommandState>
{
    public DatabaseRestoreOperationState Restore { get; private set; } = new();
    internal DatabaseRecoveryDecisionState RecoveryDecision => new(Operation, Restore);
    internal TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Model.DatabaseServiceReconciliationDecisionState ReconciliationDecision => new(Operation, Restore, new(), OperationDecision.ProcessedServiceEvents);
    protected override DatabaseOperationStateSnapshottedEvent CaptureConcern(DatabaseOperationStateSnapshottedEvent snapshot)
        => snapshot with { Restore = Restore };

    protected override bool HasConcernSnapshot(DatabaseOperationStateSnapshottedEvent snapshot) => snapshot.Restore is not null;

    protected override bool RestoreConcern(DatabaseOperationStateSnapshottedEvent snapshot)
    {
        if (snapshot.Restore is not { } value)
            return false;
        Restore = value;
        return true;
    }

    protected override void ApplyConcern(DatabaseBackupEventContract source) { Restore = Restore.Apply(source); }
}
