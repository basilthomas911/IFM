using MessagePack;

namespace TomasAI.IFM.Shared.EventModelActor.Contracts;

public interface IRealtimeSessionRecorder
{
    bool Enabled { get; }
    ValueTask<RealtimeSessionArchiveReceipt> RecordAsync(RealtimeSessionArchiveRecord record, CancellationToken cancellationToken = default);
}

/// <summary>Original immutable input, never a handler-success or financial receipt.</summary>
[MessagePackObject(true)]
public sealed record RealtimeSessionArchiveRecord
{
    public string Kind { get; init; } = "";
    public string LogicalSourceKey { get; init; } = "";
    public string Subject { get; init; } = "";
    public ReadOnlyMemory<byte> OriginalWire { get; init; }
    [IgnoreMember] public MessageOrigin? Origin { get; init; }
    [IgnoreMember] public string? TraceParent { get; init; }
    [IgnoreMember] public string? TraceState { get; init; }
    public string? Dataset { get; init; }
    public Guid WorkerInstanceId { get; init; }
    public Guid GenerationId { get; init; }
    public long Sequence { get; init; }
    public Guid EventId { get; init; }
    public ActorSubject[] Destinations { get; init; } = [];
}

public sealed record RealtimeSessionArchiveReceipt(string OriginalWireSha256, string ArchiveReceiptSha256,
    bool AlreadyRecorded, string ManifestSha256)
{
    public bool ArchiveOwnershipOnly => true;
}

public sealed record RealtimeSessionArchiveEntry(long Ordinal, RealtimeSessionArchiveRecord Record,
    string OriginalWireSha256, string ArchiveSourceSha256)
{
    /// <summary>First durable archive admission time; absent on legacy unpaced archives. Original source timestamps remain unchanged.</summary>
    public DateTimeOffset? RecordedAtUtc { get; init; }
}

public sealed record RealtimeSessionArchivePrefixFile(string RelativePath, long Length);
public sealed record RealtimeSessionArchivePrefix(string Protocol, string SessionDirectory,
    IReadOnlyList<RealtimeSessionArchivePrefixFile> Files, long Records)
{
    public bool ArchiveOwnershipOnly => true;
    public bool OwnerSnapshot => false;
    public bool MapperCheckpoint => false;
}

public sealed class RealtimeSessionArchiveOptions
{
    public bool Enabled { get; set; }
    public string Directory { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "IFM", "session-capture");
    public string SessionId { get; set; } = "prepared";
    public int MaximumWireBytes { get; set; } = 1024 * 1024;
    public long MaximumArchiveBytes { get; set; } = 64L * 1024 * 1024 * 1024;
    public long MaximumReceiptBytes { get; set; } = 8L * 1024 * 1024 * 1024;
}
