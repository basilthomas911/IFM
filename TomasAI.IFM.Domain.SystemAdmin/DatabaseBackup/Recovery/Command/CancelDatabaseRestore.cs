using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events.Domain;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Recovery.Command.State;
using static TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Model.DatabaseOperationRules;
using static TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Model.DatabaseOperationChanges;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Commands;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Model;

namespace TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Recovery.Command;

/// <summary>Handles <see cref="CancelDatabaseRestoreCommand"/> against the database backup aggregate.</summary>
public static class CancelDatabaseRestore
{

    /// <param name="command">
    /// The concrete operator intent or service observation.
    /// The concrete recovery intent.
    /// The originating command.
    /// </param>
    extension(CancelDatabaseRestoreCommand command)
    {
        /// <summary>Computes the business transition and applies its ordered source events.</summary>
        ///
        /// <param name="state">The authoritative recovery state.</param>
        /// <returns>The command acceptance or business rejection.</returns>
        public ServiceResult<GuidResult> Execute(DatabaseRecoveryCommandState state)
        {
            ArgumentNullException.ThrowIfNull(command);
            ArgumentNullException.ThrowIfNull(state);
            var errorMsg = "DatabaseBackup.STATE.APPLY_FAILED: unable to apply CancelDatabaseRestore lifecycle events";
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
                if (!state.Operation.Exists || state.Operation.IsTerminal)
                    throw new InvalidOperationException("Only an active operation can be cancelled.");
                var source = Source(command, state.Operation, state.Operation.Kind, DatabaseRecoveryPhase.Cancelled);
                lifecycleChanges.Add(CreateChange<DatabaseOperationCancelledEvent>(command, source, DatabaseRecoveryOutcome.Cancelled));
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
