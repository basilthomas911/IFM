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
/// Consumes futures option quote data update events and processes them using a specified action.
/// </summary>
/// <remarks>This class subscribes to market data feed events and invokes a user-defined action whenever a <see
/// cref="FuturesOptionQuoteDataUpdatedEvent"/> is received. It is designed to be used in conjunction with a Kafka event
/// consumer infrastructure.</remarks>
/// <param name="options"></param>
/// <param name="logger"></param>
public class FuturesOptionQuoteDataUIEventConsumer(INatsEventListenerOptions options, ILogger logger)
    : NatsActorEventListener(options, logger), IFuturesOptionQuoteDataUIEventConsumer
{
    readonly static string EventConsumer = "FuturesOptionQuoteDataUIEventConsumer";
    readonly ILogger _logger = logger;
    readonly Dictionary<ActorMailboxId, List<string>> _eventMap = new()
    {
        [new ActorMailboxId(ActorType.Event, FuturesOptionQuoteDataUpdatedEvent.Actor)] = [FuturesOptionQuoteDataUpdatedEvent.Verb]
    };

    public async ValueTask StartAsync(Action<FuturesOptionQuoteDataUpdatedEvent> eventAction)
    {
        await StartAsync(EventConsumer, _eventMap, EventHandlerAsync);

        async ValueTask EventHandlerAsync(string eventVerb, NatsMsg<byte[]> eventMsg)
        {
            try
            {
                _ = eventVerb switch
                {
                    _ when eventVerb == FuturesOptionQuoteDataUpdatedEvent.Verb 
                        => HandleEvent(eventMsg.AsEvent<FuturesOptionQuoteDataUpdatedEvent>()!, eventAction),
                    _ => default!
                };
                await ValueTask.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogErrorEvent(EventConsumer, ex, "EventHandlerAsync: failed while processing event verb: {EventVerb}", eventVerb);
            }

            IEvent HandleEvent(FuturesOptionQuoteDataUpdatedEvent e, Action<FuturesOptionQuoteDataUpdatedEvent> eventAction)
            {
                eventAction?.Invoke(e);
                return e;
            }
        }
        
    }
}

public interface IFuturesOptionQuoteDataUIEventConsumer
{
    ValueTask StartAsync(Action<FuturesOptionQuoteDataUpdatedEvent> eventAction);
    ValueTask StopAsync();
}



