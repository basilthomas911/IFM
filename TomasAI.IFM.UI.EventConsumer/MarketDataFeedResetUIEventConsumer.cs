using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.MarketDataFeed.Events;

namespace TomasAI.IFM.UI.EventConsumer;

/// <summary>
/// Consumes market data feed reset events and processes them for UI updates.
/// </summary>
/// <remarks>This class subscribes to market data feed reset streaming events and triggers a specified action when
/// such an event is received. It is designed to integrate with UI components that need to respond to market data
/// feed reset notifications.</remarks>
public class MarketDataFeedResetUIEventConsumer(INatsEventListenerOptions options, ILogger logger)
    : NatsActorEventListener(options, logger), IMarketDataFeedResetUIEventConsumer
{
    readonly static string EventConsumer = "MarketDataFeedResetUIEventConsumer";
    readonly ILogger _logger = logger;
    readonly Dictionary<ActorMailboxId, List<string>> _eventMap = new()
    {
        [new ActorMailboxId(ActorType.Event, MarketDataFeedResetStreamingEvent.Actor)] = [MarketDataFeedResetStreamingEvent.Verb]
    };

    public async ValueTask StartAsync(Action<MarketDataFeedResetStreamingEvent> eventAction)
    {
        await StartAsync(EventConsumer, _eventMap, EventHandlerAsync);

        async ValueTask EventHandlerAsync(string eventVerb, NatsMsg<byte[]> eventMsg)
        {
            try
            {
                _ = eventVerb switch
                {
                    _ when eventVerb == MarketDataFeedResetStreamingEvent.Verb 
                        => HandleEvent(eventMsg.AsEvent<MarketDataFeedResetStreamingEvent>()!, eventAction),
                    _ => default!
                };
                await ValueTask.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogErrorEvent(EventConsumer, ex, "EventHandlerAsync: failed while processing event verb: {EventVerb}", eventVerb);
            }

            IEvent HandleEvent(MarketDataFeedResetStreamingEvent e, Action<MarketDataFeedResetStreamingEvent> eventAction)
            {
                eventAction?.Invoke(e);
                return e;
            }
        }
       
    }
}
