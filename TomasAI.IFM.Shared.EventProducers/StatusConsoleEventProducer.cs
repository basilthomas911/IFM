using Microsoft.Extensions.Logging;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Contracts;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;


namespace TomasAI.IFM.Shared.EventProducers;

/// <summary>
/// Produces and streams status console events through NATS.
/// </summary>
/// <remarks>This class implements <see cref="IStatusConsoleEventProducer"/> and extends
/// <see cref="NatsActorProducer"/> to publish status console event types.</remarks>
public class StatusConsoleEventProducer :NatsActorProducer, IStatusConsoleEventProducer
{
    /// <summary>
    /// status console event producer constrictor
    /// </summary>
    /// <param name="options"></param>
    /// <param name="logger"></param>
    public StatusConsoleEventProducer(INatsProducerOptions options, ILogger logger)
        :base(options, logger)
    {
    }

}
