using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Trade.Events;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.UI.EventConsumer;

public class OptionTradeSpreadBarDataUIEventConsumer(INatsEventListenerOptions options, ILogger logger)
    : NatsActorEventListener(options, logger), IOptionTradeSpreadBarDataUIEventConsumer
{
    readonly static string EventConsumer = "OptionTradeSpreadBarDataUIEventConsumer";
    readonly ILogger _logger = logger;
    readonly Dictionary<ActorMailboxId, List<string>> _eventMap = new()
    {
        [new ActorMailboxId(ActorType.Event, OptionTradeSpreadBarDataInsertedCompleteEvent.Actor)] = [OptionTradeSpreadBarDataInsertedCompleteEvent.Verb]
    };

    /// <summary>
    /// start event consumer
    /// </summary>
    /// <param name="eventAction"></param>
    public async ValueTask StartAsync(Action<OptionTradeSpreadBarDataInsertedCompleteEvent> eventAction)
    {
        await StartAsync(EventConsumer, _eventMap, EventHandlerAsync);

        async ValueTask EventHandlerAsync(string eventVerb, NatsMsg<byte[]> eventMsg)
        {
            try
            {
                _ = eventVerb switch
                {
                    _ when eventVerb == OptionTradeSpreadBarDataInsertedCompleteEvent.Verb 
                        => HandleEvent(eventMsg.AsEvent<OptionTradeSpreadBarDataInsertedCompleteEvent>()!, eventAction),
                    _ => default!
                };
                await ValueTask.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogErrorEvent(EventConsumer, ex, "EventHandlerAsync: failed while processing event verb: {EventVerb}", eventVerb);
            }

            IEvent HandleEvent(OptionTradeSpreadBarDataInsertedCompleteEvent e, Action<OptionTradeSpreadBarDataInsertedCompleteEvent> eventAction)
            {
                eventAction?.Invoke(e);
                return e;
            }
        }
    }

}
