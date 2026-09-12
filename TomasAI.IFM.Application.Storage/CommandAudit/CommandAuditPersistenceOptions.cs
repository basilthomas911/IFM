namespace TomasAI.IFM.Application.Storage.CommandAudit;

public enum CommandAuditWriteMode : byte
{
    SequentialJsonLegacy = 0,
    SequentialMessagePack = 1,
    WindowedMessagePack = 2
}

public enum CommandAuditPayloadFormat : short
{
    MessagePack = 1
}

public sealed class CommandAuditPersistenceOptions
{
    public const string SectionName = "CommandAuditPersistence";

    public CommandAuditWriteMode WriteMode { get; set; } = CommandAuditWriteMode.WindowedMessagePack;
    public int QueueCommandCapacity { get; set; } = 8192;
    public int MaximumCommandsPerBatch { get; set; } = 64;
    public int MaximumBatchBytes { get; set; } = 256 * 1024;
    public int MaximumCommandPayloadBytes { get; set; } = 1024 * 1024;
    public TimeSpan MaximumOldestRequestDelay { get; set; } = TimeSpan.FromMilliseconds(1);
    public TimeSpan ShutdownDrainTimeout { get; set; } = TimeSpan.FromSeconds(10);

    public CommandAuditPersistenceOptions Validate()
    {
        if (!Enum.IsDefined(WriteMode)) throw new ArgumentOutOfRangeException(nameof(WriteMode));
        if (QueueCommandCapacity <= 0) throw new ArgumentOutOfRangeException(nameof(QueueCommandCapacity));
        if (MaximumCommandsPerBatch <= 0) throw new ArgumentOutOfRangeException(nameof(MaximumCommandsPerBatch));
        if (MaximumBatchBytes <= 0) throw new ArgumentOutOfRangeException(nameof(MaximumBatchBytes));
        if (MaximumCommandPayloadBytes <= 0) throw new ArgumentOutOfRangeException(nameof(MaximumCommandPayloadBytes));
        if (MaximumOldestRequestDelay <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(MaximumOldestRequestDelay));
        if (ShutdownDrainTimeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(ShutdownDrainTimeout));
        return this;
    }
}
