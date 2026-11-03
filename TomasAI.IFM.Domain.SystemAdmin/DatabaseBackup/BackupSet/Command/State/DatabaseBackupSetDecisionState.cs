using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.State;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.BackupSet.Command.State;

namespace TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.BackupSet.Command.State;
/// <summary>Immutable concern-specific decision input captured from the current operation; contains no services or other actor state.</summary>
internal sealed record DatabaseBackupSetDecisionState(DatabaseBackupOperationState Operation, DatabaseBackupSetState BackupSet);
