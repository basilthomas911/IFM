using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Trade.Shared.Events;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.UI.EventConsumer;

public class TradePlanUIEventConsumer(INatsEventListenerOptions options, ILogger logger)
    : NatsActorEventListener(options, logger), ITradePlanUIEventConsumer
{
    readonly static string EventConsumer = "TradePlanUIEventConsumer";
    readonly ILogger _logger = logger;
    readonly Dictionary<ActorMailboxId, List<string>> _eventMap = new()
    {
        [new ActorMailboxId(ActorType.Event, TradePlanUpdatedEvent.Actor)] = [TradePlanUpdatedEvent.Verb]
    };

    public async ValueTask StartAsync(Func<TradePlanUpdatedEvent, ValueTask> eventAction)
    {
        await StartAsync(EventConsumer, _eventMap, EventHandlerAsync);

        async ValueTask EventHandlerAsync(string eventVerb, NatsMsg<byte[]> eventMsg)
        {
            try
            {
                await (eventVerb switch
                {
                    _ when eventVerb == TradePlanUpdatedEvent.Verb 
                        => HandleEventAsync(eventMsg.AsEvent<TradePlanUpdatedEvent>()!, eventAction),
                    _ => ValueTask.CompletedTask
                });
            }
            catch (Exception ex)
            {
                _logger.LogErrorEvent(EventConsumer, ex, "EventHandlerAsync: failed while processing event verb: {EventVerb}", eventVerb);
            }

            static ValueTask HandleEventAsync(
                TradePlanUpdatedEvent e,
                Func<TradePlanUpdatedEvent, ValueTask> eventAction)
                => eventAction is null ? ValueTask.CompletedTask : eventAction(e);
        }

    }

}
