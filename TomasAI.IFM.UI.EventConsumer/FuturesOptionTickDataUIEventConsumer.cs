using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Framework.Messaging.Nats;
using TomasAI.IFM.Framework.Messaging.Nats.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.Trade.Events;

namespace TomasAI.IFM.UI.EventConsumer;

/// <summary>
/// Consumes futures option tick data update events and processes them for UI updates.
/// </summary>
/// <remarks>This class subscribes to market data feed events and triggers a specified action when a <see
/// cref="OptionTradeTickPriceDataUpdatedEvent"/> is received. It is designed to integrate with UI components that need to
/// respond to real-time market data changes.</remarks>
public class FuturesOptionTickDataUIEventConsumer(INatsEventListenerOptions options, ILogger logger)
    : NatsActorEventListener(options, logger), IFuturesOptionTickDataUIEventConsumer
{
    readonly static string EventConsumer = "FuturesOptionTickDataUIEventConsumer";
    readonly ILogger _logger = logger;
    readonly Dictionary<ActorMailboxId, List<string>> _eventMap = new()
    {
        [new (ActorType.Event, OptionTradeTickPriceDataUpdatedEvent.Actor)] = [OptionTradeTickPriceDataUpdatedEvent.Verb]
    };

    public async ValueTask StartAsync(Action<OptionTradeTickPriceDataUpdatedEvent> eventAction) 
    {
        await StartAsync(EventConsumer, _eventMap, EventHandlerAsync);

        async ValueTask EventHandlerAsync(string eventVerb, NatsMsg<byte[]> eventMsg)
        {
            try
            {
                _ = eventVerb switch
                {
                    _ when eventVerb == OptionTradeTickPriceDataUpdatedEvent.Verb 
                        => HandleEvent(eventMsg.AsEvent<OptionTradeTickPriceDataUpdatedEvent>(), eventAction),
                    _ => default!
                };
                await ValueTask.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogErrorEvent(EventConsumer, ex, "EventHandlerAsync: failed while processing event verb: {EventVerb}", eventVerb);
            }

            IEvent HandleEvent(OptionTradeTickPriceDataUpdatedEvent e, Action<OptionTradeTickPriceDataUpdatedEvent> eventAction)
            {
                eventAction?.Invoke(e);
                return e;
            }
        }
    }
}

public interface IFuturesOptionTickDataUIEventConsumer
{
    ValueTask StartAsync(Action<OptionTradeTickPriceDataUpdatedEvent> eventAction);
    ValueTask StopAsync();
}

