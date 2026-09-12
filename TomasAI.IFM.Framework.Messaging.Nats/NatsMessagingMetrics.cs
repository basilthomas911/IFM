using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Framework.Messaging.NatsJetStream;

/// <summary>
/// Low-overhead instruments for messaging throughput and failures. They are dormant when no meter listener is attached.
/// </summary>
internal static class NatsMessagingMetrics
{
    internal const string MeterName = "TomasAI.IFM.Framework.Messaging.Nats";
    internal const string CorePublishOperation = "core_publish";
    internal const string CoreRequestOperation = "core_request";
    internal const string JetStreamPublishOperation = "jetstream_publish";
    static readonly Meter Meter = new(MeterName, "1.0.0");

    public static readonly Counter<long> Published = Meter.CreateCounter<long>(
        "ifm.nats.messages.published",
        description: "Messages successfully published or requested through NATS.");

    public static readonly Counter<long> Received = Meter.CreateCounter<long>(
        "ifm.nats.messages.received",
        description: "Messages accepted from Core NATS or JetStream.");

    public static readonly Counter<long> DispatchFailures = Meter.CreateCounter<long>(
        "ifm.nats.dispatch.failures",
        description: "Messages that could not be delivered to an actor or event handler.");

    public static readonly Counter<long> DuplicatesSuppressed = Meter.CreateCounter<long>(
        "ifm.nats.duplicates.suppressed",
        description: "Duplicate domain events suppressed by the compatibility consumer.");

    public static readonly Counter<long> ListenerOnlyEvents = Meter.CreateCounter<long>(
        "ifm.nats.events.listener_only",
        description: "JetStream events acknowledged without actor delivery because they target only Core NATS listeners.");

    static long _typedSerializations;
    static long _typedDeserializations;
    static long _serializationFailures;
    static long _ownedIngressMessages;
    static long _ownedIngressBytes;
    static long _outstandingPayloadLeases;
    static long _legacyPayloadCopies;
    static long _legacyPayloadCopyBytes;
    static long _durablePayloadAllocations;

    static readonly ObservableCounter<long> TypedSerializations = Meter.CreateObservableCounter(
        "ifm.nats.serialization.typed",
        () => Interlocked.Read(ref _typedSerializations),
        description: "Payloads serialized directly into the NATS output writer.");

    static readonly ObservableCounter<long> TypedDeserializations = Meter.CreateObservableCounter(
        "ifm.nats.deserialization.typed",
        () => Interlocked.Read(ref _typedDeserializations),
        description: "Payloads deserialized directly from a NATS sequence or owned pooled memory.");

    static readonly ObservableCounter<long> SerializationFailures = Meter.CreateObservableCounter(
        "ifm.nats.serialization.failures",
        () => Interlocked.Read(ref _serializationFailures),
        description: "Typed MessagePack serialization or deserialization failures.");

    static readonly ObservableCounter<long> OwnedIngressMessages = Meter.CreateObservableCounter(
        "ifm.nats.ingress.owned.messages",
        () => Interlocked.Read(ref _ownedIngressMessages),
        description: "Inbound actor payloads held in pooled NATS memory until actor processing completes.");

    static readonly ObservableCounter<long> OwnedIngressBytes = Meter.CreateObservableCounter(
        "ifm.nats.ingress.owned.bytes",
        () => Interlocked.Read(ref _ownedIngressBytes),
        "By",
        "Bytes received into owned pooled actor payloads.");

    static readonly ObservableGauge<long> OutstandingPayloadLeases = Meter.CreateObservableGauge(
        "ifm.nats.payload.leases",
        () => Interlocked.Read(ref _outstandingPayloadLeases),
        description: "Owned pooled payload leases currently held by actor messaging.");

    static readonly ObservableCounter<long> LegacyPayloadCopies = Meter.CreateObservableCounter(
        "ifm.nats.payload.legacy_copies",
        () => Interlocked.Read(ref _legacyPayloadCopies),
        description: "Compatibility-listener or diagnostic rollback payloads materialized as managed byte arrays.");

    static readonly ObservableCounter<long> LegacyPayloadCopyBytes = Meter.CreateObservableCounter(
        "ifm.nats.payload.legacy_copy_bytes",
        () => Interlocked.Read(ref _legacyPayloadCopyBytes),
        "By",
        "Bytes materialized by compatibility-listener and diagnostic rollback payload paths.");

    static readonly ObservableCounter<long> DurablePayloadAllocations = Meter.CreateObservableCounter(
        "ifm.nats.payload.durable_allocations",
        () => Interlocked.Read(ref _durablePayloadAllocations),
        description: "Required managed payload allocations at the durable replay storage boundary.");

    public static readonly Histogram<double> OperationDuration = Meter.CreateHistogram<double>(
        "ifm.nats.operation.duration",
        "ms",
        "Core NATS publish/request and JetStream acknowledged-publish latency.");

    public static readonly Counter<long> OperationFailures = Meter.CreateCounter<long>(
        "ifm.nats.operation.failures",
        description: "NATS publish or request operations that failed.");

    public static readonly Counter<long> OverloadReplies = Meter.CreateCounter<long>(
        "ifm.nats.overload.replies",
        description: "Core NATS overload reply attempts by actor type and outcome.");

    public static readonly Counter<long> OverloadNaks = Meter.CreateCounter<long>(
        "ifm.nats.overload.naks",
        description: "JetStream overload negative acknowledgements by actor type and outcome.");

    public static readonly Counter<long> OptionalDrops = Meter.CreateCounter<long>(
        "ifm.nats.overload.optional_drops",
        description: "Explicitly classified optional Core NATS messages dropped during overload.");

    public static readonly Counter<long> JetStreamRedeliveries = Meter.CreateCounter<long>(
        "ifm.nats.messages.redelivered",
        description: "JetStream deliveries whose server delivery count is greater than one.");

    public static readonly Counter<long> MalformedJetStreamSubjectsTerminated = Meter.CreateCounter<long>(
        "ifm.nats.messages.malformed_subject_terminated",
        description: "JetStream messages terminally acknowledged because their actor subject contract is malformed.");

    public static readonly UpDownCounter<long> JetStreamListenerPending = Meter.CreateUpDownCounter<long>(
        "ifm.nats.listener.pending",
        description: "JetStream event-listener deliveries admitted to bounded dispatch and awaiting settlement.");

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static long StartOperation()
        => OperationDuration.Enabled ? Stopwatch.GetTimestamp() : 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void RecordOperation(long startedTimestamp, string operation)
    {
        if (startedTimestamp != 0)
        {
            OperationDuration.Record(
                Stopwatch.GetElapsedTime(startedTimestamp).TotalMilliseconds,
                new KeyValuePair<string, object?>("operation", operation));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void RecordOperationFailure(string operation)
        => OperationFailures.Add(1, new KeyValuePair<string, object?>("operation", operation));

    internal static void RecordOverloadReply(ActorType actorType, string outcome)
        => OverloadReplies.Add(
            1,
            new KeyValuePair<string, object?>("actor.type", actorType.ToStringFast()),
            new KeyValuePair<string, object?>("outcome", outcome));

    internal static void RecordOverloadNak(ActorType actorType, string outcome)
        => OverloadNaks.Add(
            1,
            new KeyValuePair<string, object?>("actor.type", actorType.ToStringFast()),
            new KeyValuePair<string, object?>("outcome", outcome));

    internal static void RecordOptionalDrop(
        ActorType actorType,
        CoreNatsTrafficClass trafficClass)
        => OptionalDrops.Add(
            1,
            new KeyValuePair<string, object?>("actor.type", actorType.ToStringFast()),
            new KeyValuePair<string, object?>("traffic.class", trafficClass.ToString()));

    internal static void RecordJetStreamRedelivery(ActorType actorType)
        => JetStreamRedeliveries.Add(
            1,
            new KeyValuePair<string, object?>("actor.type", actorType.ToStringFast()));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void AcquirePayloadLease(int payloadBytes)
    {
        Interlocked.Increment(ref _ownedIngressMessages);
        Interlocked.Add(ref _ownedIngressBytes, payloadBytes);
        Interlocked.Increment(ref _outstandingPayloadLeases);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void ReleasePayloadLease()
        => Interlocked.Decrement(ref _outstandingPayloadLeases);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void RecordLegacyPayloadCopy(int payloadBytes)
    {
        Interlocked.Increment(ref _legacyPayloadCopies);
        Interlocked.Add(ref _legacyPayloadCopyBytes, payloadBytes);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void RecordTypedSerialization()
        => Interlocked.Increment(ref _typedSerializations);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void RecordTypedDeserialization()
        => Interlocked.Increment(ref _typedDeserializations);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void RecordSerializationFailure()
        => Interlocked.Increment(ref _serializationFailures);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void RecordDurablePayloadAllocations(long count)
        => Interlocked.Add(ref _durablePayloadAllocations, count);
}
