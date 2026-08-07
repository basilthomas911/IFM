using System.Diagnostics.Metrics;

namespace TomasAI.IFM.Shared.EventModelActor;

/// <summary>
/// Low-overhead actor lifecycle instruments. Instruments remain dormant when no meter listener is attached.
/// </summary>
internal static class ActorLifecycleMetrics
{
    internal const string MeterName = "TomasAI.IFM.Shared.EventModelActor";
    static readonly Meter Meter = new(MeterName, "1.0.0");
    static readonly KeyValuePair<string, object?> StartupPhase = new("phase", "startup");
    static readonly KeyValuePair<string, object?> ShutdownWaitPhase = new("phase", "shutdown_wait");

    internal static readonly Histogram<double> StartupDuration = Meter.CreateHistogram<double>(
        "ifm.actor.startup.duration",
        "ms",
        "Elapsed time for actor runtime registration and startup.");

    internal static readonly Counter<long> StartupCompleted = Meter.CreateCounter<long>(
        "ifm.actor.startup.completed",
        description: "Actor runtime startup operations completed successfully.");

    internal static readonly Counter<long> StartupFailures = Meter.CreateCounter<long>(
        "ifm.actor.startup.failures",
        description: "Actor runtime startup operations that failed.");

    internal static readonly Histogram<double> ShutdownDuration = Meter.CreateHistogram<double>(
        "ifm.actor.shutdown.duration",
        "ms",
        "Elapsed time for the shared graceful actor shutdown operation.");

    internal static readonly Counter<long> ShutdownCompleted = Meter.CreateCounter<long>(
        "ifm.actor.shutdown.completed",
        description: "Graceful actor shutdown operations completed successfully.");

    internal static readonly Counter<long> ShutdownFailures = Meter.CreateCounter<long>(
        "ifm.actor.shutdown.failures",
        description: "Graceful actor shutdown operations that completed with one or more failures.");

    internal static readonly Counter<long> ShutdownCleanupFailures = Meter.CreateCounter<long>(
        "ifm.actor.shutdown.cleanup_failures",
        description: "Individual actor shutdown cleanup-stage failures.");

    internal static readonly Counter<long> ShutdownDrainedMessages = Meter.CreateCounter<long>(
        "ifm.actor.shutdown.messages_drained",
        description: "Accepted actor mailbox messages completed after intake stopped.");

    internal static readonly Counter<long> Cancellations = Meter.CreateCounter<long>(
        "ifm.actor.lifecycle.cancellations",
        description: "Actor lifecycle operations canceled by a caller token.");

    internal static void RecordStartupCancellation() => Cancellations.Add(1, StartupPhase);
    internal static void RecordShutdownWaitCancellation() => Cancellations.Add(1, ShutdownWaitPhase);

    internal static void RecordCleanupFailure(string stage)
        => ShutdownCleanupFailures.Add(1, new KeyValuePair<string, object?>("stage", stage));
}
