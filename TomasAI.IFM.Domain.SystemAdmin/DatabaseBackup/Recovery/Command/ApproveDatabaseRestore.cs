using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events.Domain;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Recovery.Command.State;
using static TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Model.DatabaseOperationRules;
using static TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Model.DatabaseOperationChanges;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Commands;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Model;

namespace TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Recovery.Command;

/// <summary>Handles <see cref="ApproveDatabaseRestoreCommand"/> against the database backup aggregate.</summary>
public static class ApproveDatabaseRestore
{
    /// <summary>Computes the business transition and applies its ordered source events.</summary>
    /// <param name="command">The concrete operator intent or service observation.</param>
    /// <param name="state">The authoritative recovery state.</param>
    /// <returns>The command acceptance or business rejection.</returns>
    public static ServiceResult<GuidResult> Execute(this ApproveDatabaseRestoreCommand command, DatabaseRecoveryCommandState state)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(state);
        var errorMsg = "DatabaseBackup.STATE.APPLY_FAILED: unable to apply ApproveDatabaseRestore lifecycle events";
        var updated = command.Compute(state.RecoveryDecision, out var recoveryTransition) switch
        {
            _ when recoveryTransition.RejectionReason is not null
                => command.UpdateFailed(ref errorMsg, recoveryTransition.RejectionReason),
            _ => state.Update(command.CreateLifecycleEvents(recoveryTransition), command)
        };
        return updated
            ? new ServiceOk<GuidResult>(new GuidResult(command.CommandId))
            : command.UpdateFailed(errorMsg);
    }
    /// <summary>Computes immutable changes without modifying recovery state.</summary>
    /// <param name="command">The concrete recovery intent.</param>
    /// <param name="state">The state used for revision, approval, and sequence guards.</param>
    /// <param name="recoveryTransition">The ordered changes or business rejection.</param>
    /// <returns>True when all business guards accept the transition.</returns>
    internal static bool Compute(this ApproveDatabaseRestoreCommand command, DatabaseRecoveryDecisionState state, out DatabaseBackupTransition recoveryTransition)
    {
        try
        {
            EnsureExpectedRevision(state.Operation, command.ExpectedStateRevision);
            List<DatabaseBackupChange> lifecycleChanges = [];
            EnsureOperation(state.Operation, DatabaseRecoveryOperationKind.Restore, DatabaseRecoveryPhase.Requested);
            if (command.RestorePointId is not null && command.RestorePointId != state.Restore.RestorePointId || command.FreshTarget is not null && command.FreshTarget != state.Restore.FreshTarget)
                throw new InvalidOperationException("state.Restore approval cannot change the requested restore target.");
            command = command with
            {
                RestorePointId = state.Restore.RestorePointId,
                FreshTarget = state.Restore.FreshTarget,
                RestoreClass = state.Restore.RestoreClass,
                BackupSetId = state.Operation.BackupSetId,
                Source = state.Operation.Source,
                ProtectionSetId = state.Operation.ProtectionSetId,
                ExpectedPolicyRevision = state.Operation.PolicyRevision
            };
            var source = Source(command, state.Operation, state.Operation.Kind, DatabaseRecoveryPhase.Authorized);
            lifecycleChanges.Add(CreateChange<DatabaseRestoreAuthorizedDomainEvent>(command, source));
            lifecycleChanges.Add(CreateChange<DatabaseRestoreExecutionRequestedDomainEvent>(command, NextSource(source, DatabaseRecoveryPhase.Requested)));
            recoveryTransition = new(lifecycleChanges.ToArray());
            return true;
        }
        catch (Exception rejection) when (rejection is ArgumentException or InvalidOperationException)
        {
            recoveryTransition = new([], rejection.Message);
            return false;
        }
    }
    /// <summary>Creates ordered source events carrying the originating command identity.</summary>
    /// <param name="command">The originating command.</param>
    /// <param name="recoveryTransition">The accepted immutable recovery changes.</param>
    /// <returns>The source-event batch ready for state application.</returns>
    internal static IReadOnlyList<TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events.DatabaseBackupEventContract> CreateLifecycleEvents(this ApproveDatabaseRestoreCommand command, DatabaseBackupTransition recoveryTransition)
        => DatabaseBackupEventFactory.Create(command, recoveryTransition);
}
