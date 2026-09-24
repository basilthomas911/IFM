using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Event.Actor;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Event.Translation;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Commands;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Event.Model;

/// <summary>Translates a broker-service event into its durable internal command.</summary>
internal static class DatabaseBackupServiceEventHandler
{
    /// <summary>Translates, requests, and verifies the internal Database Backup command.</summary>
    internal static async ValueTask ExecuteAsync(DatabaseBackupServiceEventContract eventValue,
        IEventActorContext<DatabaseBackupEventActor> context, ILogger<DatabaseBackupEventActor> logger)
    {
        try
        {
            var command = DatabaseBackupEventTranslator.Translate(eventValue);
            var result = await RequestAsync(context, command).ConfigureAwait(false);
            if (!result.Success)
                throw new InvalidOperationException($"DatabaseBackup command rejected: {result.ErrorMessage}");
        }
        catch (Exception exception)
        {
            logger.LogError(exception,
                "Database backup service event handoff failed for {EventType}, {EventId}, {CommandId}, {EntityId}.",
                eventValue.GetType().Name, eventValue.Id, eventValue.CommandId, eventValue.EntityId);
            exception.Data[DatabaseBackupEventActor.HandlerErrorLoggedKey] = true;
            throw;
        }
    }

    /// <summary>Sends the concrete translated internal command through the actor request API.</summary>
    static ValueTask<ServiceResult<GuidResult>> RequestAsync(IEventActorContext<DatabaseBackupEventActor> context, DatabaseBackupInternalCommand command)
        => command switch
        {
            RecordDatabaseOperationAdmissionCommand value => context.RequestAsync<RecordDatabaseOperationAdmissionCommand, Shared.DatabaseBackup.Contracts.DatabaseRecoveryOperationId>(value),
            RecordDatabaseOperationStartedCommand value => context.RequestAsync<RecordDatabaseOperationStartedCommand, Shared.DatabaseBackup.Contracts.DatabaseRecoveryOperationId>(value),
            RecordDatabaseOperationProgressCommand value => context.RequestAsync<RecordDatabaseOperationProgressCommand, Shared.DatabaseBackup.Contracts.DatabaseRecoveryOperationId>(value),
            RecordDatabaseBackupBoundaryCommand value => context.RequestAsync<RecordDatabaseBackupBoundaryCommand, Shared.DatabaseBackup.Contracts.DatabaseRecoveryOperationId>(value),
            RecordDatabaseArtifactReplicaCommand value => context.RequestAsync<RecordDatabaseArtifactReplicaCommand, Shared.DatabaseBackup.Contracts.DatabaseRecoveryOperationId>(value),
            RecordDatabaseOperationVerificationCommand value => context.RequestAsync<RecordDatabaseOperationVerificationCommand, Shared.DatabaseBackup.Contracts.DatabaseRecoveryOperationId>(value),
            RecordDatabaseOperationErrorCommand value => context.RequestAsync<RecordDatabaseOperationErrorCommand, Shared.DatabaseBackup.Contracts.DatabaseRecoveryOperationId>(value),
            RecordDatabaseRestoreReadyForCutoverCommand value => context.RequestAsync<RecordDatabaseRestoreReadyForCutoverCommand, Shared.DatabaseBackup.Contracts.DatabaseRecoveryOperationId>(value),
            CompleteDatabaseOperationCommand value => context.RequestAsync<CompleteDatabaseOperationCommand, Shared.DatabaseBackup.Contracts.DatabaseRecoveryOperationId>(value),
            FailDatabaseOperationCommand value => context.RequestAsync<FailDatabaseOperationCommand, Shared.DatabaseBackup.Contracts.DatabaseRecoveryOperationId>(value),
            RecordDatabaseOperationCancelledCommand value => context.RequestAsync<RecordDatabaseOperationCancelledCommand, Shared.DatabaseBackup.Contracts.DatabaseRecoveryOperationId>(value),
            RecordDatabaseBackupPolicyStatusCommand value => context.RequestAsync<RecordDatabaseBackupPolicyStatusCommand, Shared.DatabaseBackup.Contracts.DatabaseRecoveryOperationId>(value),
            RecordDatabaseRetentionResultCommand value => context.RequestAsync<RecordDatabaseRetentionResultCommand, Shared.DatabaseBackup.Contracts.DatabaseRecoveryOperationId>(value),
            ReconcileDatabaseBackupServiceStateCommand value => context.RequestAsync<ReconcileDatabaseBackupServiceStateCommand, Shared.DatabaseBackup.Contracts.DatabaseRecoveryOperationId>(value),
            RecordDatabaseBackupServiceCapabilityCommand value => context.RequestAsync<RecordDatabaseBackupServiceCapabilityCommand, Shared.DatabaseBackup.Contracts.DatabaseRecoveryOperationId>(value),
            RecordDatabaseRecoveryRunStatisticsCommand value => context.RequestAsync<RecordDatabaseRecoveryRunStatisticsCommand, Shared.DatabaseBackup.Contracts.DatabaseRecoveryOperationId>(value),
            _ => throw new InvalidOperationException($"Unsupported translated command '{command.GetType().Name}'.")
        };
}
