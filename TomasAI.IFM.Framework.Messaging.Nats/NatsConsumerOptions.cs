using System.Text.Json;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Contracts;

namespace TomasAI.IFM.Framework.Messaging.NatsJetStream;

/// <summary>
/// Represents configuration options for a NATS consumer.
/// </summary>
/// <remarks>This class provides options for connecting to a NATS server and configuring message
/// serialization.</remarks>
public class NatsConsumerOptions : INatsConsumerOptions
{
    public const string SectionName = "Nats:Consumer";
    public const int ExistingDispatcherCapacity = 4096;
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

    /// <inheritdoc />
    public int DispatcherCapacity { get; set; } = ExistingDispatcherCapacity;

    /// <inheritdoc />
    public int SubscriptionCapacity { get; set; }

    /// <inheritdoc />
    public bool UseOwnedCommandPayloads { get; set; } = true;

    /// <inheritdoc />
    public bool UseOwnedQueryPayloads { get; set; } = true;

    public int GetSubscriptionCapacity()
        => SubscriptionCapacity > 0
            ? SubscriptionCapacity
            : checked(DispatcherCapacity * DispatcherCount);

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Url))
            throw new InvalidOperationException($"{nameof(Url)} is required.");
        if (DispatcherCount <= 0)
            throw new InvalidOperationException($"{nameof(DispatcherCount)} must be greater than zero.");
        if (DispatcherCapacity <= 0)
            throw new InvalidOperationException($"{nameof(DispatcherCapacity)} must be greater than zero.");
        if (SubscriptionCapacity < 0)
            throw new InvalidOperationException($"{nameof(SubscriptionCapacity)} cannot be negative.");
        _ = GetSubscriptionCapacity();
    }
}
