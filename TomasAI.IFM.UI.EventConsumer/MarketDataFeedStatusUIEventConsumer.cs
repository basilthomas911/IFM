using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.UI.EventConsumer;

/// <summary>
/// Provides the desktop with exact terminal events for market-data feed start and stop commands.
/// </summary>
public sealed class MarketDataFeedStatusUIEventConsumer(
    INatsEventListenerOptions options,
    ILogger logger)
    : NatsActorEventListener(options, logger), IMarketDataFeedStatusUIEventConsumer
{
    const string ConsumerName = nameof(MarketDataFeedStatusUIEventConsumer);
    readonly ILogger _logger = logger;
    readonly Dictionary<ActorMailboxId, List<string>> _eventMap = new()
    {
        [new ActorMailboxId(ActorType.Event, MarketDataFeedStartedCompleteEvent.Actor)] =
        [
            MarketDataFeedStartedCompleteEvent.Verb,
            MarketDataFeedStartedFailEvent.Verb,
            MarketDataFeedStoppedCompleteEvent.Verb,
            MarketDataFeedStoppedFailEvent.Verb
        ]
    };

    public async ValueTask StartAsync(Func<IEvent, ValueTask> eventAction)
    {
        ArgumentNullException.ThrowIfNull(eventAction);
        await StartAsync(ConsumerName, _eventMap, HandleAsync).ConfigureAwait(false);

        async ValueTask HandleAsync(string verb, NatsMsg<byte[]> message)
        {
            try
            {
                IEvent? @event = verb switch
                {
                    MarketDataFeedStartedCompleteEvent.Verb
                        => message.AsEvent<MarketDataFeedStartedCompleteEvent>(),
                    MarketDataFeedStartedFailEvent.Verb
                        => message.AsEvent<MarketDataFeedStartedFailEvent>(),
                    MarketDataFeedStoppedCompleteEvent.Verb
                        => message.AsEvent<MarketDataFeedStoppedCompleteEvent>(),
                    MarketDataFeedStoppedFailEvent.Verb
                        => message.AsEvent<MarketDataFeedStoppedFailEvent>(),
                    _ => null
                };
                if (@event is not null)
                    await eventAction(@event).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "{Consumer}: failed while processing event verb {EventVerb}.",
                    ConsumerName,
                    verb);
            }
        }
    }
}
