namespace TomasAI.IFM.Framework.Messaging.Nats.Contracts;

/// <summary>
/// Represents the configuration options for a NATS JetStream consumer.
/// </summary>
/// <remarks>This interface defines the settings required to configure a JetStream consumer,
/// including the server URL, stream name, and durable consumer name.</remarks>
public interface INatsJetStreamConsumerOptions
{
    string Url { get; set; }
    string StreamName { get; set; }
    string DurableConsumerName { get; set; }

    /// <summary>
    /// Gets or sets the number of parallel dispatch stripes used by the consumer
    /// to route messages to actor mailboxes concurrently while preserving per-entity ordering.
    /// </summary>
    /// <remarks>Defaults to 4. Values less than 1 are clamped to 1.</remarks>
    int DispatcherCount { get; set; }
}
