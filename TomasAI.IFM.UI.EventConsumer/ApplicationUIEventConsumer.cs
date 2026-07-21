using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Framework.Messaging.Kafka;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Contracts;
using TomasAI.IFM.Shared.Application.Events;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.UI.EventConsumer;

/// <summary>
/// Consumes application UI events and triggers corresponding actions.
/// </summary>
/// <remarks>This class listens for application startup and shutdown events and executes specified actions when
/// these events occur. It extends the <see cref="KafkaEventConsumer"/> to leverage Kafka for event subscription and
/// handling.</remarks>
public class ApplicationUIEventConsumer(INatsEventListenerOptions options, ILogger logger)
    : NatsActorEventListener(options, logger), IApplicationUIEventConsumer
{
    readonly static string EventConsumer = "ApplicationUIEventConsumer";
    readonly ILogger _logger = logger;
    Dictionary<string, Action<IEvent>> _eventActionMap = [];

    public async ValueTask StartAsync(
        Action<ApplicationStartupEvent> startupAction,
        Action<ApplicationShutdownEvent> shutdownAction)
    {
        _eventActionMap = new Dictionary<string, Action<IEvent>>
        {
            { nameof(ApplicationStartupEvent), e => startupAction?.Invoke((e as ApplicationStartupEvent)! )},
            { nameof(ApplicationShutdownEvent), e => shutdownAction?.Invoke((e as ApplicationShutdownEvent)! ) }
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
                    _ when eventVerb == ApplicationStartupEvent.Verb => HandleEvent(eventMsg.AsEvent<ApplicationStartupEvent>()!, nameof(ApplicationStartupEvent)),
                    _ when eventVerb == ApplicationShutdownEvent.Verb => HandleEvent(eventMsg.AsEvent<ApplicationShutdownEvent>()!, nameof(ApplicationShutdownEvent)),
                    _ => default!
                };
                await ValueTask.CompletedTask;
            }
            catch(Exception ex)
            {
                _logger.LogErrorEvent(EventConsumer, ex, "EventHandlerAsync: failed while processing event verb: {EventVerb}", eventVerb);
            }
        }

        IEvent HandleEvent(IEvent e, string eventName)
        {
            try
            {
                if (_eventActionMap.TryGetValue(eventName, out Action<IEvent>? value))
                    value?.Invoke(e);
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
        Action<ApplicationStartupEvent> startupAction,
        Action<ApplicationShutdownEvent> shutdownction);
    ValueTask StopAsync();
}

