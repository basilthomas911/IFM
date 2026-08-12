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
}
