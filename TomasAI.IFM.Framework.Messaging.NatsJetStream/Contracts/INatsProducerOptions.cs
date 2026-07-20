using System.Text.Json;

namespace TomasAI.IFM.Framework.Messaging.Nats.Contracts;

public interface INatsProducerOptions
{
    string Url { get; set; }
    string QueueGroup { get; set; }
    JsonSerializerOptions JsonSerializerOptions { get; set; }
}
