using Microsoft.Extensions.Logging;
using NATS.Client.JetStream;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Contracts;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;


namespace TomasAI.IFM.Shared.EventProducers;

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

    /// <summary>JetStream stores and broadcasts the same event; no UI listener is required.</summary>
    public async ValueTask PublishConsoleAsync(TomasAI.IFM.Shared.StatusConsole.Events.StatusConsoleLoggedEvent message)
    {
        var js = await _connection.GetJetStreamContextAsync(_options.Url).ConfigureAwait(false);
        await StatusConsoleHistory.EnsureAsync(js).ConfigureAwait(false);
        var acknowledgement = await js.PublishAsync(message.Subject.ToString(), message,
            headers: TomasAI.IFM.Shared.EventModelActor.ActorTrace.Headers(),
            serializer: TomasAI.IFM.Framework.Messaging.NatsJetStream.Serializers.NatsMessagePackSerializer<TomasAI.IFM.Shared.StatusConsole.Events.StatusConsoleLoggedEvent>.Default,
            opts: new NATS.Client.JetStream.NatsJSPubOpts { MsgId = message.Id.ToString() }).ConfigureAwait(false);
        acknowledgement.EnsureSuccess();
    }
}
