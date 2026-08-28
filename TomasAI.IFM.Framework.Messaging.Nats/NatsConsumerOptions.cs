using System.Text.Json;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Contracts;
using TomasAI.IFM.Shared.EventModelActor;

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

    /// <inheritdoc />
    public Dictionary<ActorType, CoreNatsTrafficClass> FireAndForgetTraffic { get; set; } = [];

    public int GetSubscriptionCapacity()
        => SubscriptionCapacity > 0
            ? SubscriptionCapacity
            : checked(DispatcherCapacity * DispatcherCount);

    public CoreNatsTrafficClass GetFireAndForgetTrafficClass(ActorType actorType)
        => FireAndForgetTraffic.TryGetValue(actorType, out var trafficClass)
            ? trafficClass
            : CoreNatsTrafficClass.Unknown;

    public void Validate(ActorAdmissionOptions? admissionOptions = null)
    {
        FireAndForgetTraffic ??= [];
        if (string.IsNullOrWhiteSpace(Url))
            throw new InvalidOperationException($"{nameof(Url)} is required.");
        if (DispatcherCount <= 0)
            throw new InvalidOperationException($"{nameof(DispatcherCount)} must be greater than zero.");
        if (DispatcherCapacity <= 0)
            throw new InvalidOperationException($"{nameof(DispatcherCapacity)} must be greater than zero.");
        if (SubscriptionCapacity < 0)
            throw new InvalidOperationException($"{nameof(SubscriptionCapacity)} cannot be negative.");
        _ = GetSubscriptionCapacity();

        foreach (var (actorType, trafficClass) in FireAndForgetTraffic)
        {
            if (!Enum.IsDefined(actorType))
                throw new InvalidOperationException($"Unknown actor type '{actorType}' in Core NATS traffic classification.");
            if (!Enum.IsDefined(trafficClass))
                throw new InvalidOperationException($"Unknown Core NATS traffic class '{trafficClass}'.");
            if (actorType.GetDeliveryType() != ActorDeliveryType.NatsCore)
            {
                throw new InvalidOperationException(
                    $"Core NATS traffic cannot be configured for actor type '{actorType}'.");
            }
        }

        if (admissionOptions?.Mode != ActorAdmissionMode.Enforce)
            return;

        foreach (var actorType in new[] { ActorType.Command, ActorType.Query, ActorType.Function })
        {
            var trafficClass = GetFireAndForgetTrafficClass(actorType);
            if (trafficClass == CoreNatsTrafficClass.Unknown)
            {
                throw new InvalidOperationException(
                    $"Enforced actor admission requires an explicit Core NATS traffic classification for {actorType}.");
            }
            if (trafficClass == CoreNatsTrafficClass.RequiredNonDurable)
            {
                throw new InvalidOperationException(
                    $"Enforced actor admission is blocked while {actorType} has required non-durable Core NATS traffic.");
            }
        }

        foreach (var actorType in new[] { ActorType.Command, ActorType.Query, ActorType.Function })
        {
            if (GetFireAndForgetTrafficClass(actorType) != CoreNatsTrafficClass.RequestReplyOnly)
            {
                throw new InvalidOperationException(
                    $"Core NATS {actorType} traffic must be classified as {CoreNatsTrafficClass.RequestReplyOnly}.");
            }
        }
    }
}
