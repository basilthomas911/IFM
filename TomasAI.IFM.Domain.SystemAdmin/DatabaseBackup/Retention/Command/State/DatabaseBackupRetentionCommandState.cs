using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.State;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events;
namespace TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Retention.Command.State;
/// <summary>Owns the Retention operation and only its concern-specific facts.</summary>
public sealed class DatabaseBackupRetentionCommandState : DatabaseOperationCommandState<DatabaseBackupRetentionCommandState>
{
    public DatabaseRetentionState Retention { get; private set; } = new();
    internal DatabaseRetentionDecisionState RetentionDecision => new(Operation, Retention);
    internal TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Model.DatabaseServiceReconciliationDecisionState ReconciliationDecision => new(Operation, new(), Retention, OperationDecision.ProcessedServiceEvents);
    protected override void ApplyConcern(DatabaseBackupEventContract source) { Retention = Retention.Apply(source); }
}
