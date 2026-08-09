using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;

namespace TomasAI.IFM.Shared.EventModelActor;

/// <summary>
/// Low-cardinality actor runtime measurements. Disabled instruments do not allocate or read the clock.
/// </summary>
internal static class ActorRuntimeMetrics
{
    internal const string MeterName = ActorLifecycleMetrics.MeterName;
    internal const string ValidationStage = "validation";
    internal const string ReplayStage = "replay";
    internal const string ExecutionStage = "execution";
    internal const string PersistenceStage = "persistence";
    internal const string ReplyStage = "reply";
    internal const string PublicationStage = "publication";
    internal const string DenormalizationStage = "denormalization";

    static readonly Meter Meter = new(MeterName, "1.0.0");

    internal static readonly Counter<long> Accepted = Meter.CreateCounter<long>(
        "ifm.actor.messages.accepted",
        description: "Messages accepted into actor mailboxes.");

    internal static readonly Counter<long> Processed = Meter.CreateCounter<long>(
        "ifm.actor.messages.processed",
        description: "Actor messages whose handlers reached a terminal outcome.");

    internal static readonly Counter<long> Failed = Meter.CreateCounter<long>(
        "ifm.actor.messages.failed",
        description: "Actor messages that escaped their handler with an exception.");

    internal static readonly Counter<long> Canceled = Meter.CreateCounter<long>(
        "ifm.actor.messages.canceled",
        description: "Actor messages canceled by the owning actor-runtime token.");

    internal static readonly UpDownCounter<long> MailboxDepth = Meter.CreateUpDownCounter<long>(
        "ifm.actor.mailbox.depth",
        "{message}",
        "Current messages queued across actor mailboxes.");

    internal static readonly UpDownCounter<long> ActiveMailboxes = Meter.CreateUpDownCounter<long>(
        "ifm.actor.mailbox.active",
        "{mailbox}",
        "Current active entity mailboxes.");

    internal static readonly UpDownCounter<long> ReadyQueueDepth = Meter.CreateUpDownCounter<long>(
        "ifm.actor.ready_queue.depth",
        "{mailbox}",
        "Current scheduled mailboxes waiting for an actor worker.");

    internal static readonly Histogram<double> EnqueueWaitDuration = Meter.CreateHistogram<double>(
        "ifm.actor.mailbox.enqueue_wait.duration",
        "ms",
        "Time spent waiting for mailbox admission capacity.");

    internal static readonly Histogram<double> QueueWaitDuration = Meter.CreateHistogram<double>(
        "ifm.actor.mailbox.queue_wait.duration",
        "ms",
        "Age of an accepted actor message when it is dequeued for processing.");

    internal static readonly Histogram<double> HandlerDuration = Meter.CreateHistogram<double>(
        "ifm.actor.handler.duration",
        "ms",
        "End-to-end actor handler execution time.");

    internal static readonly Histogram<double> StageDuration = Meter.CreateHistogram<double>(
        "ifm.actor.stage.duration",
        "ms",
        "Command, query, event, persistence, publication, reply, and denormalization stage duration.");

    internal static readonly Counter<long> StageFailures = Meter.CreateCounter<long>(
        "ifm.actor.stage.failures",
        description: "Actor processing stage failures handled by a domain actor.");

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static long StartEnqueueWait()
        => EnqueueWaitDuration.Enabled ? Stopwatch.GetTimestamp() : 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static long StartQueueWait()
        => QueueWaitDuration.Enabled ? Stopwatch.GetTimestamp() : 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static long StartHandler()
        => HandlerDuration.Enabled ? Stopwatch.GetTimestamp() : 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static long StartStage()
        => StageDuration.Enabled ? Stopwatch.GetTimestamp() : 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void RecordAccepted(ActorType actorType)
    {
        Accepted.Add(1, ActorTypeTag(actorType));
        MailboxDepth.Add(1, ActorTypeTag(actorType));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void RecordDequeued(long startedTimestamp, ActorType actorType)
    {
        MailboxDepth.Add(-1, ActorTypeTag(actorType));
        RecordDuration(QueueWaitDuration, startedTimestamp, actorType);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void RecordEnqueueWait(long startedTimestamp, ActorType actorType)
        => RecordDuration(EnqueueWaitDuration, startedTimestamp, actorType);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void RecordHandler(long startedTimestamp, ActorType actorType)
        => RecordDuration(HandlerDuration, startedTimestamp, actorType);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void RecordProcessed(ActorType actorType)
        => Processed.Add(1, ActorTypeTag(actorType));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void RecordFailed(ActorType actorType)
        => Failed.Add(1, ActorTypeTag(actorType));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void RecordCanceled(ActorType actorType)
        => Canceled.Add(1, ActorTypeTag(actorType));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void RecordMailboxStarted(ActorType actorType)
        => ActiveMailboxes.Add(1, ActorTypeTag(actorType));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void RecordMailboxStopped(ActorType actorType)
        => ActiveMailboxes.Add(-1, ActorTypeTag(actorType));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void RecordReadyScheduled(ActorType actorType)
        => ReadyQueueDepth.Add(1, ActorTypeTag(actorType));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void RecordReadyDequeued(ActorType actorType)
        => ReadyQueueDepth.Add(-1, ActorTypeTag(actorType));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void RecordStage(long startedTimestamp, string stage, ActorType actorType)
    {
        if (startedTimestamp == 0)
            return;

        StageDuration.Record(
            Stopwatch.GetElapsedTime(startedTimestamp).TotalMilliseconds,
            new KeyValuePair<string, object?>("stage", stage),
            ActorTypeTag(actorType));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void RecordStageFailure(string stage, ActorType actorType)
        => StageFailures.Add(
            1,
            new KeyValuePair<string, object?>("stage", stage),
            ActorTypeTag(actorType));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void RecordDuration(Histogram<double> histogram, long startedTimestamp, ActorType actorType)
    {
        if (startedTimestamp != 0)
            histogram.Record(Stopwatch.GetElapsedTime(startedTimestamp).TotalMilliseconds, ActorTypeTag(actorType));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static KeyValuePair<string, object?> ActorTypeTag(ActorType actorType)
        => new("actor.type", actorType.ToStringFast());
}
