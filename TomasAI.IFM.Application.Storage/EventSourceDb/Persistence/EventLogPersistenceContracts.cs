using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.Storage.EventSourceDb.Persistence;

public enum EventLogWriteMode : byte
{
    Sequential = 0,
    BinaryCopy = 1
}

public sealed class EventLogPersistenceOptions
{
    public const string SectionName = "EventLogPersistence";
    public EventLogWriteMode WriteMode { get; set; } = EventLogWriteMode.Sequential;
    public bool UseLz4Compression { get; set; }
    public int QueueCommandCapacity { get; set; } = 8192;
    public int MaximumEventsPerBatch { get; set; } = 256;
    public int MaximumBatchBytes { get; set; } = 1024 * 1024;
    public TimeSpan MaximumOldestRequestDelay { get; set; } = TimeSpan.FromMilliseconds(1);
    public int MaximumEventsPerCommand { get; set; } = 4096;
    public int MaximumEventPayloadBytes { get; set; } = 16 * 1024 * 1024;
    public int MaximumCommandPayloadBytes { get; set; } = 32 * 1024 * 1024;
    public TimeSpan ShutdownDrainTimeout { get; set; } = TimeSpan.FromSeconds(10);

    public EventLogPersistenceOptions Validate()
    {
        if (!Enum.IsDefined(WriteMode)) throw new ArgumentOutOfRangeException(nameof(WriteMode));
        if (QueueCommandCapacity <= 0) throw new ArgumentOutOfRangeException(nameof(QueueCommandCapacity));
        if (MaximumEventsPerBatch <= 0) throw new ArgumentOutOfRangeException(nameof(MaximumEventsPerBatch));
        if (MaximumBatchBytes <= 0) throw new ArgumentOutOfRangeException(nameof(MaximumBatchBytes));
        if (MaximumOldestRequestDelay <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(MaximumOldestRequestDelay));
        if (MaximumEventsPerCommand <= 0) throw new ArgumentOutOfRangeException(nameof(MaximumEventsPerCommand));
        if (MaximumEventPayloadBytes <= 0) throw new ArgumentOutOfRangeException(nameof(MaximumEventPayloadBytes));
        if (MaximumCommandPayloadBytes < MaximumEventPayloadBytes) throw new ArgumentOutOfRangeException(nameof(MaximumCommandPayloadBytes));
        if (ShutdownDrainTimeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(ShutdownDrainTimeout));
        return this;
    }
}

public sealed record EventLogAppendEntry(int EventNameId, IEvent DomainEvent);

public sealed record EventLogAppendRequest(
    string EventStream,
    long EventStreamId,
    Guid CommandId,
    IReadOnlyList<EventLogAppendEntry> Events,
    long? ExpectedStreamVersion,
    DateTime EventTimestampUtc);

public readonly record struct EventLogAssignment(long EventVersion, long StreamVersion);

public sealed record EventLogAppendResult(IReadOnlyList<EventLogAssignment> Assignments, DateTime CommittedAtUtc);

/// <summary>
/// Reports that PostgreSQL did not provide a definitive commit outcome. Callers must reconcile durable state by
/// command ID and must not blindly retry the append.
/// </summary>
public sealed class EventLogCommitOutcomeUnknownException(string message, Exception innerException)
    : Exception(message, innerException);

public interface IEventLogAppender : IAsyncDisposable
{
    EventLogWriteMode WriteMode { get; }
    bool UseLz4Compression { get; }
    ValueTask<EventLogAppendResult> AppendAsync(EventLogAppendRequest request, CancellationToken cancellationToken = default);
}
