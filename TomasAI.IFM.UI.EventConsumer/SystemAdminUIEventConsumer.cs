using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.SystemAdmin.Shared.Events;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events.Domain;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.UI.EventConsumer;

public class SystemAdminUIEventConsumer(INatsEventListenerOptions options, ILogger logger)
        : NatsActorEventListener(options, logger), ISystemAdminUIEventConsumer
{
    readonly static string EventConsumer = "SystemAdminUIEventConsumer";
    readonly ILogger _logger = logger;
    readonly Dictionary<ActorMailboxId, List<string>> _eventMap = new()
    {
        [new ActorMailboxId(ActorType.Event, DatabaseBackupEvent.Actor)] = [
                    DatabaseBackupEvent.Verb,
                    DatabaseBackupInfoMessageEvent.Verb,
                    DatabaseBackupCompleteEvent.Verb,
                    DatabaseBackupFailEvent.Verb
                ]
    };

    readonly Dictionary<ActorMailboxId, List<string>> _databaseBackupEventMap = new()
    {
        [new ActorMailboxId(ActorType.Event, "DatabaseBackupEvent")] =
        [
            "BackupRequested", "OperationStarted", "ProgressRecorded", "VerificationRecorded",
            "ErrorRecorded", "OperationCompleted", "OperationFailed", "OperationCancelled",
            "BackupSetCompleted", "PolicyRevised", "ServiceCapabilityRecorded", "ServiceReconciled"
        ]
    };

    public async ValueTask StartAsync(
        Action<DatabaseBackupEvent> backupAction,
        Action<DatabaseBackupInfoMessageEvent> infoMsgAction,
        Action<DatabaseBackupCompleteEvent> completedAction, 
        Action<DatabaseBackupFailEvent> failedAction)
    {
        await StartAsync(EventConsumer,  _eventMap, EventHandlerAsync);

        async ValueTask EventHandlerAsync(string eventVerb, NatsMsg<byte[]> eventMsg)
        {
            try
            {
                _ = eventVerb switch
                {
                    _ when eventVerb == DatabaseBackupEvent.Verb 
                        => HandleEvent(eventMsg.AsEvent<DatabaseBackupEvent>()!, e => backupAction?.Invoke((DatabaseBackupEvent)e)),
                    _ when eventVerb == DatabaseBackupInfoMessageEvent.Verb 
                        => HandleEvent(eventMsg.AsEvent<DatabaseBackupInfoMessageEvent>()!, e => infoMsgAction?.Invoke((DatabaseBackupInfoMessageEvent)e)),
                    _ when eventVerb == DatabaseBackupCompleteEvent.Verb 
                        => HandleEvent(eventMsg.AsEvent<DatabaseBackupCompleteEvent>()!, e => completedAction?.Invoke((DatabaseBackupCompleteEvent)e)),
                    _ when eventVerb == DatabaseBackupFailEvent.Verb 
                        => HandleEvent(eventMsg.AsEvent<DatabaseBackupFailEvent>()!, e => failedAction?.Invoke((DatabaseBackupFailEvent)e)),
                    _ => default!
                };
                await ValueTask.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogErrorEvent(EventConsumer, ex, "EventHandlerAsync: failed while processing event verb: {EventVerb}", eventVerb);
            }

            IEvent HandleEvent(IEvent e, Action<IEvent> eventAction)
            {
                eventAction?.Invoke(e);
                return e;
            }
        }
    }

    /// <inheritdoc />
    public async ValueTask StartDatabaseBackupAsync(
        Func<DatabaseBackupEventContract, ValueTask> eventAction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventAction);
        cancellationToken.ThrowIfCancellationRequested();
        await StartAsync(EventConsumer, _databaseBackupEventMap, EventHandlerAsync).ConfigureAwait(false);

        async ValueTask EventHandlerAsync(string eventVerb, NatsMsg<byte[]> eventMsg)
        {
            try
            {
                DatabaseBackupEventContract? domainEvent = eventVerb switch
                {
                    "BackupRequested" => eventMsg.AsEvent<DatabaseBackupRequestedDomainEvent>(),
                    "OperationStarted" => eventMsg.AsEvent<DatabaseOperationStartedEvent>(),
                    "ProgressRecorded" => eventMsg.AsEvent<DatabaseOperationProgressRecordedEvent>(),
                    "VerificationRecorded" => eventMsg.AsEvent<DatabaseOperationVerificationRecordedEvent>(),
                    "ErrorRecorded" => eventMsg.AsEvent<DatabaseOperationErrorRecordedEvent>(),
                    "OperationCompleted" => eventMsg.AsEvent<DatabaseOperationCompletedEvent>(),
                    "OperationFailed" => eventMsg.AsEvent<DatabaseOperationFailedEvent>(),
                    "OperationCancelled" => eventMsg.AsEvent<DatabaseOperationCancelledEvent>(),
                    "BackupSetCompleted" => eventMsg.AsEvent<DatabaseBackupSetCompletedEvent>(),
                    "PolicyRevised" => eventMsg.AsEvent<DatabaseBackupPolicyRevisedEvent>(),
                    "ServiceCapabilityRecorded" => eventMsg.AsEvent<DatabaseBackupServiceCapabilityRecordedEvent>(),
                    "ServiceReconciled" => eventMsg.AsEvent<DatabaseBackupServiceReconciledEvent>(),
                    _ => null
                };
                if (domainEvent is not null)
                    await eventAction(domainEvent).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                _logger.LogErrorEvent(
                    EventConsumer,
                    exception,
                    "DatabaseBackup event handling failed for verb {EventVerb}.",
                    eventVerb);
            }
        }
    }
}
