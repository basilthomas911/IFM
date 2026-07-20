using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Framework.Messaging.Nats;
using TomasAI.IFM.Framework.Messaging.Nats.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Trade.Events;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.UI.EventConsumer;

public class TradePlacementUIEventConsumer(INatsEventListenerOptions options, ILogger logger)
        : NatsActorEventListener(options, logger), ITradePlacementUIEventConsumer
{
    readonly static string EventConsumer = "TradePlacementUIEventConsumer";
    readonly ILogger _logger = logger;
    readonly Dictionary<ActorMailboxId, List<string>> _eventMap = new()
    {
        [new ActorMailboxId(ActorType.Event, TradePlacementSetEvent.Actor)] = [
                   TradePlacementSetEvent.Verb,
                   TradePlacementWaitEvent.Verb,
                   TradePlacementClearedEvent.Verb
               ]
    };

    public async ValueTask StartAsync(Action<IEvent> eventAction)
    {
        await StartAsync(EventConsumer, _eventMap, EventHandlerAsync);
      
        async ValueTask EventHandlerAsync(string eventVerb, NatsMsg<byte[]> eventMsg)
        {
            try
            {
                _ = eventVerb switch
                {
                    _ when eventVerb == TradePlacementSetEvent.Verb 
                        => HandleEvent(eventMsg.AsEvent<TradePlacementSetEvent>()!, eventAction),
                    _ when eventVerb == TradePlacementWaitEvent.Verb 
                        => HandleEvent(eventMsg.AsEvent<TradePlacementWaitEvent>()!, eventAction),
                    _ when eventVerb == TradePlacementClearedEvent.Verb 
                        => HandleEvent(eventMsg.AsEvent<TradePlacementClearedEvent>()!, eventAction),
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
