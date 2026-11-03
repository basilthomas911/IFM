using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Policy.Command.State;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.State;

namespace TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Policy.Command.State;
/// <summary>Immutable concern-specific decision input captured from the current operation; contains no services or other actor state.</summary>
internal sealed record DatabasePolicyDecisionState(DatabaseBackupOperationState Operation, DatabaseBackupPolicyState Policy);
