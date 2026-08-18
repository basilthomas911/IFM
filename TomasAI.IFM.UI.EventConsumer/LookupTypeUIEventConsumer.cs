using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Domain.Reference.Shared.Events;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.UI.EventConsumer;

/// <summary>
/// Delivers lookup-maintenance terminal events to one UI lifecycle owner.
/// </summary>
public sealed class LookupTypeUIEventConsumer(INatsEventListenerOptions options, ILogger logger)
    : NatsActorEventListener(options, logger), ILookupTypeUIEventConsumer
{
    const string EventConsumer = "LookupTypeUIEventConsumer";
    readonly ILogger _logger = logger;
    readonly Dictionary<ActorMailboxId, List<string>> _eventMap = new()
    {
        [new ActorMailboxId(ActorType.Event, LookupTypeAddedCompleteEvent.Actor)] =
        [
            LookupTypeAddedCompleteEvent.Verb,
            LookupTypeAddedFailEvent.Verb,
            LookupTypeChangedCompleteEvent.Verb,
            LookupTypeChangedFailEvent.Verb,
            LookupTypeRemovedCompleteEvent.Verb,
            LookupTypeRemovedFailEvent.Verb
        ]
    };

    public async ValueTask StartAsync(Func<IEvent, ValueTask> eventAction)
    {
        ArgumentNullException.ThrowIfNull(eventAction);
        await StartAsync(EventConsumer, _eventMap, EventHandlerAsync);

        async ValueTask EventHandlerAsync(string eventVerb, NatsMsg<byte[]> eventMessage)
        {
            try
            {
                IEvent? terminalEvent = eventVerb switch
                {
                    _ when eventVerb == LookupTypeAddedCompleteEvent.Verb
                        => eventMessage.AsEvent<LookupTypeAddedCompleteEvent>(),
                    _ when eventVerb == LookupTypeAddedFailEvent.Verb
                        => eventMessage.AsEvent<LookupTypeAddedFailEvent>(),
                    _ when eventVerb == LookupTypeChangedCompleteEvent.Verb
                        => eventMessage.AsEvent<LookupTypeChangedCompleteEvent>(),
                    _ when eventVerb == LookupTypeChangedFailEvent.Verb
                        => eventMessage.AsEvent<LookupTypeChangedFailEvent>(),
                    _ when eventVerb == LookupTypeRemovedCompleteEvent.Verb
                        => eventMessage.AsEvent<LookupTypeRemovedCompleteEvent>(),
                    _ when eventVerb == LookupTypeRemovedFailEvent.Verb
                        => eventMessage.AsEvent<LookupTypeRemovedFailEvent>(),
                    _ => null
                };
                if (terminalEvent is not null)
                    await eventAction(terminalEvent);
            }
            catch (Exception exception)
            {
                _logger.LogErrorEvent(
                    EventConsumer,
                    exception,
                    "EventHandlerAsync: failed while processing event verb: {EventVerb}",
                    eventVerb);
            }
        }
    }
}

public interface ILookupTypeUIEventConsumer
{
    ValueTask StartAsync(Func<IEvent, ValueTask> eventAction);
    ValueTask StopAsync();
}
