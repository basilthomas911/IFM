using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Framework.Messaging.Nats;
using TomasAI.IFM.Framework.Messaging.Nats.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Trade.Events;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.UI.EventConsumer;

public class TradePlanActionUIEventConsumer(INatsEventListenerOptions options, ILogger logger)
    : NatsActorEventListener(options, logger), ITradePlanActionUIEventConsumer
{
    readonly static string EventConsumer = "TradePlanActionUIEventConsumer";
    readonly ILogger _logger = logger;
    readonly Dictionary<ActorMailboxId, List<string>> _eventMap = new()
    {
        [new ActorMailboxId(ActorType.Event, TradePlanActionUpdatedEvent.Actor)] = [TradePlanActionUpdatedEvent.Verb]
    };

    public async ValueTask StartAsync(Action<TradePlanActionUpdatedEvent> eventAction)
    {
        await StartAsync(EventConsumer, _eventMap, EventHandlerAsync);

        async ValueTask EventHandlerAsync(string eventVerb, NatsMsg<byte[]> eventMsg)
        {
            try
            {
                _ = eventVerb switch
                {
                    _ when eventVerb == TradePlanActionUpdatedEvent.Verb 
                        => HandleEvent(eventMsg.AsEvent<TradePlanActionUpdatedEvent>()!, e => eventAction?.Invoke(e)),
                    _ => default!
                };
                await ValueTask.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogErrorEvent(EventConsumer, ex, "EventHandlerAsync: failed while processing event verb: {EventVerb}", eventVerb);
            }

            IEvent HandleEvent(TradePlanActionUpdatedEvent e, Action<TradePlanActionUpdatedEvent> eventAction)
            {
                eventAction?.Invoke(e);
                return e;
            }
        }

    }

}
