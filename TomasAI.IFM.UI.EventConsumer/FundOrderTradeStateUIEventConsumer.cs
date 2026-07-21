using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.Fund.Shared.Events;

namespace TomasAI.IFM.UI.EventConsumer;

public class FundOrderTradeStateUIEventConsumer(INatsEventListenerOptions options, ILogger logger)
    : NatsActorEventListener(options, logger), IFundOrderTradeStateUIEventConsumer
{
    readonly static string EventConsumer = "FundOrderTradeStateUIEventConsumer";
    readonly ILogger _logger = logger;
    readonly Dictionary<ActorMailboxId, List<string>> _eventMap = new()
    {
        [new ActorMailboxId(ActorType.Event, FundOrderTradeStateChangedCompleteEvent.Actor)]
            = [FundOrderTradeStateChangedCompleteEvent.Verb, FundOrderTradeStateChangedFailEvent.Verb]
    };

    public async ValueTask StartAsync(
       Action<FundOrderTradeStateChangedCompleteEvent> completeAction,
       Action<FundOrderTradeStateChangedFailEvent> failAction)
    {
        await StartAsync(EventConsumer, _eventMap, EventHandlerAsync);

        async ValueTask EventHandlerAsync(string eventVerb, NatsMsg<byte[]> eventMsg)
        {
            try
            {
                _ = eventVerb switch
                {
                    _ when eventVerb == FundOrderTradeStateChangedCompleteEvent.Verb 
                        => HandleEvent(eventMsg.AsEvent<FundOrderTradeStateChangedCompleteEvent>()!, e => completeAction?.Invoke((e as FundOrderTradeStateChangedCompleteEvent)!)),
                    _ when eventVerb == FundOrderTradeStateChangedFailEvent.Verb 
                        => HandleEvent(eventMsg.AsEvent<FundOrderTradeStateChangedFailEvent>()!, e => failAction?.Invoke((e as FundOrderTradeStateChangedFailEvent)!)),
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

public interface IFundOrderTradeStateUIEventConsumer
{
    ValueTask StartAsync(Action<FundOrderTradeStateChangedCompleteEvent> completeAction, Action<FundOrderTradeStateChangedFailEvent> failAction);
    ValueTask StopAsync();
}
