using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Framework.Messaging.Nats;
using TomasAI.IFM.Framework.Messaging.Nats.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.Fund.Shared.Events;

namespace TomasAI.IFM.UI.EventConsumer;

/// <summary>
/// fund risk margin ui event consumer constructor
/// </summary>
/// <param name="options"></param>
/// <param name="logger"></param>
public class FundRiskMarginUIEventConsumer(INatsEventListenerOptions options, ILogger logger)
    : NatsActorEventListener(options, logger), IFundRiskMarginUIEventConsumer
{
    readonly static string EventConsumer = "FundRiskMarginUIEventConsumer";
    readonly ILogger _logger = IsArgumentNull.Set(logger);
    readonly Dictionary<ActorMailboxId, List<string>> _eventMap = new()
    {
        [new ActorMailboxId(ActorType.Event, FundMaxProfitGeneratedCompleteEvent.Actor)]
                = [FundMaxProfitGeneratedCompleteEvent.Verb,
                    FundMaxProfitGeneratedCompleteEvent.Verb,
                ]
    };

    /// <summary>
    /// start event consumer with action for each subscribed event
    /// </summary>
    /// <param name="completeAction"></param>
    /// <param name="failAction"></param>
    /// <returns></returns>
    public async ValueTask StartAsync(
        Action<FundMaxProfitGeneratedCompleteEvent> completeAction,
        Action<FundMaxProfitGeneratedFailEvent> failAction)
    {
        await StartAsync( EventConsumer, _eventMap, EventHandlerAsync);

        async ValueTask EventHandlerAsync(string eventVerb, NatsMsg<byte[]> eventMsg)
        {
            try
            {
                _ = eventVerb switch
                {
                    _ when eventVerb == FundMaxProfitGeneratedCompleteEvent.Verb 
                        => HandleEvent(eventMsg.AsEvent<FundMaxProfitGeneratedCompleteEvent>()!, e => completeAction?.Invoke(e as FundMaxProfitGeneratedCompleteEvent)),
                    _ when eventVerb == FundMaxProfitGeneratedFailEvent.Verb 
                        => HandleEvent(eventMsg.AsEvent<FundMaxProfitGeneratedFailEvent>()!, e => failAction?.Invoke(e as FundMaxProfitGeneratedFailEvent)),
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

public interface IFundRiskMarginUIEventConsumer
{
    ValueTask StartAsync(
        Action<FundMaxProfitGeneratedCompleteEvent> completeAction,
        Action<FundMaxProfitGeneratedFailEvent> failAction);
    ValueTask StopAsync();
}

