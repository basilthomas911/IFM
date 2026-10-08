using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Trade.Shared.Events;
using TomasAI.IFM.Shared.Extensions;

using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;

namespace TomasAI.IFM.UI.EventConsumer;

public class TradePlanUIEventConsumer(INatsEventListenerOptions options, ILogger logger)
    : NatsActorEventListener(options, logger), ITradePlanUIEventConsumer
{
    readonly static string EventConsumer = "TradePlanUIEventConsumer";
    readonly ILogger _logger = logger;
    readonly INatsEventListenerOptions _monitoringOptions = options;
    readonly ConcurrentDictionary<Guid, TradePlanUIEventConsumer> _monitoringListeners = new();
    string _listenerName = EventConsumer;

    /// <summary>Starts an independent monitoring listener owned by one trade view.</summary>
    /// <param name="ownerId">The view subscription identity.</param>
    /// <returns>The listener registration operation.</returns>
    public async ValueTask StartIronCondorAsync(Guid ownerId, Func<IronCondorTradePlanUpdatedEvent, ValueTask> eventAction)
    {
        if (ownerId == Guid.Empty) throw new ArgumentException("A monitoring owner is required.", nameof(ownerId));
        var listener = new TradePlanUIEventConsumer(_monitoringOptions, _logger) { _listenerName = $"{EventConsumer}.{ownerId:N}" };
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
        [new ActorMailboxId(ActorType.Event, TradePlanUpdatedEvent.Actor)] = [TradePlanUpdatedEvent.Verb]
    };

    public async ValueTask StartAsync(Func<TradePlanUpdatedEvent, ValueTask> eventAction)
    {
        await StartAsync(_listenerName, _eventMap, EventHandlerAsync);

        async ValueTask EventHandlerAsync(string eventVerb, NatsMsg<byte[]> eventMsg)
        {
            try
            {
                await (eventVerb switch
                {
                    _ when eventVerb == TradePlanUpdatedEvent.Verb
                        => HandleEventAsync(eventMsg.AsEvent<TradePlanUpdatedEvent>()!, eventAction),
                    _ => ValueTask.CompletedTask
                });
            }
            catch (Exception ex)
            {
                _logger.LogErrorEvent(EventConsumer, ex, "EventHandlerAsync: failed while processing event verb: {EventVerb}", eventVerb);
            }

            static ValueTask HandleEventAsync(
                TradePlanUpdatedEvent e,
                Func<TradePlanUpdatedEvent, ValueTask> eventAction)
                => eventAction is null ? ValueTask.CompletedTask : eventAction(e);
        }

    }


    /// <summary>Listens to current Iron Condor events using the persisted position and plan contracts.</summary>
    /// <param name="eventAction">The read-only monitoring callback.</param>
    /// <returns>The listener startup operation.</returns>
    public ValueTask StartIronCondorAsync(Func<IronCondorTradePlanUpdatedEvent, ValueTask> eventAction)
        => StartAsync(_listenerName, new Dictionary<ActorMailboxId, List<string>>
        {
            [new ActorMailboxId(ActorType.Event, UpdateIronCondorTradePlanCommand.Actor)] = [IronCondorTradePlanUpdatedEvent.Verb]
        }, async (eventVerb, eventMsg) =>
        {
            try
            {
                if (eventVerb == IronCondorTradePlanUpdatedEvent.Verb)
                    await eventAction(eventMsg.AsEvent<IronCondorTradePlanUpdatedEvent>()!).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "{MethodName} failed. EventVerb={EventVerb}",
                    nameof(StartIronCondorAsync), eventVerb);
            }
        });
}
