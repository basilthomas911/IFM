using System.Text.Json;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Framework.Messaging.NatsJetStream.Contracts;

/// <summary>
/// Represents the configuration options for an NATS consumer.
/// </summary>
/// <remarks>This interface defines the settings required to configure a consumer for connecting to a NATS server
/// and handling message serialization.</remarks>
public interface INatsConsumerOptions
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1056", Justification = "Preserve the published interface/configuration contract used by actor callers, named arguments, and serialized settings.")]
    string Url { get; set; }
    JsonSerializerOptions JsonSerializerOptions { get; set; }

    /// <summary>
    /// Gets or sets the number of parallel dispatch stripes used by the consumer
    /// to route messages to actor mailboxes concurrently while preserving per-entity ordering.
    /// </summary>
    /// <remarks>Defaults to 4. Values less than 1 are clamped to 1.</remarks>
    int DispatcherCount { get; set; }

    /// <summary>Gets or sets the bounded capacity of each dispatch stripe.</summary>
    int DispatcherCapacity { get; set; }

    /// <summary>
    /// Gets or sets the Core NATS subscription channel capacity. Zero derives it from dispatcher count and capacity.
    /// </summary>
    int SubscriptionCapacity { get; set; }

    /// <summary>
    /// Gets or sets whether command ingress retains NATS pooled payload ownership
    /// through actor deserialization. Disable only for controlled A/B diagnostics.
    /// </summary>
    bool UseOwnedCommandPayloads { get; set; }

    /// <summary>
    /// Gets or sets whether query ingress retains NATS pooled payload ownership
    /// through actor deserialization. Disable only for controlled A/B diagnostics.
    /// </summary>
    bool UseOwnedQueryPayloads { get; set; }

    /// <summary>
    /// Gets or sets whether realtime event ingress retains one shared NATS pooled payload
    /// across all actor fan-out branches. Disable only for controlled A/B diagnostics.
    /// </summary>
    bool UseOwnedRealtimePayloads { get; set; }

    /// <summary>
    /// Gets or sets the explicit loss/retry contract for messages without a
    /// reply subject, keyed by actor type.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA2227", Justification = "Preserve the existing serialized message/configuration contract, including enum widths, numeric values, and collection shape.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0016", Justification = "Preserve the published interface/configuration contract used by actor callers, named arguments, and serialized settings.")]
    Dictionary<ActorType, CoreNatsTrafficClass> FireAndForgetTraffic { get; set; }
}
