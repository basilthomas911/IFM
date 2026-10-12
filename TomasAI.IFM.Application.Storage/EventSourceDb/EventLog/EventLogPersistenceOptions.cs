


namespace TomasAI.IFM.Application.Storage.EventSourceDb.EventLog;

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

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0015", Justification = "This existing options validator reports the invalid configuration property by name; preserve its established exception type and diagnostic property name.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3928", Justification = "This existing options validator reports the invalid configuration property by name; preserve its established exception type and diagnostic property name.")]
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
