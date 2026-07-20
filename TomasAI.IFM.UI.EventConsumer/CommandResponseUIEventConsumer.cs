using Microsoft.Extensions.Logging;
using TomasAI.IFM.Framework.Messaging.Nats;
using TomasAI.IFM.Framework.Messaging.Nats.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.UI.EventConsumer;

/// <summary>
/// Represents a consumer for command response UI events, utilizing Kafka for event handling.
/// </summary>
/// <remarks>This class is responsible for subscribing to and processing command response events specific to a
/// site, identified by a unique site ID. It extends the functionality of <see cref="KafkaEventConsumer"/> and
/// implements <see cref="ICommandResponseUIEventConsumer"/>.</remarks>
/// <param name="options"></param>
/// <param name="logger"></param>
public class CommandResponseUIEventConsumer(INatsEventListenerOptions options, ILogger logger)
    : NatsActorEventListener(options, logger), ICommandResponseUIEventConsumer
{

    public async ValueTask StartAsync(ICollection<IEvent> commandResponseEvents, Action<IEvent> eventAction)
    {
        await ValueTask.CompletedTask;
    }

    public new async ValueTask StopAsync()
    {
        await base.StopAsync();
    }

}
