using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.Trade.Events;
using TomasAI.IFM.Domain.Fund.Shared.Events;

namespace TomasAI.IFM.UI.EventConsumer;

/// <summary>
/// Represents a consumer for fund UI events, utilizing NATS for asynchronous event processing.
/// </summary>
/// <remarks>This class extends <see cref="NatsActorEventListener"/> and implements <see
/// cref="IFundUIEventConsumer"/> to handle fund-related UI events. It subscribes to events using a unique site
/// identifier and processes them asynchronously.</remarks>
/// <param name="options"></param>
/// <param name="logger"></param>
public class FundUIEventConsumer(INatsEventListenerOptions options, ILogger logger)
    : NatsActorEventListener(options, logger), IFundUIEventConsumer
{
    readonly static string EventConsumer = "FundUIEventConsumer";
    readonly ILogger _logger = logger;
    readonly Dictionary<ActorMailboxId, List<string>> _eventMap = new()
    {
        [new ActorMailboxId(ActorType.Event, EndOfDayFundTransactionProcessedCompleteEvent.Actor)]
               = [EndOfDayFundTransactionProcessedCompleteEvent.Verb,
                   EndOfDayFundTransactionProcessedFailEvent.Verb,
                   OptionTradeEndOfDayProcessedFailEvent.Verb]
    };

    public async ValueTask StartAsync(ICollection<IEvent> consumeEvents, Func<IEvent, ValueTask> eventAction)
    {

        await StartAsync( EventConsumer, _eventMap, EventHandlerAsync);

        async ValueTask EventHandlerAsync(string eventVerb, NatsMsg<byte[]> eventMsg)
        {
            try
            {
                var valueTask = eventVerb switch
                {
                    _ when eventVerb == EndOfDayFundTransactionProcessedCompleteEvent.Verb 
                        => HandleEventAsync(eventMsg.AsEvent<EndOfDayFundTransactionProcessedCompleteEvent>()!, eventAction),
                    _ when eventVerb == EndOfDayFundTransactionProcessedFailEvent.Verb 
                        => HandleEventAsync(eventMsg.AsEvent<EndOfDayFundTransactionProcessedFailEvent>()!, eventAction),
                    _ when eventVerb == OptionTradeEndOfDayProcessedFailEvent.Verb 
                        => HandleEventAsync(eventMsg.AsEvent<OptionTradeEndOfDayProcessedFailEvent>()!, eventAction),
                    _ => ValueTask.CompletedTask
                };
                await valueTask;
            }
            catch (Exception ex)
            {
                _logger.LogErrorEvent(EventConsumer, ex, "EventHandlerAsync: failed while processing event verb: {EventVerb}", eventVerb);
            }

            async ValueTask HandleEventAsync(IEvent e, Func<IEvent, ValueTask> eventAction)
                => await (eventAction?.Invoke(e) ?? ValueTask.CompletedTask);
        }
    }

}

public interface IFundUIEventConsumer
{
    ValueTask StartAsync(ICollection<IEvent> consumeEvents, Func<IEvent, ValueTask> eventAction);
    ValueTask StopAsync();
}


