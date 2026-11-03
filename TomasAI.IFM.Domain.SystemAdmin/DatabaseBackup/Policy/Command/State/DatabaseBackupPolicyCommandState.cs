using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.State;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events;
namespace TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Policy.Command.State;
/// <summary>Owns the Policy operation and only its concern-specific facts.</summary>
public sealed class DatabaseBackupPolicyCommandState : DatabaseOperationCommandState<DatabaseBackupPolicyCommandState>
{
    public DatabaseBackupPolicyState Policy { get; private set; } = new();
    internal DatabasePolicyDecisionState PolicyDecision => new(Operation, Policy);

    internal TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Model.DatabaseServiceReconciliationDecisionState ReconciliationDecision => new(Operation, new(), new(), OperationDecision.ProcessedServiceEvents);
    protected override void ApplyConcern(DatabaseBackupEventContract source) { Policy = Policy.Apply(source); }
}
