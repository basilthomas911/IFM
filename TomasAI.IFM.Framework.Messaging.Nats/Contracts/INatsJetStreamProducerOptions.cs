using System.Text.Json;
using NATS.Client.JetStream;

namespace TomasAI.IFM.Framework.Messaging.NatsJetStream.Contracts;

/// <summary>
/// Represents the configuration options for a NATS JetStream producer.
/// </summary>
/// <remarks>This interface defines the settings required to configure a JetStream producer,
/// including the JetStream context, subject prefix, and JSON serialization options.</remarks>
public interface INatsJetStreamProducerOptions
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1056", Justification = "Preserve the published interface/configuration contract used by actor callers, named arguments, and serialized settings.")]
    string Url { get; set; }
    string SubjectPrefix { get; set; }
    JsonSerializerOptions JsonSerializerOptions { get; set; }
}
