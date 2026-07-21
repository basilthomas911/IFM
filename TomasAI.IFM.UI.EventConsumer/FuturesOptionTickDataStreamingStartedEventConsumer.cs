using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.MarketDataFeed.Events;

namespace TomasAI.IFM.UI.EventConsumer;

public class FuturesOptionTickDataStreamingStartedEventConsumer(INatsEventListenerOptions options, ILogger logger)
    : NatsActorEventListener(options, logger), IFuturesOptionTickDataStreamingStartedEventConsumer
{
    readonly static string EventConsumer = "FuturesOptionTickDataStreamingStartedEventConsumer";
    readonly ILogger _logger = logger;
    readonly Dictionary<ActorMailboxId, List<string>> _eventMap = new()
    {
        [new ActorMailboxId(ActorType.Event, FuturesOptionTickDataStreamingStartedCompleteEvent.Actor)]
                = [FuturesOptionTickDataStreamingStartedCompleteEvent.Verb,
                    FuturesOptionTickDataStreamingStartedFailEvent.Verb]
    };

    public async ValueTask StartAsync(
        Action<FuturesOptionTickDataStreamingStartedCompleteEvent> completeAction,
        Action<FuturesOptionTickDataStreamingStartedFailEvent> failAction)
    {
        await StartAsync(EventConsumer, _eventMap, EventHandlerAsync);

        async ValueTask EventHandlerAsync(string eventVerb, NatsMsg<byte[]> eventMsg)
        {
            try
            {
                _ = eventVerb switch
                {
                    _ when eventVerb == FuturesOptionTickDataStreamingStartedCompleteEvent.Verb 
                        => HandleEvent(eventMsg.AsEvent<FuturesOptionTickDataStreamingStartedCompleteEvent>()!, e => completeAction?.Invoke(e as FuturesOptionTickDataStreamingStartedCompleteEvent)),
                    _ when eventVerb == FuturesOptionTickDataStreamingStartedFailEvent.Verb 
                        => HandleEvent(eventMsg.AsEvent<FuturesOptionTickDataStreamingStartedFailEvent>()!, e => failAction?.Invoke(e as FuturesOptionTickDataStreamingStartedFailEvent)),
                    _ => default!
                };
                await ValueTask.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogErrorEvent(EventConsumer, ex, "EventHandlerAsync: failed while processing event verb: {EventVerb}", eventVerb);
            }

            IEvent HandleEvent(IEvent e, Action<IEvent> eventAction)
            {
                eventAction?.Invoke(e);
                return e;
            }
        }
    }
}

public interface IFuturesOptionTickDataStreamingStartedEventConsumer
{
    ValueTask StartAsync(
        Action<FuturesOptionTickDataStreamingStartedCompleteEvent> completeAction,
        Action<FuturesOptionTickDataStreamingStartedFailEvent> failAction);
    ValueTask StopAsync();
}
