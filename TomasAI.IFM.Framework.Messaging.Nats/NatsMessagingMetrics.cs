using System.Diagnostics.Metrics;

namespace TomasAI.IFM.Framework.Messaging.NatsJetStream;

/// <summary>
/// Low-overhead instruments for messaging throughput and failures. They are dormant when no meter listener is attached.
/// </summary>
internal static class NatsMessagingMetrics
{
    static readonly Meter Meter = new("TomasAI.IFM.Framework.Messaging.Nats", "1.0.0");

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
}
