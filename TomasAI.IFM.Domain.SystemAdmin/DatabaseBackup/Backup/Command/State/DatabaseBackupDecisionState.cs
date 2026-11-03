using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.State;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Backup.Command.State;

namespace TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Backup.Command.State;
/// <summary>Immutable concern-specific decision input captured from the current operation; contains no services or other actor state.</summary>
internal sealed record DatabaseBackupDecisionState(DatabaseBackupOperationState Operation);
