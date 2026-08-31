using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;

namespace TomasAI.IFM.UI.EventConsumer;

public class FuturesBarDataUIEventConsumer(INatsEventListenerOptions options, ILogger logger)
    : NatsActorEventListener(options, logger), IFuturesBarDataUIEventConsumer
{
    readonly static string EventConsumer = "FuturesBarDataUIEventConsumer";
    readonly ILogger _logger = logger;
    readonly Dictionary<ActorMailboxId, List<string>> _eventMap = new()
    {
        [new ActorMailboxId(ActorType.Event, FuturesBarDataInsertedCompleteEvent.Actor)] = [FuturesBarDataInsertedCompleteEvent.Verb],
        [new ActorMailboxId(ActorType.Realtime, FuturesMarketPriceUpdatedRealtimeEvent.Actor)] = [FuturesMarketPriceUpdatedRealtimeEvent.Verb]
    };

    public async ValueTask StartAsync(
        Func<FuturesBarDataInsertedCompleteEvent, ValueTask> barEventAction,
        Func<FuturesMarketPriceUpdatedRealtimeEvent, ValueTask> acceptedPriceAction)
    {
        await StartAsync(EventConsumer, _eventMap, EventHandlerAsync);

        async ValueTask EventHandlerAsync(string eventVerb, NatsMsg<byte[]> eventMsg)
        {
            try
            {
                switch (eventVerb)
                {
                    case FuturesBarDataInsertedCompleteEvent.Verb:
                        await barEventAction(eventMsg.AsEvent<FuturesBarDataInsertedCompleteEvent>()!);
                        break;
                    case FuturesMarketPriceUpdatedRealtimeEvent.Verb:
                        await acceptedPriceAction(eventMsg.AsEvent<FuturesMarketPriceUpdatedRealtimeEvent>()!);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogErrorEvent(EventConsumer, ex, "EventHandlerAsync: failed while processing event verb: {EventVerb}", eventVerb);
            }
        }
    }
}

 public interface IFuturesBarDataUIEventConsumer
{
    ValueTask StartAsync(
        Func<FuturesBarDataInsertedCompleteEvent, ValueTask> barEventAction,
        Func<FuturesMarketPriceUpdatedRealtimeEvent, ValueTask> acceptedPriceAction);
    ValueTask StopAsync();
}
