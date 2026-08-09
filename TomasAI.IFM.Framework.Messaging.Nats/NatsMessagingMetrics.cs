using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;

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
}
