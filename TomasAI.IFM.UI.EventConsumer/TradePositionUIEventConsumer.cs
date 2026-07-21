using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Trade.Events;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.UI.EventConsumer;

public class TradePositionUIEventConsumer(INatsEventListenerOptions options, ILogger logger)
    : NatsActorEventListener(options, logger), ITradePositionUIEventConsumer
{
    readonly static string EventConsumer = "TradePositionUIEventConsumer";
    readonly ILogger _logger = logger;
    readonly Dictionary<ActorMailboxId, List<string>> _eventMap = new()
    {
        [new ActorMailboxId(ActorType.Event, TradePositionUpdatedEvent.Actor)] = [TradePositionUpdatedEvent.Verb]
    };

    /// <summary>
    /// start event consumer
    /// </summary>
    /// <param name="eventAction"></param>
    public async ValueTask StartAsync(Action<TradePositionUpdatedEvent> eventAction)
    {
        await StartAsync(EventConsumer, _eventMap, EventHandlerAsync);

        async ValueTask EventHandlerAsync(string eventVerb, NatsMsg<byte[]> eventMsg)
        {
            try
            {
                _ = eventVerb switch
                {
                    _ when eventVerb == TradePositionUpdatedEvent.Verb => HandleEvent(eventMsg.AsEvent<TradePositionUpdatedEvent>()!, eventAction),
                    _ => default!
                };
                await ValueTask.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogErrorEvent(EventConsumer, ex, "EventHandlerAsync: failed while processing event verb: {EventVerb}", eventVerb);
            }

            IEvent HandleEvent(TradePositionUpdatedEvent e, Action<TradePositionUpdatedEvent> eventAction)
            {
                eventAction?.Invoke(e);
                return e;
            }
        }
    }

}
