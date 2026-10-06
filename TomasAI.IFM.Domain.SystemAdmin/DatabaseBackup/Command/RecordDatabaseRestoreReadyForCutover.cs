using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Command.State;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Commands;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Command.Model;

namespace TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Command;

/// <summary>Handles <see cref="RecordDatabaseRestoreReadyForCutoverCommand"/> against the database backup aggregate.</summary>
public static class RecordDatabaseRestoreReadyForCutover
{
    /// <summary>Computes the business transition and applies its ordered source events.</summary>
    /// <param name="command">The concrete operator intent or service observation.</param>
    /// <param name="state">The authoritative recovery state.</param>
    /// <returns>The command acceptance or business rejection.</returns>
    public static ServiceResult<GuidResult> Execute(this RecordDatabaseRestoreReadyForCutoverCommand command, DatabaseBackupCommandState state)
    {
        var errorMsg = "DatabaseBackup.STATE.APPLY_FAILED: unable to apply RecordDatabaseRestoreReadyForCutover lifecycle events";
        var updated = command.Compute(state, out var recoveryTransition) switch
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
    internal static bool Compute(this RecordDatabaseRestoreReadyForCutoverCommand command, DatabaseBackupCommandState state, out DatabaseBackupTransition recoveryTransition)
    {
        try
        {
            recoveryTransition = state.Compute(command);
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
    internal static IReadOnlyList<TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events.DatabaseBackupEventContract> CreateLifecycleEvents(this RecordDatabaseRestoreReadyForCutoverCommand command, DatabaseBackupTransition recoveryTransition)
        => DatabaseBackupEventFactory.Create(command, recoveryTransition);
}
