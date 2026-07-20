using System.Text.Json;

namespace TomasAI.IFM.Framework.Messaging.Nats.Contracts;

/// <summary>
/// Represents the configuration options for an NATS consumer.
/// </summary>
/// <remarks>This interface defines the settings required to configure a consumer for connecting to a NATS server
/// and handling message serialization.</remarks>
public interface INatsConsumerOptions
{
    string Url { get; set; }
    JsonSerializerOptions JsonSerializerOptions { get; set; }

    /// <summary>
    /// Gets or sets the number of parallel dispatch stripes used by the consumer
    /// to route messages to actor mailboxes concurrently while preserving per-entity ordering.
    /// </summary>
    /// <remarks>Defaults to 4. Values less than 1 are clamped to 1.</remarks>
    int DispatcherCount { get; set; }
}
