using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events.Domain;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Retention.Command.State;
using static TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Model.DatabaseOperationRules;
using static TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Model.DatabaseOperationChanges;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Commands;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Model;

namespace TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Retention.Command;

/// <summary>Handles <see cref="RequestBackupRetentionEvaluationCommand"/> against the database backup aggregate.</summary>
public static class RequestBackupRetentionEvaluation
{
    /// <summary>Computes the business transition and applies its ordered source events.</summary>
    /// <param name="command">The concrete operator intent or service observation.</param>
    /// <param name="state">The authoritative recovery state.</param>
    /// <returns>The command acceptance or business rejection.</returns>
    public static ServiceResult<GuidResult> Execute(this RequestBackupRetentionEvaluationCommand command, DatabaseBackupRetentionCommandState state)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(state);
        var errorMsg = "DatabaseBackup.STATE.APPLY_FAILED: unable to apply RequestBackupRetentionEvaluation lifecycle events";
        var updated = command.Compute(state.RetentionDecision, out var recoveryTransition) switch
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
    internal static bool Compute(this RequestBackupRetentionEvaluationCommand command, DatabaseRetentionDecisionState state, out DatabaseBackupTransition recoveryTransition)
    {
        try
        {
            EnsureExpectedRevision(state.Operation, command.ExpectedStateRevision);
            if (command.Request.Origin == DatabaseRequestOrigin.ScheduledTask && state.Operation.Revision > 0 && state.Operation.OperationId == command.EntityId && state.Operation.Source == command.Source && state.Operation.ProtectionSetId == command.ProtectionSetId && state.Retention.PlanId == command.RetentionPlanId && state.Retention.PlanRevision == command.RetentionPlanRevision)
                { recoveryTransition = new([]); return true; }
            List<DatabaseBackupChange> lifecycleChanges = [];
            EnsureNewOperation(state.Operation, command.EntityId);
            var source = Source(command, state.Operation, DatabaseRecoveryOperationKind.Retention, DatabaseRecoveryPhase.Requested);
            lifecycleChanges.Add(CreateChange<DatabaseRetentionRequestedDomainEvent>(command, source));
            lifecycleChanges.Add(CreateChange<DatabaseRetentionAuthorizedDomainEvent>(command, NextSource(source, DatabaseRecoveryPhase.Authorized)));
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
    internal static IReadOnlyList<TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events.DatabaseBackupEventContract> CreateLifecycleEvents(this RequestBackupRetentionEvaluationCommand command, DatabaseBackupTransition recoveryTransition)
        => DatabaseBackupEventFactory.Create(command, recoveryTransition);
}
