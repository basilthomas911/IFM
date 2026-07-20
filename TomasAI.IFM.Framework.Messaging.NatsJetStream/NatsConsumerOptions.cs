using System.Text.Json;
using TomasAI.IFM.Framework.Messaging.Nats.Contracts;

namespace TomasAI.IFM.Framework.Messaging.Nats;

/// <summary>
/// Represents configuration options for a NATS consumer.
/// </summary>
/// <remarks>This class provides options for connecting to a NATS server and configuring message
/// serialization.</remarks>
public class NatsConsumerOptions : INatsConsumerOptions
{
    /// <summary>
    /// The NATS server URL to connect to. Defaults to "nats://localhost:4222".
    /// </summary>
    public string Url { get; set; } = "nats://localhost:4222";

    /// <summary>
    /// Json serializer options used to (de)serialize message payloads.
    /// </summary>
    public JsonSerializerOptions JsonSerializerOptions { get; set; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    /// <inheritdoc />
    public int DispatcherCount { get; set; } = 4;
}
