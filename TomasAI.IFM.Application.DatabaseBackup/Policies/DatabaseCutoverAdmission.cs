using TomasAI.IFM.Application.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events.Execution;

namespace TomasAI.IFM.Application.DatabaseBackup.Policies;

/// <summary>Binds cutover to a validated restore rather than caller-supplied replacement data.</summary>
public static class DatabaseCutoverAdmission
{
    public static void Validate(
        DatabaseBackupEventContract originalRestore,
        DatabaseBackupEventContract cutover,
        DatabaseRecoveryPhase phase,
        bool terminal,
        long validatedRevision)
    {
        if (originalRestore is not DatabaseRestoreExecutionRequestedEvent
            || cutover is not DatabaseCutoverExecutionRequestedEvent
            || originalRestore.Source.OperationKind != DatabaseRecoveryOperationKind.Restore
            || cutover.Source.OperationKind != DatabaseRecoveryOperationKind.Cutover
            || phase != DatabaseRecoveryPhase.ReadyForCutover || !terminal
            || validatedRevision <= 0 || cutover.ValidationRevision != validatedRevision
            || originalRestore.Source.OperationId != cutover.Source.OperationId
            || originalRestore.Source.Source != cutover.Source.Source
            || originalRestore.Source.ProtectionSetId != cutover.Source.ProtectionSetId
            || originalRestore.Source.BackupSetId != cutover.Source.BackupSetId
            || originalRestore.Source.PolicyRevision != cutover.Source.PolicyRevision
            || originalRestore.RestorePointId is null || originalRestore.FreshTarget is null
            || originalRestore.RestorePointId != cutover.RestorePointId
            || originalRestore.FreshTarget != cutover.FreshTarget)
            throw new DatabaseExecutionConflictException(
                "Cutover must identify the completed, revision-bound fresh-target restore.");
    }
    /// <summary>Reapproval preserves the previously validated cutover definition; the controller still requires a reconciled prepared record.</summary>
    public static void ValidateRetry(DatabaseBackupEventContract prior, DatabaseBackupEventContract next,
        DatabaseRecoveryPhase phase, bool terminal)
    {
        if (prior is not DatabaseCutoverExecutionRequestedEvent || next is not DatabaseCutoverExecutionRequestedEvent
            || phase != DatabaseRecoveryPhase.Failed || !terminal || prior.ValidationRevision <= 0
            || prior.ValidationRevision != next.ValidationRevision
            || prior.Source.OperationId != next.Source.OperationId || prior.Source.Source != next.Source.Source
            || prior.Source.ProtectionSetId != next.Source.ProtectionSetId || prior.Source.BackupSetId != next.Source.BackupSetId
            || prior.Source.PolicyRevision != next.Source.PolicyRevision
            || prior.RestorePointId is null || prior.FreshTarget is null
            || prior.RestorePointId != next.RestorePointId || prior.FreshTarget != next.FreshTarget)
            throw new DatabaseExecutionConflictException("Cutover reapproval must preserve the previously validated restore identity and revision.");
    }}
