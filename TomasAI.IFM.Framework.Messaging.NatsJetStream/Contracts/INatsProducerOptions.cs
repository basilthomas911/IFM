using System.Text.Json;

namespace TomasAI.IFM.Framework.Messaging.NatsJetStream.Contracts;

public interface INatsProducerOptions
{
    string Url { get; set; }
    string QueueGroup { get; set; }
    JsonSerializerOptions JsonSerializerOptions { get; set; }
}
