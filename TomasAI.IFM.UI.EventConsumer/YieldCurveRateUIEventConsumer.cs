using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Framework.Messaging.Nats;
using TomasAI.IFM.Framework.Messaging.Nats.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketData.Events;

namespace TomasAI.IFM.UI.EventConsumer;

public class YieldCurveRateUIEventConsumer(INatsEventListenerOptions options, ILogger logger)
    : NatsActorEventListener(options, logger), IYieldCurveRateUIEventConsumer
{
    readonly static string EventConsumer = "YieldCurveRateUIEventConsumer";
    readonly ILogger _logger = logger;
    readonly Dictionary<ActorMailboxId, List<string>> _eventMap = new()
    {
        [new ActorMailboxId(ActorType.Event, YieldCurveRateAddedCompleteEvent.Actor)] 
            = [YieldCurveRateAddedCompleteEvent.Verb,
                YieldCurveRateAddedFailEvent.Verb,
                YieldCurveRateChangedCompleteEvent.Verb,
                YieldCurveRateChangedFailEvent.Verb,
                YieldCurveRateRemovedCompleteEvent.Verb,
                YieldCurveRateRemovedFailEvent.Verb,
                YieldCurveRatesImportedCompleteEvent.Verb,
                YieldCurveRatesImportedFailEvent.Verb
             ]
    };

    public async ValueTask StartAsync(Func<IEvent, ValueTask> eventAction)
    {
        await StartAsync(EventConsumer, _eventMap, EventHandlerAsync);

        async ValueTask EventHandlerAsync(string eventVerb, NatsMsg<byte[]> eventMsg)
        {
            try
            {
                var valueTask = eventVerb switch
                {
                    _ when eventVerb == YieldCurveRateAddedCompleteEvent.Verb
                        => HandleEvent(eventMsg.AsEvent<YieldCurveRateAddedCompleteEvent>(), eventAction),
                    _ when eventVerb == YieldCurveRateAddedFailEvent.Verb
                        => HandleEvent(eventMsg.AsEvent<YieldCurveRateAddedFailEvent>(), eventAction),
                    _ when eventVerb == YieldCurveRateChangedCompleteEvent.Verb
                        => HandleEvent(eventMsg.AsEvent<YieldCurveRateChangedCompleteEvent>(), eventAction),
                    _ when eventVerb == YieldCurveRateChangedFailEvent.Verb
                        => HandleEvent(eventMsg.AsEvent<YieldCurveRateChangedFailEvent>(), eventAction),
                    _ when eventVerb == YieldCurveRateRemovedCompleteEvent.Verb
                        => HandleEvent(eventMsg.AsEvent<YieldCurveRateRemovedCompleteEvent>(), eventAction),
                    _ when eventVerb == YieldCurveRateRemovedFailEvent.Verb
                        => HandleEvent(eventMsg.AsEvent<YieldCurveRateRemovedFailEvent>(), eventAction),
                    _ when eventVerb == YieldCurveRatesImportedCompleteEvent.Verb
                        => HandleEvent(eventMsg.AsEvent<YieldCurveRatesImportedCompleteEvent>(), eventAction),
                    _ when eventVerb == YieldCurveRatesImportedFailEvent.Verb
                        => HandleEvent(eventMsg.AsEvent<YieldCurveRatesImportedFailEvent>(), eventAction),
                    _ => ValueTask.CompletedTask
                };
                await valueTask;
            }
            catch (Exception ex)
            {
                _logger.LogErrorEvent(EventConsumer, ex, "EventHandlerAsync: failed while processing event verb: {EventVerb}", eventVerb);
            }

            ValueTask HandleEvent(IEvent e, Func<IEvent, ValueTask> eventAction)
                => eventAction?.Invoke(e) ?? ValueTask.CompletedTask;
        }
    }

}



