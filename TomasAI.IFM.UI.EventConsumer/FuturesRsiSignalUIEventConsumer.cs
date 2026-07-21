using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.MarketDataAnalytics.Events;
using TomasAI.IFM.Shared.MarketDataFeed.Events;
using TomasAI.IFM.Shared.Trade.Events;

namespace TomasAI.IFM.UI.EventConsumer
{
    /// <summary>
    /// futures rsi event consumer constrictor
    /// </summary>
    /// <param name="options"></param>
    /// <param name="logger"></param>
    public class FuturesRsiSignalUIEventConsumer(INatsEventListenerOptions options, ILogger logger)
    : NatsActorEventListener(options, logger), IFuturesRsiSignalUIEventConsumer
    {
        readonly static string EventConsumer = "FuturesRsiSignalUIEventConsumer";
        readonly ILogger _logger = logger;
        readonly Dictionary<ActorMailboxId, List<string>> _eventMap = new()
        {
            [new(ActorType.Event, FuturesTdiSignalGeneratedCompleteEvent.Actor)] = [FuturesTdiSignalGeneratedCompleteEvent.Verb]
        };

        /// <summary>
        /// start futures rsi signal event consumer
        /// </summary>
        /// <param name="siteId"></param>
        /// <param name="eventAction"></param>
        /// <returns></returns>
        public async ValueTask StartAsync(Action<FuturesTdiSignalGeneratedCompleteEvent> eventAction)
        {
            await StartAsync(EventConsumer, _eventMap, EventHandlerAsync);

            async ValueTask EventHandlerAsync(string eventVerb, NatsMsg<byte[]> eventMsg)
            {
                try
                {
                    _ = eventVerb switch
                    {
                        _ when eventVerb == FuturesTdiSignalGeneratedCompleteEvent.Verb 
                        => HandleEvent(eventMsg.AsEvent<FuturesTdiSignalGeneratedCompleteEvent>()!, eventAction),
                        _ => default!
                    };
                    await ValueTask.CompletedTask;
                }
                catch (Exception ex)
                {
                    _logger.LogErrorEvent(EventConsumer, ex, "EventHandlerAsync: failed while processing event verb: {EventVerb}", eventVerb);
                }
            }

            IEvent HandleEvent(FuturesTdiSignalGeneratedCompleteEvent e, Action<FuturesTdiSignalGeneratedCompleteEvent> eventAction)
            {
                eventAction?.Invoke(e);
                return e;
            }
        }

    }
}

public interface IFuturesRsiSignalUIEventConsumer
{
    ValueTask StartAsync(Action<FuturesTdiSignalGeneratedCompleteEvent> eventAction);
    ValueTask StopAsync();
}

