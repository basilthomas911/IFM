using Microsoft.Extensions.Logging;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Contracts;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Serializers;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;


namespace TomasAI.IFM.Application.EventServices;

/// <summary>
/// Produces and streams status console events through NATS.
/// </summary>
/// <remarks>This class implements <see cref="IStatusConsoleEventProducer"/> and extends
/// <see cref="NatsActorProducer"/> to publish status console event types.</remarks>
public class StatusConsoleEventProducer : NatsActorProducer, IStatusConsoleEventProducer
{
    readonly NatsConnectionManager _connection;
    readonly INatsProducerOptions _options;
    /// <summary>
    /// status console event producer constrictor
    /// </summary>
    /// <param name="options"></param>
    /// <param name="logger"></param>
    public StatusConsoleEventProducer(INatsProducerOptions options, ILogger logger, NatsConnectionManager connection)
        : base(options, logger, connection)
    {
        _connection = connection;
        _options = options;
    }

    /// <summary>Publishes display-only console traffic to currently connected subscribers.</summary>
    public async ValueTask PublishConsoleAsync(TomasAI.IFM.Shared.StatusConsole.Events.StatusConsoleLoggedEvent message)
    {
        // Display-only live delivery: no stream storage, replay or render acknowledgement.
        var client = await _connection.GetClientAsync(_options.Url).ConfigureAwait(false);
        await client.PublishAsync(message.Subject.ToString(), message,
            serializer: NatsMessagePackSerializer<TomasAI.IFM.Shared.StatusConsole.Events.StatusConsoleLoggedEvent>.Default).ConfigureAwait(false);
    }
}
