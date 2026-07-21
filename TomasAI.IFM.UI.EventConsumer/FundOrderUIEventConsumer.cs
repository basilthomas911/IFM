using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Contracts;
using TomasAI.IFM.Shared.EventModelActor; 
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.Fund.Shared.Events;

namespace TomasAI.IFM.UI.EventConsumer;

/// <summary>
/// Represents a consumer for fund order UI events, utilizing NATS for asynchronous event processing.
/// </summary>
/// <remarks>This class is designed to handle events related to fund orders in a user interface context. It
/// extends <see cref="NatsActorEventListener"/> to leverage NATS for event streaming capabilities and implements <see
/// cref="IFundOrderUIEventConsumer"/> to provide a specific interface for fund order events.</remarks>
/// <param name="options"></param>
/// <param name="logger"></param>
public class FundOrderUIEventConsumer(INatsEventListenerOptions options, ILogger logger)
    : NatsActorEventListener(options, logger), IFundOrderUIEventConsumer
{
    readonly static string EventConsumer = "FundOrderUIEventConsumer";
    readonly ILogger _logger = logger;
    readonly Dictionary<ActorMailboxId, List<string>> _eventMap = new()
    {
        [new ActorMailboxId(ActorType.Event, OrderAddedToFundCompleteEvent.Actor)]
                   = [OrderAddedToFundCompleteEvent.Verb,
                       OrderAddedToFundFailEvent.Verb,
                       OrderRemovedFromFundCompleteEvent.Verb,
                       OrderRemovedFromFundFailEvent.Verb,
                       FundOrderClosedCompleteEvent.Verb,
                       FundOrderClosedFailEvent.Verb,
                       TradeAddedToFundOrderCompleteEvent.Verb,
                       TradeAddedToFundOrderFailEvent.Verb,
                       TradeRemovedFromFundOrderCompleteEvent.Verb,
                       TradeRemovedFromFundOrderFailEvent.Verb,
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
                    _ when eventVerb == OrderAddedToFundCompleteEvent.Verb 
                        => HandleEventAsync(eventMsg.AsEvent<OrderAddedToFundCompleteEvent>()!, eventAction),
                    _ when eventVerb == OrderAddedToFundFailEvent.Verb 
                        => HandleEventAsync(eventMsg.AsEvent<OrderAddedToFundFailEvent>()!, eventAction),
                    _ when eventVerb == OrderRemovedFromFundCompleteEvent.Verb 
                        => HandleEventAsync(eventMsg.AsEvent<OrderRemovedFromFundCompleteEvent>()!, eventAction),
                    _ when eventVerb == OrderRemovedFromFundFailEvent.Verb 
                        => HandleEventAsync(eventMsg.AsEvent<OrderRemovedFromFundFailEvent>()!, eventAction),
                    _ when eventVerb == FundOrderClosedCompleteEvent.Verb 
                        => HandleEventAsync(eventMsg.AsEvent<FundOrderClosedCompleteEvent>()!, eventAction),
                    _ when eventVerb == FundOrderClosedFailEvent.Verb 
                        => HandleEventAsync(eventMsg.AsEvent<FundOrderClosedFailEvent>()!, eventAction),
                    _ when eventVerb == TradeAddedToFundOrderCompleteEvent.Verb 
                        => HandleEventAsync(eventMsg.AsEvent<TradeAddedToFundOrderCompleteEvent>()!, eventAction),
                    _ when eventVerb == TradeAddedToFundOrderFailEvent.Verb 
                        => HandleEventAsync(eventMsg.AsEvent<TradeAddedToFundOrderFailEvent>()!, eventAction),
                    _ when eventVerb == TradeRemovedFromFundOrderCompleteEvent.Verb 
                        => HandleEventAsync(eventMsg.AsEvent<TradeRemovedFromFundOrderCompleteEvent>()!, eventAction),
                    _ when eventVerb == TradeRemovedFromFundOrderFailEvent.Verb 
                        => HandleEventAsync(eventMsg.AsEvent<TradeRemovedFromFundOrderFailEvent>()!, eventAction),
                    _ => ValueTask.CompletedTask
                };
                await valueTask;
            }
            catch (Exception ex)
            {
                _logger.LogErrorEvent(EventConsumer, ex, "EventHandlerAsync: failed while processing event verb: {EventVerb}", eventVerb);
            }

            async ValueTask HandleEventAsync(IEvent e, Func<IEvent, ValueTask> eventAction)
                => await eventAction.Invoke(e);
        }
    }

}

public interface IFundOrderUIEventConsumer
{
    ValueTask StartAsync(Func<IEvent, ValueTask> eventAction);
    ValueTask StopAsync();
}



