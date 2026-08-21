using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.UI.EventConsumer;

/// <summary>
/// Delivers every valid, successfully persisted Futures ITI change to independently owned UI subscribers.
/// </summary>
public sealed class FuturesItiSignalUIEventConsumer(INatsEventListenerOptions options, ILogger logger)
    : NatsActorEventListener(options, logger), IFuturesItiSignalUIEventConsumer
{
    const string EventConsumer = nameof(FuturesItiSignalUIEventConsumer);
    readonly ILogger _logger = logger;
    readonly ConcurrentDictionary<Guid, Action<FuturesItiSignalUpdatedNotifyEvent>> _eventActions = new();
    readonly SemaphoreSlim _subscriberGate = new(1, 1);
    readonly Dictionary<ActorMailboxId, List<string>> _eventMap = new()
    {
        [new(ActorType.Notify, FuturesItiSignalUpdatedNotifyEvent.Actor)] =
            [FuturesItiSignalUpdatedNotifyEvent.Verb]
    };

    public async ValueTask StartAsync(
        Guid siteId,
        Action<FuturesItiSignalUpdatedNotifyEvent> eventAction)
    {
        await _subscriberGate.WaitAsync().ConfigureAwait(false);
        try
        {
            var startListener = AddSubscriber(siteId, eventAction);
            if (!startListener)
                return;

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
            if (RemoveSubscriber(siteId))
                await base.StopAsync().ConfigureAwait(false);
        }
        finally
        {
            _subscriberGate.Release();
        }
    }

    async ValueTask EventHandlerAsync(string eventVerb, NatsMsg<byte[]> eventMessage)
    {
        try
        {
            if (eventVerb != FuturesItiSignalUpdatedNotifyEvent.Verb)
                return;

            var notification = eventMessage.AsEvent<FuturesItiSignalUpdatedNotifyEvent>();
            if (notification is not null)
                Dispatch(notification);
        }
        catch (Exception exception)
        {
            _logger.LogErrorEvent(
                EventConsumer,
                exception,
                "Failed while processing Futures ITI event verb {EventVerb}",
                eventVerb);
        }

        await ValueTask.CompletedTask;
    }

    internal void Dispatch(FuturesItiSignalUpdatedNotifyEvent notification)
    {
        if (!notification.IsValid)
            return;

        foreach (var eventAction in _eventActions.Values)
        {
            try
            {
                eventAction(notification);
            }
            catch (Exception exception)
            {
                _logger.LogErrorEvent(
                    EventConsumer,
                    exception,
                    "A Futures ITI UI subscriber failed while processing {EventVerb}",
                    FuturesItiSignalUpdatedNotifyEvent.Verb);
            }
        }
    }

    internal bool AddSubscriber(
        Guid siteId,
        Action<FuturesItiSignalUpdatedNotifyEvent> eventAction)
    {
        if (siteId == Guid.Empty)
            throw new ArgumentException("A non-empty UI site identifier is required.", nameof(siteId));
        ArgumentNullException.ThrowIfNull(eventAction);

        var startListener = _eventActions.IsEmpty;
        _eventActions[siteId] = eventAction;
        return startListener;
    }

    internal bool RemoveSubscriber(Guid siteId)
    {
        _eventActions.TryRemove(siteId, out _);
        return _eventActions.IsEmpty;
    }
}
