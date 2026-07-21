using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.MarketData.Events;

namespace TomasAI.IFM.UI.EventConsumer;

public class FuturesOptionContractUIEventConsumer(INatsEventListenerOptions options, ILogger logger)
    : NatsActorEventListener(options, logger), IFuturesOptionContractUIEventConsumer
{
    readonly static string EventConsumer = "FuturesOptionContractUIEventConsumer";
    readonly ILogger _logger = logger;
    readonly Dictionary<ActorMailboxId, List<string>> _eventMap = new()
    {
        [new ActorMailboxId(ActorType.Event, FuturesOptionContractAddedCompleteEvent.Actor)] 
            = [FuturesOptionContractAddedCompleteEvent.Verb,
                FuturesOptionContractAddedFailEvent.Verb,
                FuturesOptionContractChangedCompleteEvent.Verb, 
                FuturesOptionContractChangedFailEvent.Verb,
                FuturesOptionContractRemovedCompleteEvent.Verb,
                FuturesOptionContractRemovedFailEvent.Verb,
            ]
    };

    public async ValueTask StartAsync( Func<IEvent, ValueTask> eventAction)
    {
        await StartAsync(EventConsumer, _eventMap, EventHandlerAsync);

        async ValueTask EventHandlerAsync(string eventVerb, NatsMsg<byte[]> eventMsg)
        {
            try
            {
                ValueTask valueTask = eventVerb switch
                {
                    _ when eventVerb == FuturesOptionContractAddedCompleteEvent.Verb
                        => HandleEvent(eventMsg.AsEvent<FuturesOptionContractAddedCompleteEvent>()!, eventAction),
                    _ when eventVerb == FuturesOptionContractAddedFailEvent.Verb
                        => HandleEvent(eventMsg.AsEvent<FuturesOptionContractAddedFailEvent>()!, eventAction),
                    _ when eventVerb == FuturesOptionContractChangedCompleteEvent.Verb
                        => HandleEvent(eventMsg.AsEvent<FuturesOptionContractChangedCompleteEvent>()!, eventAction),
                    _ when eventVerb == FuturesOptionContractChangedFailEvent.Verb
                        => HandleEvent(eventMsg.AsEvent<FuturesOptionContractChangedFailEvent>()!, eventAction),
                    _ when eventVerb == FuturesOptionContractRemovedCompleteEvent.Verb
                        => HandleEvent(eventMsg.AsEvent<FuturesOptionContractRemovedCompleteEvent>()!, eventAction),
                    _ when eventVerb == FuturesOptionContractRemovedFailEvent.Verb
                        => HandleEvent(eventMsg.AsEvent<FuturesOptionContractRemovedFailEvent>()!, eventAction),
                    _ => ValueTask.CompletedTask
                };
                await valueTask;
            }
            catch (Exception ex)
            {
                _logger.LogErrorEvent(EventConsumer, ex, "EventHandlerAsync: failed while processing event verb: {EventVerb}", eventVerb);
            }

            ValueTask HandleEvent(IEvent e, Func<IEvent, ValueTask> eventAction)
                => eventAction(e);
        }
    }

}
