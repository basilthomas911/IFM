using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Recovery.Command.State;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.State;

namespace TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Recovery.Command.State;
/// <summary>Immutable concern-specific decision input captured from the current operation; contains no services or other actor state.</summary>
internal sealed record DatabaseRecoveryDecisionState(DatabaseBackupOperationState Operation, DatabaseRestoreOperationState Restore);
