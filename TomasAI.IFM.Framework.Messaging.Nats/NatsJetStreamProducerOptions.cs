using System.Text.Json;
using NATS.Client.JetStream;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Contracts;

namespace TomasAI.IFM.Framework.Messaging.NatsJetStream;

/// <summary>
/// Represents the configuration options for a NATS JetStream producer.
/// </summary>
public class NatsJetStreamProducerOptions : INatsJetStreamProducerOptions
{
    /// <summary>
    /// The NATS server URL to connect to. Defaults to "nats://localhost:4222".
    /// </summary>
    public string Url { get; set; } = "nats://localhost:4222";

    /// <summary>
    /// The subject prefix prepended to the mailbox name when publishing messages.
    /// </summary>
    public string SubjectPrefix { get; set; } = string.Empty;

    /// <summary>
    /// JSON serializer options used to (de)serialize message payloads.
    /// </summary>
    public JsonSerializerOptions JsonSerializerOptions { get; set; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };
}
