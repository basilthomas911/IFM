using Microsoft.Extensions.Logging;
using NATS.Client.JetStream;
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
    readonly System.Threading.Lock _streamInitializationLock = new();
    Task<INatsJSStream>? _streamInitialization;
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
        Task<INatsJSStream> initialization;
        lock (_streamInitializationLock)
        {
            // Stream policy/migration is startup work, not a configuration request per console row.
            if (_streamInitialization is null || _streamInitialization.IsFaulted || _streamInitialization.IsCanceled)
                _streamInitialization = StatusConsoleHistory.EnsureAsync(js).AsTask();
            initialization = _streamInitialization;
        }
        await initialization.ConfigureAwait(false);
        var acknowledgement = await js.PublishAsync(StatusConsoleHistory.Subject, message,
            headers: global::TomasAI.IFM.Framework.Messaging.Nats.NatsActorTrace.Headers(),
            serializer: TomasAI.IFM.Framework.Messaging.NatsJetStream.Serializers.NatsMessagePackSerializer<TomasAI.IFM.Shared.StatusConsole.Events.StatusConsoleLoggedEvent>.Default,
            opts: new NATS.Client.JetStream.NatsJSPubOpts { MsgId = message.Id.ToString() }).ConfigureAwait(false);
        acknowledgement.EnsureSuccess();
        // Compatibility broadcast is transient. Standalone uses only the durable console queue.
        var client = await _connection.GetClientAsync(_options.Url).ConfigureAwait(false);
        await client.PublishAsync(message.Subject.ToString(), message,
            serializer: NatsMessagePackSerializer<TomasAI.IFM.Shared.StatusConsole.Events.StatusConsoleLoggedEvent>.Default).ConfigureAwait(false);
    }
}
