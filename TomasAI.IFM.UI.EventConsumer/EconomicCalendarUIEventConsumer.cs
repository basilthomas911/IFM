using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Contracts;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Reference.Events;

namespace TomasAI.IFM.UI.EventConsumer;

public class EconomicCalendarUIEventConsumer(INatsEventListenerOptions options, ILogger logger)
    : NatsActorEventListener(options, logger), IEconomicCalendarUIEventConsumer
{
    readonly static string EventConsumer = "EconomicCalendarUIEventConsumer";
    readonly ILogger _logger = logger;
    readonly Dictionary<ActorMailboxId, List<string>> _eventMap = new()
    {
        [new ActorMailboxId(ActorType.Event, EconomicCalendarAddedCompleteEvent.Actor)]
            = [EconomicCalendarAddedCompleteEvent.Verb,
                EconomicCalendarChangedCompleteEvent.Verb,
                EconomicCalendarRemovedCompleteEvent.Verb,
                EconomicCalendarsImportedCompleteEvent.Verb
            ]
    };

    /// <summary>
    /// start event consumer with action for each subscribed event
    /// </summary>
    /// <param name="addedAction"></param>
    /// <param name="changedAction"></param>
    /// <param name="removedAction"></param>
    /// <param name="importedAction"></param>
    /// <returns></returns>
    public async ValueTask StartAsync(
        Action<EconomicCalendarAddedCompleteEvent> addedAction,
        Action<EconomicCalendarChangedCompleteEvent> changedAction, 
        Action<EconomicCalendarRemovedCompleteEvent> removedAction,
        Action<EconomicCalendarsImportedCompleteEvent> importedAction)
    {
        await StartAsync(EventConsumer, _eventMap, EventHandlerAsync);

        async ValueTask EventHandlerAsync(string eventVerb, NatsMsg<byte[]> eventMsg)
        {
            try
            {
                _ = eventVerb switch
                {
                    _ when eventVerb == EconomicCalendarAddedCompleteEvent.Verb 
                        => HandleEvent(eventMsg.AsEvent<EconomicCalendarAddedCompleteEvent>()!, e => addedAction(e as EconomicCalendarAddedCompleteEvent)),
                    _ when eventVerb == EconomicCalendarChangedCompleteEvent.Verb 
                        => HandleEvent(eventMsg.AsEvent<EconomicCalendarChangedCompleteEvent>()!, e => changedAction(e as EconomicCalendarChangedCompleteEvent)),
                    _ when eventVerb == EconomicCalendarRemovedCompleteEvent.Verb 
                        => HandleEvent(eventMsg.AsEvent<EconomicCalendarRemovedCompleteEvent>()!, e => removedAction(e as EconomicCalendarRemovedCompleteEvent)),
                    _ when eventVerb == EconomicCalendarsImportedCompleteEvent.Verb 
                        => HandleEvent(eventMsg.AsEvent<EconomicCalendarsImportedCompleteEvent>()!, e => importedAction(e as EconomicCalendarsImportedCompleteEvent)),
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

public interface IEconomicCalendarUIEventConsumer
{
    ValueTask StartAsync(
        Action<EconomicCalendarAddedCompleteEvent> addedAction,
        Action<EconomicCalendarChangedCompleteEvent> changedAction,
        Action<EconomicCalendarRemovedCompleteEvent> removedAction,
        Action<EconomicCalendarsImportedCompleteEvent> importedAction);
    ValueTask StopAsync();
}
