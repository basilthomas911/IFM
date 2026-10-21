using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
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
    readonly ConcurrentDictionary<Guid, TradePositionUIEventConsumer> _realtimeListeners = new();
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
        if (_realtimeListeners.TryRemove(ownerId, out var realtime))
            await realtime.StopAsync().ConfigureAwait(false);
    }

    public async ValueTask StartRealtimeAsync(Guid ownerId, Func<TomasAI.IFM.Domain.Trade.Shared.Trade.Realtime.TradeRealtimeSnapshotChangedEvent, ValueTask> eventAction, Func<ValueTask>? reconnected = null)
    {
        if (ownerId == Guid.Empty) throw new ArgumentException("A monitoring owner is required.", nameof(ownerId));
        var listener = new TradePositionUIEventConsumer(_monitoringOptions, _logger) { Reconnected = reconnected };
        if (!_realtimeListeners.TryAdd(ownerId, listener)) throw new InvalidOperationException("The monitoring owner already has a realtime listener.");
        try
        {
            await listener.StartAsync($"{EventConsumer}.Realtime.{ownerId:N}", new Dictionary<ActorMailboxId, List<string>>
            {
                [new(ActorType.Notify, TomasAI.IFM.Domain.Trade.Shared.Trade.Realtime.TradeRealtimeSnapshotChangedEvent.Actor)] =
                    [TomasAI.IFM.Domain.Trade.Shared.Trade.Realtime.TradeRealtimeSnapshotChangedEvent.Verb]
            }, async (_, message) => await eventAction(message.AsEvent<TomasAI.IFM.Domain.Trade.Shared.Trade.Realtime.TradeRealtimeSnapshotChangedEvent>()!).ConfigureAwait(false)).ConfigureAwait(false);
        }
        catch { _realtimeListeners.TryRemove(ownerId, out _); await listener.StopAsync().ConfigureAwait(false); throw; }
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
