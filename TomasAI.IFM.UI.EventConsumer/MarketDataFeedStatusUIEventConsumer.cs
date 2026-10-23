using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Events;
using System.Collections.Concurrent;
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
    readonly INatsEventListenerOptions monitoringOptions = options;
    readonly ConcurrentDictionary<Guid, MarketDataFeedStatusUIEventConsumer> monitoringListeners = new();
    string listenerName = ConsumerName;

    /// <summary>Starts an independent listener so trade views cannot replace the shell or each other.</summary>
    public async ValueTask StartMonitoringAsync(Guid ownerId, Func<IEvent, ValueTask> eventAction)
    {
        if (ownerId == Guid.Empty) throw new ArgumentException("A trade view owner is required.", nameof(ownerId));
        ArgumentNullException.ThrowIfNull(eventAction);
        var listener = new MarketDataFeedStatusUIEventConsumer(monitoringOptions, _logger)
            { listenerName = $"{ConsumerName}.{ownerId:N}" };
        if (!monitoringListeners.TryAdd(ownerId, listener))
            throw new InvalidOperationException("The trade view already has a feed lifecycle listener.");
        try { await listener.StartAsync(eventAction).ConfigureAwait(false); }
        catch { await StopMonitoringAsync(ownerId).ConfigureAwait(false); throw; }
    }

    /// <summary>Releases one trade view's transport after its monitoring lifecycle has stopped.</summary>
    public async ValueTask StopMonitoringAsync(Guid ownerId)
    {
        if (monitoringListeners.TryRemove(ownerId, out var listener))
            await listener.DisposeAsync().ConfigureAwait(false);
    }
    readonly Dictionary<ActorMailboxId, List<string>> _eventMap = new()
    {
        [new ActorMailboxId(ActorType.Event, ScheduledTaskRunStageRecordedCompleteEvent.Actor)] =
        [ScheduledTaskRunStageRecordedCompleteEvent.Verb],
        [new ActorMailboxId(ActorType.Event, MarketDataFeedStartedCompleteEvent.Actor)] =
        [
            MarketDataFeedStartedCompleteEvent.Verb,
            MarketDataFeedStartedFailEvent.Verb,
            MarketDataFeedStoppedCompleteEvent.Verb,
            MarketDataFeedStoppedFailEvent.Verb,
            MarketDataDatasetRecoveryFailedEvent.Verb,
            DailyAnalyticsInitializationStatusEvent.Verb,
            OptionMarketDataFeedFailedEvent.Verb
        ]
    };

    public async ValueTask StartAsync(Func<IEvent, ValueTask> eventAction)
    {
        ArgumentNullException.ThrowIfNull(eventAction);
        await StartAsync(listenerName, _eventMap, HandleAsync).ConfigureAwait(false);

        async ValueTask HandleAsync(string verb, NatsMsg<byte[]> message)
        {
            try
            {
                IEvent? @event = verb switch
                {
                    DailyAnalyticsInitializationStatusEvent.Verb
                        => message.AsEvent<DailyAnalyticsInitializationStatusEvent>(),
                    ScheduledTaskRunStageRecordedCompleteEvent.Verb
                        => message.AsEvent<ScheduledTaskRunStageRecordedCompleteEvent>(),
                    MarketDataDatasetRecoveryFailedEvent.Verb
                        => message.AsEvent<MarketDataDatasetRecoveryFailedEvent>(),
                    OptionMarketDataFeedFailedEvent.Verb
                        => message.AsEvent<OptionMarketDataFeedFailedEvent>(),
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
