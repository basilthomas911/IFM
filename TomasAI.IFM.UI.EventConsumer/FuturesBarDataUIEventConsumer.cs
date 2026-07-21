using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.MarketDataFeed.Events;

namespace TomasAI.IFM.UI.EventConsumer;

public class FuturesBarDataUIEventConsumer(INatsEventListenerOptions options, ILogger logger)
    : NatsActorEventListener(options, logger), IFuturesBarDataUIEventConsumer
{
    readonly static string EventConsumer = "FuturesBarDataUIEventConsumer";
    readonly ILogger _logger = logger;
    readonly Dictionary<ActorMailboxId, List<string>> _eventMap = new()
    {
        [new ActorMailboxId(ActorType.Event, FuturesBarDataInsertedCompleteEvent.Actor)] = [FuturesBarDataInsertedCompleteEvent.Verb]
    };

    public async ValueTask StartAsync( Action<FuturesBarDataInsertedCompleteEvent> eventAction)
    {
        await StartAsync(EventConsumer, _eventMap, EventHandlerAsync);

        async ValueTask EventHandlerAsync(string eventVerb, NatsMsg<byte[]> eventMsg)
        {
            try
            {
                _ = eventVerb switch
                {
                    _ when eventVerb == FuturesBarDataInsertedCompleteEvent.Verb 
                        => HandleEvent(eventMsg.AsEvent<FuturesBarDataInsertedCompleteEvent>()!, e => eventAction?.Invoke(e)),
                    _ => default!
                };
                await ValueTask.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogErrorEvent(EventConsumer, ex, "EventHandlerAsync: failed while processing event verb: {EventVerb}", eventVerb);
            }

            IEvent HandleEvent(FuturesBarDataInsertedCompleteEvent e, Action<FuturesBarDataInsertedCompleteEvent> eventAction)
            {
                eventAction?.Invoke(e);
                return e;
            }
        }
    }
}

 public interface IFuturesBarDataUIEventConsumer
{
    ValueTask StartAsync(Action<FuturesBarDataInsertedCompleteEvent> eventAction);
    ValueTask StopAsync();
}
