using System.Security.Cryptography;
using System.Text;
using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Framework.Messaging.NatsJetStream.RealtimeMailboxes;

/// <summary>Capture-only ownership. No dispatch, handler success, trading permission or financial acceptance is implied.</summary>
public sealed class DurableRealtimeSessionArchive : IRealtimeSessionRecorder, IAsyncDisposable
{
    const string Protocol = "session-archive:v1:ownership-only";
    readonly RealtimeSourceOutbox? journal;
    readonly int maximumWire;
    readonly int maximumPayload;
    readonly SemaphoreSlim entryGate = new(1, 1), readersGate = new(1, 1), resource = new(1, 1);
    int readers, disposed;
    public bool Enabled { get; }
    public string SessionDirectory { get; } = "";
    public int PendingRecords => journal?.PendingCount ?? 0;
    public long RetainedBytes => journal?.RetainedSegmentBytes ?? 0;
    public long ReceiptBytes => journal?.ReceiptBytes ?? 0;
    public string? CurrentFailure => journal?.Failure?.Message ?? journal?.CapacityFailure;

    public DurableRealtimeSessionArchive(RealtimeSessionArchiveOptions options)
    {
        Enabled = options.Enabled; maximumWire = options.MaximumWireBytes;
        if (!Enabled) return;
        if (maximumWire is <= 0 or > 16 * 1024 * 1024 || string.IsNullOrWhiteSpace(options.Directory)
            || string.IsNullOrWhiteSpace(options.SessionId) || options.SessionId.Length > 256
            || options.MaximumArchiveBytes <= 32L * 1024 * 1024 || options.MaximumReceiptBytes < 148)
            throw new InvalidOperationException("Invalid finite session archive bounds.");
        maximumPayload = checked(maximumWire + 65536);
        var epoch = "archive_" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(options.SessionId)))[..24];
        SessionDirectory = Path.Combine(Path.GetFullPath(options.Directory), epoch);
        Directory.CreateDirectory(SessionDirectory);
        var marker = Path.Combine(SessionDirectory, "archive-only.protocol");
        if (File.Exists(marker))
        {
            if (File.ReadAllText(marker) != Protocol) throw new InvalidDataException("Unknown session archive protocol.");
        }
        else
        {
            if (Directory.EnumerateFileSystemEntries(SessionDirectory).Any()) throw new InvalidDataException("Refusing to convert an existing owner directory into a session archive.");
            using var created = new FileStream(marker, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
            created.Write(Encoding.UTF8.GetBytes(Protocol)); created.Flush(true);
        }
        journal = new(new RealtimeMailboxOptions
        {
            Directory = options.Directory, Epoch = epoch, MaximumPayloadBytes = maximumPayload,
            MaximumOutboxBytes = options.MaximumArchiveBytes, MaximumReceiptBytes = options.MaximumReceiptBytes,
            CaptureBatchSize = 16, CaptureMaximumDelay = TimeSpan.Zero
        }, archiveOnly: true);
        // A fully flushed archive body is sufficient archival ownership. Recover
        // its receipt even when the process died before replying to the producer.
        foreach (var group in journal.Pending().Chunk(64))
            Task.WhenAll(group.Select(item => journal.ConfirmArchiveOwnershipAsync(item, default).AsTask())).GetAwaiter().GetResult();
    }

    public async ValueTask<RealtimeSessionArchiveReceipt> RecordAsync(RealtimeSessionArchiveRecord record, CancellationToken cancellationToken = default)
    {
        if (!Enabled || journal is null) throw new InvalidOperationException("Session recording is disabled.");
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
        await EnterReader(cancellationToken).ConfigureAwait(false);
        try { return await RecordCore(record, cancellationToken).ConfigureAwait(false); }
        finally { await ExitReader().ConfigureAwait(false); }
    }
    async ValueTask<RealtimeSessionArchiveReceipt> RecordCore(RealtimeSessionArchiveRecord record, CancellationToken cancellationToken)
    {
        if (record.Kind.Length is < 1 or > 64 || record.LogicalSourceKey.Length is < 1 or > 1024
            || record.Subject.Length is < 1 or > 32768 || record.OriginalWire.IsEmpty || record.OriginalWire.Length > maximumWire
            || record.Sequence < 0 || record.Dataset?.Length > 256 || record.Destinations.Length > 256
            || record.TraceParent?.Length > 256 || record.TraceState?.Length > 1024)
            throw new InvalidDataException("Invalid session record metadata/body bounds.");
        // The manifest lives separately in archive-v3 metadata. Source identity/body
        // do not change when a retry observes a newer routing catalog.
        var payload = MessagePackSerializer.Serialize(record with { Destinations = [] });
        if (payload.Length > record.OriginalWire.Length + 65536) throw new InvalidDataException("Session metadata exceeds 64 KiB.");
        var identity = SHA256.HashData(Encoding.UTF8.GetBytes(record.Kind + "|" + record.LogicalSourceKey));
        var subject = new ActorSubject(ActorType.Realtime, "SessionArchive", "Recorded", Convert.ToHexStringLower(identity));
        var source = RealtimeMailboxCodec.CaptureArchive(new(subject, new Guid(identity.AsSpan(0, 16)), payload,
            record.Origin, record.Dataset, record.GenerationId, record.TraceParent, record.TraceState), record.Destinations, maximumPayload);
        var wireHash = Convert.ToHexStringLower(SHA256.HashData(record.OriginalWire.Span));
        var receipt = await journal!.CaptureAsync(source, cancellationToken).ConfigureAwait(false);
        // The shared completion task also fences concurrent duplicates until the
        // original archival receipt is flushed. It always uses the first manifest.
        await journal.ConfirmArchiveOwnershipAsync(source, cancellationToken).ConfigureAwait(false);
        return new(wireHash, receipt.SourceSha256, receipt.AlreadyCaptured, receipt.ManifestSha256);
    }

    public IEnumerable<RealtimeSessionArchiveEntry> ReadCommitted()
    {
        if (!Enabled || journal is null) throw new InvalidOperationException("Session recording is disabled.");
        long ordinal = 0;
        foreach (var source in journal.ReadCommittedArchive())
        {
            var record = MessagePackSerializer.Deserialize<RealtimeSessionArchiveRecord>(source.Source.Payload)
                with { Origin = source.Source.Origin, Destinations = source.Destinations,
                    TraceParent = source.Source.TraceParent, TraceState = source.Source.TraceState };
            yield return new(++ordinal, record, Convert.ToHexStringLower(SHA256.HashData(record.OriginalWire.Span)), source.SourceHash)
                { RecordedAtUtc = source.RecordedAtUtc };
        }
    }
    public async ValueTask<RealtimeSessionArchivePrefix> GetCommittedPrefixAsync(CancellationToken cancellationToken = default)
    {
        if (!Enabled || journal is null) throw new InvalidOperationException("Session recording is disabled.");
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
        await entryGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await resource.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
                // Canceled callers may have left already-flushed archive bodies.
                // They can be owned here without any business execution.
                foreach (var group in journal.Pending().Chunk(64))
                    await Task.WhenAll(group.Select(item => journal.ConfirmArchiveOwnershipAsync(item, cancellationToken).AsTask())).ConfigureAwait(false);
                var files = journal.ArchivePrefixFiles();
                var receipts = files.Single(file => Path.GetFileName(file.RelativePath) == "receipts.bin");
                return new(Protocol, SessionDirectory, files, receipts.Length / 148);
            }
            finally { resource.Release(); }
        }
        finally { entryGate.Release(); }
    }
    async ValueTask EnterReader(CancellationToken token)
    {
        await entryGate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            await readersGate.WaitAsync(token).ConfigureAwait(false);
            try
            {
                ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
                if (readers == 0) await resource.WaitAsync(token).ConfigureAwait(false);
                readers++;
            }
            finally { readersGate.Release(); }
        }
        finally { entryGate.Release(); }
    }
    async ValueTask ExitReader()
    {
        await readersGate.WaitAsync().ConfigureAwait(false);
        try { if (--readers == 0) resource.Release(); }
        finally { readersGate.Release(); }
    }
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        await entryGate.WaitAsync().ConfigureAwait(false);
        try
        {
            await resource.WaitAsync().ConfigureAwait(false);
            try { if (journal is not null) await journal.DisposeAsync().ConfigureAwait(false); }
            finally { resource.Release(); }
        }
        finally { entryGate.Release(); }
    }
}
