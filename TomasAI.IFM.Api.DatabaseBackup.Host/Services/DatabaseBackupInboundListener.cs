using Microsoft.Extensions.Hosting;
using NATS.Client.Core;
using TomasAI.IFM.Application.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events.Execution;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.LocalWorkstation.Processing;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Api.DatabaseBackup.Host.Services;

public sealed class DatabaseBackupInboundListener(
    IJSActorEventListener listener,
    IDatabaseRecoveryProcessorRegistry processors,
    DatabaseBackupHostHealthState health) : IHostedService
{
    static readonly ActorMailboxId Mailbox = new(ActorType.Event, "DatabaseBackupExecution");
    static readonly string[] Verbs =
    [
        "BackupExecutionRequested", "BackupCancellationRequested", "BackupVerificationRequested",
        "RestoreExecutionRequested", "RestoreCancellationRequested", "RestoreDrillRequested",
        "CutoverExecutionRequested", "RetentionEvaluationRequested", "RetentionExecutionRequested",
        "BackupPolicyActivated", "BackupReconciliationRequested"
    ];

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await listener.StartAsync(
            "database-backup-host",
            new Dictionary<ActorMailboxId, List<string>> { [Mailbox] = [.. Verbs] },
            AdmitAsync).ConfigureAwait(false);
        health.MarkReady();
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        health.MarkNotReady();
        await listener.StopAsync().ConfigureAwait(false);
    }

    public async ValueTask AdmitAsync(string verb, NatsMsg<byte[]> message)
    {
        var executionEvent = Deserialize(verb, message)
            ?? throw new InvalidOperationException($"Unable to deserialize DatabaseBackup execution event '{verb}'.");
        executionEvent.Validate();
        var processor = processors.GetRequired(executionEvent.Source.Source);
        if (processor is LocalWorkstationDatabaseRecoveryProcessor localProcessor
            && !localProcessor.CanProcess(executionEvent.Source.ProtectionSetId))
            return;
        await processor.AdmitAsync(
            new DatabaseExecutionIntent { ExecutionEvent = executionEvent },
            CancellationToken.None).ConfigureAwait(false);
    }

    static DatabaseBackupEventContract? Deserialize(string verb, NatsMsg<byte[]> message)
        => verb switch
        {
            "BackupExecutionRequested" => message.AsEvent<DatabaseBackupExecutionRequestedEvent>(),
            "BackupCancellationRequested" => message.AsEvent<DatabaseBackupCancellationRequestedEvent>(),
            "BackupVerificationRequested" => message.AsEvent<DatabaseBackupVerificationRequestedEvent>(),
            "RestoreExecutionRequested" => message.AsEvent<DatabaseRestoreExecutionRequestedEvent>(),
            "RestoreCancellationRequested" => message.AsEvent<DatabaseRestoreCancellationRequestedEvent>(),
            "RestoreDrillRequested" => message.AsEvent<DatabaseRestoreDrillRequestedEvent>(),
            "CutoverExecutionRequested" => message.AsEvent<DatabaseCutoverExecutionRequestedEvent>(),
            "RetentionEvaluationRequested" => message.AsEvent<DatabaseRetentionEvaluationRequestedEvent>(),
            "RetentionExecutionRequested" => message.AsEvent<DatabaseRetentionExecutionRequestedEvent>(),
            "BackupPolicyActivated" => message.AsEvent<DatabaseBackupPolicyActivatedEvent>(),
            "BackupReconciliationRequested" => message.AsEvent<DatabaseBackupReconciliationRequestedEvent>(),
            _ => throw new InvalidOperationException($"Unsupported DatabaseBackup execution verb '{verb}'.")
        };
}
