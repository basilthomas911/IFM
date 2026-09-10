using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.UI.EventConsumer;

/// <summary>Fans projected Strategy Workflow snapshots out to independently owned UI subscribers.</summary>
public sealed class IntrinsicTimeStrategyWorkflowUIEventConsumer(
    INatsEventListenerOptions options,
    ILogger logger)
    : NatsActorEventListener(options, logger), IIntrinsicTimeStrategyWorkflowUIEventConsumer
{
    const string EventConsumer = nameof(IntrinsicTimeStrategyWorkflowUIEventConsumer);
    readonly ILogger _logger = logger;
    readonly ConcurrentDictionary<Guid, Action<IntrinsicTimeStrategyWorkflowUpdatedNotifyEvent>> _eventActions = new();
    readonly SemaphoreSlim _subscriberGate = new(1, 1);
    readonly Dictionary<ActorMailboxId, List<string>> _eventMap = new()
    {
        [new(ActorType.Notify, IntrinsicTimeStrategyWorkflowUpdatedNotifyEvent.Actor)] =
            [IntrinsicTimeStrategyWorkflowUpdatedNotifyEvent.Verb]
    };

    public async ValueTask StartAsync(
        Guid siteId,
        Action<IntrinsicTimeStrategyWorkflowUpdatedNotifyEvent> eventAction)
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
            if (eventVerb != IntrinsicTimeStrategyWorkflowUpdatedNotifyEvent.Verb)
                return;

            var notification = eventMessage.AsEvent<IntrinsicTimeStrategyWorkflowUpdatedNotifyEvent>();
            if (notification is not null)
                Dispatch(notification);
        }
        catch (Exception exception)
        {
            _logger.LogErrorEvent(
                EventConsumer,
                exception,
                "Failed while processing Strategy Workflow event verb {EventVerb}",
                eventVerb);
        }

        await ValueTask.CompletedTask;
    }

    internal void Dispatch(IntrinsicTimeStrategyWorkflowUpdatedNotifyEvent notification)
    {
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
                    "A Strategy Workflow UI subscriber failed while processing {EventVerb}",
                    IntrinsicTimeStrategyWorkflowUpdatedNotifyEvent.Verb);
            }
        }
    }

    internal bool AddSubscriber(
        Guid siteId,
        Action<IntrinsicTimeStrategyWorkflowUpdatedNotifyEvent> eventAction)
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
