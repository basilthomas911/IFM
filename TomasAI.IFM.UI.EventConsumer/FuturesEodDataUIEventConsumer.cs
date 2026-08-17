using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;

namespace TomasAI.IFM.UI.EventConsumer;

public class FuturesEodDataUIEventConsumer(INatsEventListenerOptions options, ILogger logger)
    : NatsActorEventListener(options, logger), IFuturesEodDataUIEventConsumer
{
    readonly static string EventConsumer = "FuturesEodDataUIEventConsumer";
    readonly ILogger _logger = logger;
    readonly ConcurrentDictionary<Guid, Action<FuturesEodDataUpdatedNotifyEvent>> _eventActions = new();
    readonly SemaphoreSlim _subscriberGate = new(1, 1);
    readonly Dictionary<ActorMailboxId, List<string>> _eventMap = new()
    {
        [new ActorMailboxId(ActorType.Notify, FuturesEodDataUpdatedNotifyEvent.Actor)] = [FuturesEodDataUpdatedNotifyEvent.Verb]
    };

    public async ValueTask StartAsync(
        Guid siteId,
        Action<FuturesEodDataUpdatedNotifyEvent> eventAction)
    {
        if (siteId == Guid.Empty)
            throw new ArgumentException("A non-empty UI site identifier is required.", nameof(siteId));
        ArgumentNullException.ThrowIfNull(eventAction);

        await _subscriberGate.WaitAsync().ConfigureAwait(false);
        try
        {
            var startListener = _eventActions.IsEmpty;
            _eventActions[siteId] = eventAction;
            if (startListener)
            {
                try
                {
                    await StartAsync(EventConsumer, _eventMap, EventHandlerAsync).ConfigureAwait(false);
                }
                catch
                {
                    _eventActions.TryRemove(siteId, out _);
                    throw;
                }
            }
        }
        finally
        {
            _subscriberGate.Release();
        }
    }

    public async ValueTask StopAsync(Guid siteId)
    {
        if (siteId == Guid.Empty)
            return;

        await _subscriberGate.WaitAsync().ConfigureAwait(false);
        try
        {
            _eventActions.TryRemove(siteId, out _);
            if (_eventActions.IsEmpty)
                await base.StopAsync().ConfigureAwait(false);
        }
        finally
        {
            _subscriberGate.Release();
        }
    }

    async ValueTask EventHandlerAsync(string eventVerb, NatsMsg<byte[]> eventMsg)
    {
        try
        {
            if (eventVerb != FuturesEodDataUpdatedNotifyEvent.Verb)
                return;
            var notification = eventMsg.AsEvent<FuturesEodDataUpdatedNotifyEvent>()!;
            if (!notification.IsValid)
                return;

            foreach (var eventAction in _eventActions.Values)
            {
                try
                {
                    eventAction.Invoke(notification);
                }
                catch (Exception exception)
                {
                    _logger.LogErrorEvent(
                        EventConsumer,
                        exception,
                        "A futures EOD UI subscriber failed while processing {EventVerb}",
                        eventVerb);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogErrorEvent(
                EventConsumer,
                ex,
                "EventHandlerAsync: failed while processing event verb: {EventVerb}",
                eventVerb);
        }
        await ValueTask.CompletedTask;
    }
}

public interface IFuturesEodDataUIEventConsumer
{
    ValueTask StartAsync(Guid siteId, Action<FuturesEodDataUpdatedNotifyEvent> eventAction);
    ValueTask StopAsync(Guid siteId);
}
