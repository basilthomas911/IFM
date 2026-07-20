using System.Text.Json;
using TomasAI.IFM.Framework.Messaging.Nats.Contracts;

namespace TomasAI.IFM.Framework.Messaging.Nats;

/// <summary>
/// Represents the configuration options for a NATS producer.
/// </summary>
/// <remarks>This class provides settings for configuring the behavior of a NATS producer,  including
/// the connection URL and JSON serialization options.</remarks>
public class NatsProducerOptions: INatsProducerOptions
{

    /// <summary>
    /// Gets or sets the URL associated with the current instance.
    /// </summary>
    public string Url { get; set; } = "nats://localhost:4222";

    /// <summary>
    /// Optional queue group name for shared consumer delivery semantics.
    /// </summary>
    public string QueueGroup { get; set; } = string.Empty;

    public JsonSerializerOptions JsonSerializerOptions { get; set; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

}
