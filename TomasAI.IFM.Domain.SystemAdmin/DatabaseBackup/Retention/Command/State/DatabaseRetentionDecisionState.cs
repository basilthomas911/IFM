using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Retention.Command.State;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.State;

namespace TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Retention.Command.State;
/// <summary>Immutable concern-specific decision input captured from the current operation; contains no services or other actor state.</summary>
internal sealed record DatabaseRetentionDecisionState(DatabaseBackupOperationState Operation, DatabaseRetentionState Retention);
