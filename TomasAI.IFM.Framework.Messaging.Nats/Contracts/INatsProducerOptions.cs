using System.Text.Json;

namespace TomasAI.IFM.Framework.Messaging.NatsJetStream.Contracts;

public interface INatsProducerOptions
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1056", Justification = "Preserve the published interface/configuration contract used by actor callers, named arguments, and serialized settings.")]
    string Url { get; set; }
    string QueueGroup { get; set; }
    JsonSerializerOptions JsonSerializerOptions { get; set; }
}
