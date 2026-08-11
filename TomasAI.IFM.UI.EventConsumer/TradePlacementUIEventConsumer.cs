using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Trade.Shared.Events;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.Trade.Shared.Events;

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

    public async ValueTask StartAsync(Func<IEvent, ValueTask> eventAction)
    {
        await StartAsync(EventConsumer, _eventMap, EventHandlerAsync);
      
        async ValueTask EventHandlerAsync(string eventVerb, NatsMsg<byte[]> eventMsg)
        {
            try
            {
                await (eventVerb switch
                {
                    _ when eventVerb == TradePlacementSetEvent.Verb 
                        => HandleEvent(eventMsg.AsEvent<TradePlacementSetEvent>()!, eventAction),
                    _ when eventVerb == TradePlacementWaitEvent.Verb 
                        => HandleEvent(eventMsg.AsEvent<TradePlacementWaitEvent>()!, eventAction),
                    _ when eventVerb == TradePlacementClearedEvent.Verb 
                        => HandleEvent(eventMsg.AsEvent<TradePlacementClearedEvent>()!, eventAction),
                    _ => ValueTask.CompletedTask
                });
            }
            catch (Exception ex)
            {
                _logger.LogErrorEvent(EventConsumer, ex, "EventHandlerAsync: failed while processing event verb: {EventVerb}", eventVerb);
            }

            static ValueTask HandleEvent(IEvent e, Func<IEvent, ValueTask> eventAction)
                => eventAction(e);
        }
    }

}
