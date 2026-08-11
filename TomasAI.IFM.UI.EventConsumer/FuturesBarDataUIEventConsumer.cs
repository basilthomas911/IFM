using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;

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

    public async ValueTask StartAsync(Func<FuturesBarDataInsertedCompleteEvent, ValueTask> eventAction)
    {
        await StartAsync(EventConsumer, _eventMap, EventHandlerAsync);

        async ValueTask EventHandlerAsync(string eventVerb, NatsMsg<byte[]> eventMsg)
        {
            try
            {
                switch (eventVerb)
                {
                    case FuturesBarDataInsertedCompleteEvent.Verb:
                        await eventAction(eventMsg.AsEvent<FuturesBarDataInsertedCompleteEvent>()!);
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
    ValueTask StartAsync(Func<FuturesBarDataInsertedCompleteEvent, ValueTask> eventAction);
    ValueTask StopAsync();
}
