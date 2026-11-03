using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.State;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Recovery.Command.State;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Retention.Command.State;
namespace TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Model;
/// <summary>Operation-local journal-repair evidence; no global policy, artifact or service state is loaded.</summary>
internal sealed record DatabaseServiceReconciliationDecisionState(DatabaseBackupOperationState Operation,
    DatabaseRestoreOperationState Restore, DatabaseRetentionState Retention, IReadOnlyDictionary<Guid, string> ProcessedServiceEvents);
