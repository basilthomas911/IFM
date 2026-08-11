using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Contracts;
using TomasAI.IFM.Domain.Application.Shared.Events;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.UI.EventConsumer;

/// <summary>
/// Consumes application UI events and triggers corresponding actions.
/// </summary>
/// <remarks>This class listens for application startup and shutdown events and executes specified actions when
/// these events occur. It extends <see cref="NatsActorEventListener"/> to receive events from NATS.</remarks>
public class ApplicationUIEventConsumer(INatsEventListenerOptions options, ILogger logger)
    : NatsActorEventListener(options, logger), IApplicationUIEventConsumer
{
    readonly static string EventConsumer = "ApplicationUIEventConsumer";
    readonly ILogger _logger = logger;
    Dictionary<string, Func<IEvent, ValueTask>> _eventActionMap = [];

    public async ValueTask StartAsync(
        Func<ApplicationStartupEvent, ValueTask> startupAction,
        Func<ApplicationShutdownEvent, ValueTask> shutdownAction)
    {
        _eventActionMap = new Dictionary<string, Func<IEvent, ValueTask>>
        {
            { nameof(ApplicationStartupEvent), e => startupAction((ApplicationStartupEvent)e) },
            { nameof(ApplicationShutdownEvent), e => shutdownAction((ApplicationShutdownEvent)e) }
        };
        await StartAsync(
           EventConsumer,
           new()
           {
               [new ActorMailboxId(ActorType.Event, ApplicationStartupEvent.Actor)] = [ApplicationStartupEvent.Verb, ApplicationShutdownEvent.Verb]
           },
           EventHandlerAsync
       );

        async ValueTask EventHandlerAsync(string eventVerb, NatsMsg<byte[]> eventMsg)
        {
            try
            {
                _ = eventVerb switch
                {
                    _ when eventVerb == ApplicationStartupEvent.Verb => await HandleEventAsync(eventMsg.AsEvent<ApplicationStartupEvent>()!, nameof(ApplicationStartupEvent)),
                    _ when eventVerb == ApplicationShutdownEvent.Verb => await HandleEventAsync(eventMsg.AsEvent<ApplicationShutdownEvent>()!, nameof(ApplicationShutdownEvent)),
                    _ => default!
                };
            }
            catch(Exception ex)
            {
                _logger.LogErrorEvent(EventConsumer, ex, "EventHandlerAsync: failed while processing event verb: {EventVerb}", eventVerb);
            }
        }

        async ValueTask<IEvent> HandleEventAsync(IEvent e, string eventName)
        {
            try
            {
                if (_eventActionMap.TryGetValue(eventName, out var value))
                    await value(e);
            }
            catch (Exception ex)
            {
                _logger.LogErrorEvent(EventConsumer, ex, "HandleEvent: failed while processing event: {EventName}", eventName);
            }
            return e;
        }
    }
}

public interface IApplicationUIEventConsumer
{
    ValueTask StartAsync(
        Func<ApplicationStartupEvent, ValueTask> startupAction,
        Func<ApplicationShutdownEvent, ValueTask> shutdownAction);
    ValueTask StopAsync();
}

