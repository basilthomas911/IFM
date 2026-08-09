using TomasAI.IFM.Framework.Messaging.NatsJetStream.Contracts;

namespace TomasAI.IFM.Framework.Messaging.NatsJetStream;

/// <summary>
/// Represents the configuration options for a NATS JetStream consumer.
/// </summary>
public class NatsJetStreamConsumerOptions : INatsJetStreamConsumerOptions
{
    public const string SectionName = "Nats:JetStreamConsumer";
    public const int ExistingDispatcherCapacity = 4096;
    /// <summary>
    /// The NATS server URL to connect to. Defaults to "nats://localhost:4222".
    /// </summary>
    public string Url { get; set; } = "nats://localhost:4222";

    /// <summary>
    /// The JetStream stream name to consume from.
    /// </summary>
    public string StreamName { get; set; } = string.Empty;

    /// <summary>
    /// The durable consumer name for JetStream.
    /// </summary>
    public string DurableConsumerName { get; set; } = string.Empty;

    /// <inheritdoc />
    public string FilterSubject { get; set; } = string.Empty;

    /// <inheritdoc />
    public int DispatcherCount { get; set; } = 4;

    /// <inheritdoc />
    public int DispatcherCapacity { get; set; } = ExistingDispatcherCapacity;

    /// <inheritdoc />
    public int MaxAckPending { get; set; }

    /// <inheritdoc />
    public int MaxMessages { get; set; }

    /// <inheritdoc />
    public int ThresholdMessages { get; set; }

    /// <inheritdoc />
    public bool UseOwnedEventPayloads { get; set; } = true;

    public int GetOutstandingLimit()
        => MaxAckPending > 0
            ? MaxAckPending
            : checked(DispatcherCapacity * DispatcherCount);

    public int GetMaxMessages() => MaxMessages > 0 ? MaxMessages : GetOutstandingLimit();

    public int GetThresholdMessages() => ThresholdMessages > 0 ? ThresholdMessages : DispatcherCapacity;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Url))
            throw new InvalidOperationException($"{nameof(Url)} is required.");
        if (DispatcherCount <= 0)
            throw new InvalidOperationException($"{nameof(DispatcherCount)} must be greater than zero.");
        if (DispatcherCapacity <= 0)
            throw new InvalidOperationException($"{nameof(DispatcherCapacity)} must be greater than zero.");
        if (MaxAckPending < 0 || MaxMessages < 0 || ThresholdMessages < 0)
            throw new InvalidOperationException("JetStream outstanding limits cannot be negative.");

        var outstanding = GetOutstandingLimit();
        if (GetMaxMessages() > outstanding)
            throw new InvalidOperationException($"{nameof(MaxMessages)} cannot exceed {nameof(MaxAckPending)}.");
        if (GetThresholdMessages() > GetMaxMessages())
            throw new InvalidOperationException($"{nameof(ThresholdMessages)} cannot exceed {nameof(MaxMessages)}.");
    }
}
