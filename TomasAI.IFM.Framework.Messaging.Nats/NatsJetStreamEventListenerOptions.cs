using TomasAI.IFM.Framework.Messaging.NatsJetStream.Contracts;

namespace TomasAI.IFM.Framework.Messaging.NatsJetStream;

/// <summary>
/// Default configuration for <see cref="NatsJetStreamEventListener"/>.
/// </summary>
public sealed class NatsJetStreamEventListenerOptions : INatsJetStreamEventListenerOptions
{
    public const string SectionName = "Nats:JetStreamEventListener";

    public string Url { get; set; } = "nats://localhost:4222";
    public string StreamName { get; set; } = "IFM_EVENTS";
    public string DurableConsumerNamePrefix { get; set; } = "ifm-listener";
    public string FilterSubject { get; set; } = string.Empty;
    public NatsJetStreamEventDeliverPolicy DeliverPolicy { get; set; } = NatsJetStreamEventDeliverPolicy.All;
    public TimeSpan AckWait { get; set; } = TimeSpan.FromSeconds(30);
    public int MaxDeliver { get; set; } = -1;
    public int DispatcherCount { get; set; } = 1;
    public int DispatcherCapacity { get; set; } = 256;
    public int MaxAckPending { get; set; }
    public int MaxMessages { get; set; }
    public int ThresholdMessages { get; set; }
    public TimeSpan NegativeAcknowledgeDelay { get; set; } = TimeSpan.FromMilliseconds(250);

    public int GetOutstandingLimit() => MaxAckPending > 0
        ? MaxAckPending
        : checked(DispatcherCount * DispatcherCapacity);

    public int GetMaxMessages() => MaxMessages > 0 ? MaxMessages : GetOutstandingLimit();

    public int GetThresholdMessages() => ThresholdMessages > 0
        ? ThresholdMessages
        : Math.Min(DispatcherCapacity, GetMaxMessages());

    /// <summary>
    /// Validates bounds and NATS resource names before any connection is opened.
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Url))
            throw new InvalidOperationException($"{nameof(Url)} is required.");
        ValidateResourceName(StreamName, nameof(StreamName));
        ValidateResourceName(DurableConsumerNamePrefix, nameof(DurableConsumerNamePrefix));
        if (DispatcherCount <= 0 || DispatcherCapacity <= 0)
            throw new InvalidOperationException("Dispatcher count and capacity must be greater than zero.");
        if (MaxAckPending < 0 || MaxMessages < 0 || ThresholdMessages < 0)
            throw new InvalidOperationException("JetStream outstanding limits cannot be negative.");
        if (AckWait <= TimeSpan.Zero)
            throw new InvalidOperationException($"{nameof(AckWait)} must be greater than zero.");
        if (MaxDeliver == 0 || MaxDeliver < -1)
            throw new InvalidOperationException($"{nameof(MaxDeliver)} must be -1 or greater than zero.");
        if (NegativeAcknowledgeDelay < TimeSpan.Zero)
            throw new InvalidOperationException($"{nameof(NegativeAcknowledgeDelay)} cannot be negative.");
        if (!Enum.IsDefined(DeliverPolicy))
            throw new InvalidOperationException($"{nameof(DeliverPolicy)} is invalid.");
        if (GetMaxMessages() > GetOutstandingLimit())
            throw new InvalidOperationException($"{nameof(MaxMessages)} cannot exceed {nameof(MaxAckPending)}.");
        if (GetThresholdMessages() > GetMaxMessages())
            throw new InvalidOperationException($"{nameof(ThresholdMessages)} cannot exceed {nameof(MaxMessages)}.");
        if (!string.IsNullOrWhiteSpace(FilterSubject))
            ValidateSubject(FilterSubject, nameof(FilterSubject));
    }

    internal static void ValidateResourceName(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.Length > 120
            || value.Any(character => !(char.IsLetterOrDigit(character) || character is '-' or '_')))
        {
            throw new InvalidOperationException(
                $"{parameterName} must contain only letters, digits, '-' or '_' and be at most 120 characters.");
        }
    }

    internal static void ValidateSubject(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.Any(char.IsWhiteSpace)
            || value.StartsWith('.')
            || value.EndsWith('.')
            || value.Contains("..", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"{parameterName} is not a valid NATS subject.");
        }

        var tokens = value.Split('.');
        for (var index = 0; index < tokens.Length; index++)
        {
            var token = tokens[index];
            if (token.Contains('>') && (token != ">" || index != tokens.Length - 1))
                throw new InvalidOperationException($"{parameterName} contains an invalid '>' wildcard.");
            if (token.Contains('*') && token != "*")
                throw new InvalidOperationException($"{parameterName} contains an invalid '*' wildcard.");
        }
    }
}
