using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace TomasAI.IFM.Application.Storage.EventSourceDb.Persistence;

internal static class EventLogPersistenceMetrics
{
    internal const string MeterName = "TomasAI.IFM.EventLogPersistence";
    static readonly Meter Meter = new(MeterName);
    static readonly Counter<long> Commands = Meter.CreateCounter<long>("ifm.event_log.append.commands");
    static readonly Counter<long> Events = Meter.CreateCounter<long>("ifm.event_log.append.events");
    static readonly Counter<long> PayloadBytes = Meter.CreateCounter<long>("ifm.event_log.append.payload_bytes", "By");
    static readonly Counter<long> Failures = Meter.CreateCounter<long>("ifm.event_log.append.failures");
    static readonly Histogram<double> Duration = Meter.CreateHistogram<double>("ifm.event_log.append.duration", "ms");
    static readonly Histogram<long> BatchCommands = Meter.CreateHistogram<long>("ifm.event_log.batch.commands");
    static readonly Histogram<long> BatchEvents = Meter.CreateHistogram<long>("ifm.event_log.batch.events");
    static readonly UpDownCounter<long> QueueDepth = Meter.CreateUpDownCounter<long>("ifm.event_log.queue.depth");

    internal static TagList Tags(EventLogWriteMode mode, bool compression)
    {
        var tags = new TagList();
        tags.Add("write_mode", mode.ToString());
        tags.Add("lz4", compression);
        return tags;
    }

    internal static void Queued(in TagList tags) => QueueDepth.Add(1, tags);
    internal static void Dequeued(in TagList tags) => QueueDepth.Add(-1, tags);

    internal static void Committed(
        in TagList tags,
        int commandCount,
        int eventCount,
        long payloadBytes,
        long startedTimestamp)
    {
        Commands.Add(commandCount, tags);
        Events.Add(eventCount, tags);
        PayloadBytes.Add(payloadBytes, tags);
        BatchCommands.Record(commandCount, tags);
        BatchEvents.Record(eventCount, tags);
        Duration.Record(Stopwatch.GetElapsedTime(startedTimestamp).TotalMilliseconds, tags);
    }

    internal static void Failed(in TagList tags, long startedTimestamp)
    {
        Failures.Add(1, tags);
        Duration.Record(Stopwatch.GetElapsedTime(startedTimestamp).TotalMilliseconds, tags);
    }
}
