using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Framework.Messaging.Nats;
using TomasAI.IFM.Framework.Messaging.Nats.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.SystemAdmin.Events;
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
}
