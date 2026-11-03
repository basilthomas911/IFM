using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.State;
using System.Text.Json;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Model;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Commands;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events.Domain;

namespace TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Model;
/// <summary>Pure identity, revision, host-sequence and lifecycle guards shared by operation concerns.</summary>
internal static class DatabaseOperationRules
{
    internal static void EnsureNewOperation(DatabaseBackupOperationState operation, DatabaseRecoveryOperationId operationId){
        if (operation.Exists && operation.OperationId != operationId)
            throw new InvalidOperationException("The aggregate already owns another operation.");
        if (operation.Exists)
            throw new InvalidOperationException("The operation was already requested.");
    }
    internal static void EnsureOperation(DatabaseBackupOperationState operation, DatabaseRecoveryOperationKind kind, DatabaseRecoveryPhase phase){
        if (!operation.Exists || operation.Kind != kind || operation.Phase != phase)
            throw new InvalidOperationException($"operation must be {kind} in {phase} phase.");
    }
    internal static void EnsureExpectedRevision(DatabaseBackupOperationState operation, long expected){
        if (expected > 0 && expected != operation.Revision)
            throw new InvalidOperationException($"Expected revision {expected} does not match current revision {operation.Revision}.");
    }
    internal static void EnsureServiceIdentityAndSequence(DatabaseBackupOperationState operation, DatabaseSourceEnvelope source){
        if (!operation.Exists)
            throw new InvalidOperationException("Service observations require an existing operation.");
        if (source.OperationId != operation.OperationId || source.Source != operation.Source || source.ProtectionSetId != operation.ProtectionSetId || source.OperationKind != operation.Kind)
            throw new InvalidOperationException("Service observation does not match the immutable operation definition.");
        if (operation.HostId is not null && source.ProducingHostId != operation.HostId)
            throw new InvalidOperationException("Producing host changed for an existing operation.");
        var expected = operation.LastServiceSequence + 1;
        if (source.SourceRevisionOrSequence != expected)
            throw new InvalidOperationException($"Service sequence gap: expected {expected}, received {source.SourceRevisionOrSequence}.");
    }
    internal static void EnsureLegalTransition(DatabaseBackupOperationState operation, DatabaseRecoveryPhase target, DatabaseRecoveryOutcome outcome){
        if (operation.IsTerminal)
            throw new InvalidOperationException("Terminal operations cannot transition.");
        if (target == DatabaseRecoveryPhase.Started && operation.Phase is not (DatabaseRecoveryPhase.Admitted or DatabaseRecoveryPhase.Preflight))
            throw new InvalidOperationException("Started requires admitted or preflight state.");
        if (target is DatabaseRecoveryPhase.Capturing or DatabaseRecoveryPhase.Transferring or DatabaseRecoveryPhase.Verifying or DatabaseRecoveryPhase.Validating && operation.Phase is not (DatabaseRecoveryPhase.Started or DatabaseRecoveryPhase.Capturing or DatabaseRecoveryPhase.Transferring or DatabaseRecoveryPhase.Verifying or DatabaseRecoveryPhase.Validating))
            throw new InvalidOperationException("Progress requires a started operation.");
        if (target == DatabaseRecoveryPhase.Completed && outcome != DatabaseRecoveryOutcome.Succeeded)
            throw new InvalidOperationException("Completion requires a successful outcome.");
        if (target == DatabaseRecoveryPhase.Completed && operation.Phase is DatabaseRecoveryPhase.Requested or DatabaseRecoveryPhase.Authorized or DatabaseRecoveryPhase.Admitted)
            throw new InvalidOperationException("An operation cannot complete before it starts.");
        if (target == DatabaseRecoveryPhase.ReadyForCutover && (operation.Kind != DatabaseRecoveryOperationKind.Restore || operation.Phase != DatabaseRecoveryPhase.Validating))
            throw new InvalidOperationException("Only production restore can become ready for cutover.");
    }
}
