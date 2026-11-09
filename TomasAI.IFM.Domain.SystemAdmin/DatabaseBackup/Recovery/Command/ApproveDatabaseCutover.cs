using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events.Domain;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Recovery.Command.State;
using static TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Model.DatabaseOperationRules;
using static TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Model.DatabaseOperationChanges;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Commands;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Model;

namespace TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Recovery.Command;

/// <summary>Handles <see cref="ApproveDatabaseCutoverCommand"/> against the database backup aggregate.</summary>
public static class ApproveDatabaseCutover
{

    /// <param name="command">
    /// The concrete operator intent or service observation.
    /// The concrete recovery intent.
    /// The originating command.
    /// </param>
    extension(ApproveDatabaseCutoverCommand command)
    {
        /// <summary>Computes the business transition and applies its ordered source events.</summary>
        ///
        /// <param name="state">The authoritative recovery state.</param>
        /// <returns>The command acceptance or business rejection.</returns>
        public ServiceResult<GuidResult> Execute(DatabaseRecoveryCommandState state)
        {
            ArgumentNullException.ThrowIfNull(command);
            ArgumentNullException.ThrowIfNull(state);
            var errorMsg = "DatabaseBackup.STATE.APPLY_FAILED: unable to apply ApproveDatabaseCutover lifecycle events";
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
        ///
        /// <param name="state">The state used for revision, approval, and sequence guards.</param>
        /// <param name="recoveryTransition">The ordered changes or business rejection.</param>
        /// <returns>True when all business guards accept the transition.</returns>
        internal bool Compute(DatabaseRecoveryDecisionState state, out DatabaseBackupTransition recoveryTransition)
        {
            try
            {
                EnsureExpectedRevision(state.Operation, command.ExpectedStateRevision);
                List<DatabaseBackupChange> lifecycleChanges = [];
                if (state.Operation.Kind != DatabaseRecoveryOperationKind.Cutover || state.Operation.Phase != DatabaseRecoveryPhase.Failed)
                    EnsureOperation(state.Operation, DatabaseRecoveryOperationKind.Restore, DatabaseRecoveryPhase.ReadyForCutover);
                if (command.ValidationRevision != state.Operation.ValidationRevision)
                    throw new InvalidOperationException("Cutover approval does not match the current validation revision.");
                if (command.RestorePointId is not null && command.RestorePointId != state.Restore.RestorePointId || command.FreshTarget is not null && command.FreshTarget != state.Restore.FreshTarget)
                    throw new InvalidOperationException("Cutover cannot replace the validated restore target.");
                command = global::TomasAI.IFM.Shared.EventModelActor.MessageOrigins.Copy(command, command with
                {
                    RestorePointId = state.Restore.RestorePointId,
                    FreshTarget = state.Restore.FreshTarget,
                    RestoreClass = state.Restore.RestoreClass,
                    BackupSetId = state.Operation.BackupSetId,
                    Source = state.Operation.Source,
                    ProtectionSetId = state.Operation.ProtectionSetId,
                    ExpectedPolicyRevision = state.Operation.PolicyRevision
                });
                var source = Source(command, state.Operation, DatabaseRecoveryOperationKind.Cutover, DatabaseRecoveryPhase.Authorized);
                lifecycleChanges.Add(CreateChange<DatabaseCutoverRequestedDomainEvent>(command, source));
                lifecycleChanges.Add(CreateChange<DatabaseCutoverAuthorizedDomainEvent>(command, NextSource(source, DatabaseRecoveryPhase.Authorized)));
                lifecycleChanges.Add(CreateChange<DatabaseCutoverExecutionRequestedDomainEvent>(command, NextSource(source, DatabaseRecoveryPhase.CuttingOver)));
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
        ///
        /// <param name="recoveryTransition">The accepted immutable recovery changes.</param>
        /// <returns>The source-event batch ready for state application.</returns>
        internal IReadOnlyList<TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events.DatabaseBackupEventContract> CreateLifecycleEvents(DatabaseBackupTransition recoveryTransition)
            => DatabaseBackupEventFactory.Create(command, recoveryTransition);
    }
}
