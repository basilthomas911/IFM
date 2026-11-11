namespace TomasAI.IFM.Shared.EventSourcing;

/// <summary>Identifies the committed stream and revision for a caller-prepared event batch.</summary>
public sealed class EventLogBatchIdentity
{
    public EventLogBatchIdentity(string stream, long streamId, Guid commandId, long? expectedStreamVersion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stream);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(streamId);
        if (commandId == Guid.Empty)
            throw new ArgumentException("Command ID is required.", nameof(commandId));
        if (expectedStreamVersion is < 0)
            throw new ArgumentOutOfRangeException(nameof(expectedStreamVersion));
        Stream = stream;
        StreamId = streamId;
        CommandId = commandId;
        ExpectedStreamVersion = expectedStreamVersion;
    }

    public string Stream { get; }
    public long StreamId { get; }
    public Guid CommandId { get; }
    public long? ExpectedStreamVersion { get; }
}
