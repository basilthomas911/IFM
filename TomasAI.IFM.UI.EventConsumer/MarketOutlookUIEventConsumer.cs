using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.UI.EventConsumer;

public sealed class MarketOutlookUIEventConsumer(
    INatsEventListenerOptions options,
    ILogger logger)
    : NatsActorEventListener(options, logger), IMarketOutlookUIEventConsumer
{
    const string ConsumerName = "MarketOutlookUIEventConsumer";
    readonly ILogger _logger = logger;
    readonly ConcurrentDictionary<Guid, Action<MarketOutlookUpdatedNotifyEvent>> _actions = new();
    readonly SemaphoreSlim _gate = new(1, 1);
    readonly Dictionary<ActorMailboxId, List<string>> _eventMap = new()
    {
        [new ActorMailboxId(ActorType.Notify, MarketOutlookUpdatedNotifyEvent.Actor)] =
            [MarketOutlookUpdatedNotifyEvent.Verb]
    };

    public async ValueTask StartAsync(
        Guid siteId,
        Action<MarketOutlookUpdatedNotifyEvent> action)
    {
        if (siteId == Guid.Empty)
            throw new ArgumentException("A non-empty UI site identifier is required.", nameof(siteId));
        ArgumentNullException.ThrowIfNull(action);
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            var start = _actions.IsEmpty;
            _actions[siteId] = action;
            if (start)
                await StartAsync(ConsumerName, _eventMap, HandleAsync).ConfigureAwait(false);
        }
        catch
        {
            _actions.TryRemove(siteId, out _);
            throw;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask StopAsync(Guid siteId)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            _actions.TryRemove(siteId, out _);
            if (_actions.IsEmpty)
                await base.StopAsync().ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    async ValueTask HandleAsync(string eventVerb, NatsMsg<byte[]> message)
    {
        try
        {
            if (eventVerb != MarketOutlookUpdatedNotifyEvent.Verb)
                return;
            var notification = message.AsEvent<MarketOutlookUpdatedNotifyEvent>();
            if (notification?.IsValid != true)
                return;
            foreach (var action in _actions.Values)
                action(notification);
        }
        catch (Exception exception)
        {
            _logger.LogErrorEvent(
                ConsumerName,
                exception,
                "Failed while processing {EventVerb}",
                eventVerb);
        }
        await ValueTask.CompletedTask;
    }
}

public interface IMarketOutlookUIEventConsumer
{
    ValueTask StartAsync(Guid siteId, Action<MarketOutlookUpdatedNotifyEvent> action);
    ValueTask StopAsync(Guid siteId);
}
