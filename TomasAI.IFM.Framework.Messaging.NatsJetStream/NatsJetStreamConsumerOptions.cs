using TomasAI.IFM.Framework.Messaging.Nats.Contracts;

namespace TomasAI.IFM.Framework.Messaging.Nats;

/// <summary>
/// Represents the configuration options for a NATS JetStream consumer.
/// </summary>
public class NatsJetStreamConsumerOptions : INatsJetStreamConsumerOptions
{
    /// <summary>
    /// The NATS server URL to connect to. Defaults to "nats://localhost:4222".
    /// </summary>
    public string Url { get; set; } = "nats://localhost:4222";

    /// <summary>
    /// The JetStream stream name to consume from.
    /// </summary>
    public string StreamName { get; set; } = string.Empty;

    /// <summary>
    /// The durable consumer name for JetStream.
    /// </summary>
    public string DurableConsumerName { get; set; } = string.Empty;

    /// <inheritdoc />
    public int DispatcherCount { get; set; } = 4;
}
