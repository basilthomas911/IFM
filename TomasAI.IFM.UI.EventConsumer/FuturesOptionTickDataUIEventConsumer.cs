using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.Trade.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;

namespace TomasAI.IFM.UI.EventConsumer;

/// <summary>
/// Consumes futures option tick data update events and processes them for UI updates.
/// </summary>
/// <remarks>This class subscribes to market data feed events and triggers a specified action when a <see
/// cref="OptionTradeTickPriceDataUpdatedEvent"/> is received. It is designed to integrate with UI components that need to
/// respond to real-time market data changes.</remarks>
public class FuturesOptionTickDataUIEventConsumer(INatsEventListenerOptions options, ILogger logger)
    : NatsActorEventListener(options, logger), IFuturesOptionTickDataUIEventConsumer
{
    readonly static string EventConsumer = "FuturesOptionTickDataUIEventConsumer";
    readonly ILogger _logger = logger;
    readonly INatsEventListenerOptions _monitoringOptions = options;
    readonly ConcurrentDictionary<Guid, FuturesOptionTickDataUIEventConsumer> _monitoringListeners = new();
    string _listenerName = EventConsumer;

    /// <summary>Starts an independent monitoring listener owned by one trade view.</summary>
    /// <param name="ownerId">The view subscription identity.</param>
    /// <returns>The listener registration operation.</returns>
    public async ValueTask StartMonitoringAsync(Guid ownerId, Func<OptionTradeTickPriceDataUpdatedEvent, ValueTask> observation, Func<FuturesOptionTickDataStreamingStartedCompleteEvent, ValueTask> started, Func<FuturesOptionTickDataStreamingStartedFailEvent, ValueTask> failed)
    {
        if (ownerId == Guid.Empty) throw new ArgumentException("A monitoring owner is required.", nameof(ownerId));
        var listener = new FuturesOptionTickDataUIEventConsumer(_monitoringOptions, _logger) { _listenerName = $"{EventConsumer}.{ownerId:N}" };
        if (!_monitoringListeners.TryAdd(ownerId, listener)) throw new InvalidOperationException("The monitoring owner already has a listener.");
        try { await listener.StartMonitoringAsync(observation, started, failed).ConfigureAwait(false); }
        catch { await StopMonitoringAsync(ownerId).ConfigureAwait(false); throw; }
    }

    /// <summary>Stops only the specified trade view's monitoring listener.</summary>
    /// <param name="ownerId">The view subscription identity.</param>
    /// <returns>The listener cleanup operation.</returns>
    public async ValueTask StopMonitoringAsync(Guid ownerId)
    {
        if (_monitoringListeners.TryRemove(ownerId, out var listener))
            await listener.StopAsync().ConfigureAwait(false);
    }

    readonly Dictionary<ActorMailboxId, List<string>> _eventMap = new()
    {
        [new(ActorType.Notify, OptionTradeTickPriceDataUpdatedEvent.Actor)] = [OptionTradeTickPriceDataUpdatedEvent.Verb]
    };

    /// <summary>Consumes leg observations and provider-confirmed startup outcomes on one monitoring listener.</summary>
    /// <param name="observation">Receives option leg observations.</param>
    /// <param name="started">Receives the acknowledged exact subscription owner.</param>
    /// <param name="failed">Receives provider startup failures.</param>
    /// <returns>The listener registration operation.</returns>
    public async ValueTask StartMonitoringAsync(Func<OptionTradeTickPriceDataUpdatedEvent, ValueTask> observation,
        Func<FuturesOptionTickDataStreamingStartedCompleteEvent, ValueTask> started,
        Func<FuturesOptionTickDataStreamingStartedFailEvent, ValueTask> failed)
    {
        var eventMap = new Dictionary<ActorMailboxId, List<string>>(_eventMap)
        {
            [new(ActorType.Event, FuturesOptionTickDataStreamingStartedCompleteEvent.Actor)] =
                [FuturesOptionTickDataStreamingStartedCompleteEvent.Verb, FuturesOptionTickDataStreamingStartedFailEvent.Verb]
        };
        await StartAsync(_listenerName, eventMap, HandleAsync);
        async ValueTask HandleAsync(string verb, NatsMsg<byte[]> message)
        {
            try
            {
                await (verb switch
                {
                    OptionTradeTickPriceDataUpdatedEvent.Verb => observation(message.AsEvent<OptionTradeTickPriceDataUpdatedEvent>()),
                    FuturesOptionTickDataStreamingStartedCompleteEvent.Verb => started(message.AsEvent<FuturesOptionTickDataStreamingStartedCompleteEvent>()),
                    FuturesOptionTickDataStreamingStartedFailEvent.Verb => failed(message.AsEvent<FuturesOptionTickDataStreamingStartedFailEvent>()),
                    _ => ValueTask.CompletedTask
                });
            }
            catch (Exception error)
            {
                _logger.LogError(error, "Monitoring listener failed; MethodName={MethodName} EventVerb={EventVerb}", nameof(HandleAsync), verb);
            }
        }
    }

    public async ValueTask StartAsync(Func<OptionTradeTickPriceDataUpdatedEvent, ValueTask> eventAction)
    {
        await StartAsync(_listenerName, _eventMap, EventHandlerAsync);

        async ValueTask EventHandlerAsync(string eventVerb, NatsMsg<byte[]> eventMsg)
        {
            try
            {
                await (eventVerb switch
                {
                    _ when eventVerb == OptionTradeTickPriceDataUpdatedEvent.Verb
                        => HandleEvent(eventMsg.AsEvent<OptionTradeTickPriceDataUpdatedEvent>(), eventAction),
                    _ => ValueTask.CompletedTask
                });
            }
            catch (Exception ex)
            {
                _logger.LogErrorEvent(EventConsumer, ex, "EventHandlerAsync: failed while processing event verb: {EventVerb}", eventVerb);
            }

            static ValueTask HandleEvent(
                OptionTradeTickPriceDataUpdatedEvent e,
                Func<OptionTradeTickPriceDataUpdatedEvent, ValueTask> eventAction)
                => eventAction(e);
        }
    }
}

public interface IFuturesOptionTickDataUIEventConsumer
{
    ValueTask StartAsync(Func<OptionTradeTickPriceDataUpdatedEvent, ValueTask> eventAction);
    ValueTask StartMonitoringAsync(Func<OptionTradeTickPriceDataUpdatedEvent, ValueTask> observation,
        Func<FuturesOptionTickDataStreamingStartedCompleteEvent, ValueTask> started,
        Func<FuturesOptionTickDataStreamingStartedFailEvent, ValueTask> failed);
    ValueTask StartMonitoringAsync(Guid ownerId, Func<OptionTradeTickPriceDataUpdatedEvent, ValueTask> observation, Func<FuturesOptionTickDataStreamingStartedCompleteEvent, ValueTask> started, Func<FuturesOptionTickDataStreamingStartedFailEvent, ValueTask> failed);
    ValueTask StopMonitoringAsync(Guid ownerId);
    ValueTask StopAsync();
}

