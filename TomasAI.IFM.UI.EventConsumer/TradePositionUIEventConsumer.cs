using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Trade.Shared.Events;
using TomasAI.IFM.Shared.Extensions;

using TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position;

namespace TomasAI.IFM.UI.EventConsumer;

public class TradePositionUIEventConsumer(INatsEventListenerOptions options, ILogger logger)
    : NatsActorEventListener(options, logger), ITradePositionUIEventConsumer
{
    readonly static string EventConsumer = "TradePositionUIEventConsumer";
    readonly ILogger _logger = logger;
    readonly INatsEventListenerOptions _monitoringOptions = options;
    readonly ConcurrentDictionary<Guid, TradePositionUIEventConsumer> _monitoringListeners = new();
    string _listenerName = EventConsumer;

    /// <summary>Starts an independent monitoring listener owned by one trade view.</summary>
    /// <param name="ownerId">The view subscription identity.</param>
    /// <returns>The listener registration operation.</returns>
    public async ValueTask StartIronCondorAsync(Guid ownerId, Func<IronCondorPositionChangedEvent, ValueTask> eventAction)
    {
        if (ownerId == Guid.Empty) throw new ArgumentException("A monitoring owner is required.", nameof(ownerId));
        var listener = new TradePositionUIEventConsumer(_monitoringOptions, _logger) { _listenerName = $"{EventConsumer}.{ownerId:N}" };
        if (!_monitoringListeners.TryAdd(ownerId, listener)) throw new InvalidOperationException("The monitoring owner already has a listener.");
        try { await listener.StartIronCondorAsync(eventAction).ConfigureAwait(false); }
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
        [new ActorMailboxId(ActorType.Event, TradePositionUpdatedEvent.Actor)] = [TradePositionUpdatedEvent.Verb]
    };

    /// <summary>
    /// start event consumer
    /// </summary>
    /// <param name="eventAction"></param>
    public async ValueTask StartAsync(Action<TradePositionUpdatedEvent> eventAction)
    {
        await StartAsync(_listenerName, _eventMap, EventHandlerAsync);

        async ValueTask EventHandlerAsync(string eventVerb, NatsMsg<byte[]> eventMsg)
        {
            try
            {
                _ = eventVerb switch
                {
                    _ when eventVerb == TradePositionUpdatedEvent.Verb => HandleEvent(eventMsg.AsEvent<TradePositionUpdatedEvent>()!, eventAction),
                    _ => default!
                };
                await ValueTask.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogErrorEvent(EventConsumer, ex, "EventHandlerAsync: failed while processing event verb: {EventVerb}", eventVerb);
            }

            IEvent HandleEvent(TradePositionUpdatedEvent e, Action<TradePositionUpdatedEvent> eventAction)
            {
                eventAction?.Invoke(e);
                return e;
            }
        }
    }


    /// <summary>Listens to current Iron Condor events using the persisted position and plan contracts.</summary>
    /// <param name="eventAction">The read-only monitoring callback.</param>
    /// <returns>The listener startup operation.</returns>
    public ValueTask StartIronCondorAsync(Func<IronCondorPositionChangedEvent, ValueTask> eventAction)
        => StartAsync(_listenerName, new Dictionary<ActorMailboxId, List<string>>
        {
            [new ActorMailboxId(ActorType.Event, "FuturesIronCondorTradePositionEvent")] = [IronCondorPositionChangedEvent.Verb]
        }, async (eventVerb, eventMsg) =>
        {
            try
            {
                if (eventVerb == IronCondorPositionChangedEvent.Verb)
                    await eventAction(eventMsg.AsEvent<IronCondorPositionChangedEvent>()!).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "{MethodName} failed. EventVerb={EventVerb}",
                    nameof(StartIronCondorAsync), eventVerb);
            }
        });
}
