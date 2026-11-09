using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.State;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events;
namespace TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Retention.Command.State;
/// <summary>Owns the Retention operation and only its concern-specific facts.</summary>
public sealed class DatabaseBackupRetentionCommandState : DatabaseOperationCommandState<DatabaseBackupRetentionCommandState>
{
    public DatabaseRetentionState Retention { get; private set; } = new();
    internal DatabaseRetentionDecisionState RetentionDecision => new(Operation, Retention);
    internal TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Model.DatabaseServiceReconciliationDecisionState ReconciliationDecision => new(Operation, new(), Retention, OperationDecision.ProcessedServiceEvents);
    protected override DatabaseOperationStateSnapshottedEvent CaptureConcern(DatabaseOperationStateSnapshottedEvent snapshot)
        => global::TomasAI.IFM.Shared.EventModelActor.MessageOrigins.Copy(snapshot, snapshot with { Retention = Retention });

    protected override bool HasConcernSnapshot(DatabaseOperationStateSnapshottedEvent snapshot) => snapshot.Retention is not null;

    protected override bool RestoreConcern(DatabaseOperationStateSnapshottedEvent snapshot)
    {
        if (snapshot.Retention is not { } value)
            return false;
        Retention = value;
        return true;
    }

    protected override void ApplyConcern(DatabaseBackupEventContract source) { Retention = Retention.Apply(source); }
}
