using Microsoft.Extensions.Logging;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Contracts;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;


namespace TomasAI.IFM.Shared.EventProducers;

/// <summary>
/// Produces and streams status console events to an event broker using Kafka.
/// </summary>
/// <remarks>This class is intended for publishing status console events to a Kafka event broker. It implements
/// the <see cref="IStatusConsoleEventProducer"/> interface and extends <see cref="KafkaEventProducer"/> to provide
/// specialized handling for status console event types. Thread safety and reliability depend on the underlying Kafka
/// producer implementation.</remarks>
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
