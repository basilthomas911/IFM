using System.Buffers.Binary;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Threading.Channels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Framework.Messaging.NatsJetStream.RealtimeMailboxes;

/// <summary>Flushed bounded group commits. Full source segments retire only after every destination has a durable handler-success proof.
/// Receipt metadata remains durable and bounded; exhaustion refuses new capture instead of silently evicting evidence.</summary>
internal sealed class RealtimeSourceOutbox : IAsyncDisposable
{
    const int ReceiptSize = 148, SegmentSize = 16 * 1024 * 1024, RecentLimit = 8192;
    const int LookupBuckets = 1 << 20, LookupNodeBytes = 48;
    const long LookupHeadsBytes = LookupBuckets * 8L;
    readonly RealtimeMailboxOptions options;
    readonly bool archiveOnly;
    readonly string root;
    readonly FileStream lease, receipts;
    FileStream lookup = null!;
    long lookupLength = LookupHeadsBytes;
    FileStream deliveryReceipts;
    readonly Lock gate = new();
    readonly Dictionary<string, Entry> pending = new(StringComparer.Ordinal);
    static readonly IComparer<Entry> EntryOrder=Comparer<Entry>.Create((left,right)=>left.Ordinal.CompareTo(right.Ordinal));
    readonly SortedSet<Entry> orderedPending = new(EntryOrder);
    // Derived only from frozen source manifests and durably flushed handler
    // proofs. Each reference belongs to one bounded pending source recipient.
    readonly Dictionary<string,SortedSet<Entry>> mailboxPendingOrder=new(StringComparer.Ordinal);
    long pendingOrderReferences,liveDestinationProofSlots;
    internal int PendingMailboxOrderCount{get{lock(gate)return mailboxPendingOrder.Count;}}
    internal long PendingMailboxOrderReferences{get{lock(gate)return pendingOrderReferences;}}
    internal long LiveDestinationProofSlots{get{lock(gate)return liveDestinationProofSlots;}}
    void IndexPendingOrder(Entry entry)
    {
        foreach(var destination in entry.Destinations)
        {
            if(!mailboxPendingOrder.TryGetValue(destination,out var order))mailboxPendingOrder.Add(destination,order=new(EntryOrder));
            if(order.Add(entry))pendingOrderReferences++;
        }
    }
    void RetirePendingOrder(Entry entry,string destination)
    {
        if(!mailboxPendingOrder.TryGetValue(destination,out var order))return;
        if(order.Remove(entry))pendingOrderReferences--;
        if(order.Count==0)mailboxPendingOrder.Remove(destination);
    }
    void RetirePendingOrder(Entry entry){foreach(var destination in entry.Destinations)RetirePendingOrder(entry,destination);}
    readonly Dictionary<string, RealtimeCaptureReceipt> recent = new(StringComparer.Ordinal);
    readonly Queue<string> recentKeys = new();
    readonly byte[][] bloom = Enumerable.Range(0, 256).Select(_ => new byte[65536]).ToArray();
    readonly Dictionary<string, int> segmentPending = new(StringComparer.Ordinal);
    readonly Channel<Write> writes;
    readonly Task writerTask;
    FileStream segment = null!;
    string segmentPath = "";
    long retainedBytes, pendingBytes;
    Exception? failed;
    long sequence, captureOrdinal, archiveRecordedTicks;
    CapturedRealtimeSource StampArchiveArrival(CapturedRealtimeSource value)
    {
        if (!archiveOnly || value.RecordedAtUtc is not null) return value;
        archiveRecordedTicks = Math.Max(archiveRecordedTicks, DateTimeOffset.UtcNow.UtcTicks);
        return value with { RecordedAtUtc = new DateTimeOffset(archiveRecordedTicks, TimeSpan.Zero) };
    }
    long flushCount, flushTicks;
    internal long WriterFlushCount => Interlocked.Read(ref flushCount);
    internal double WriterFlushMilliseconds => TimeSpan.FromTicks(Interlocked.Read(ref flushTicks)).TotalMilliseconds;
    void FlushWriter(FileStream file)
    {
        var started = Stopwatch.GetTimestamp(); file.Flush(true);
        Interlocked.Add(ref flushTicks, Stopwatch.GetElapsedTime(started).Ticks); Interlocked.Increment(ref flushCount);
    }
    int disposed;
    internal sealed class Entry(CapturedRealtimeSource value)
    {
        public CapturedRealtimeSource Value { get; } = value;
        public HashSet<string> Destinations { get; } = value.Destinations.Select(target => RealtimeMailboxCodec.MailboxKey(target.ThreadId)).ToHashSet(StringComparer.Ordinal);
        public readonly TaskCompletionSource<RealtimeCaptureReceipt> Captured = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public string Segment = "";
        public bool Completing;
        public TaskCompletionSource? Completion;
        public long Ordinal;
        public readonly HashSet<string> Delivered = new(StringComparer.Ordinal);
        public readonly Dictionary<string, TaskCompletionSource> PendingDelivery = new(StringComparer.Ordinal);
    }
    sealed record Write(Entry Entry, bool Completion, TaskCompletionSource? Done = null, string? Destination = null);
    public RealtimeSourceOutbox(RealtimeMailboxOptions options, bool archiveOnly = false)
    {
        this.options = options; this.archiveOnly = archiveOnly; root = Path.Combine(options.Directory, options.Epoch, "sources");
        var modePath = Path.Combine(options.Directory, options.Epoch, "archive-only.protocol");
        if (File.Exists(modePath) != archiveOnly) throw new InvalidDataException("Handler journals and archive-only journals are not interchangeable.");
        Directory.CreateDirectory(root);
        lease = new(Path.Combine(root, "owner.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        try
        {
            receipts = new(Path.Combine(root, "receipts.bin"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read, 65536);
            deliveryReceipts = new(Path.Combine(root, "delivery-receipts.bin"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read, 65536);
            foreach (var index in Directory.EnumerateFiles(root, "*.idx")) File.Delete(index);
            // Derived bounded on-disk hash chains replace linear prefix scans.
            // Only receipts.bin is authority; this file is rebuilt on every open.
            lookup = new(Path.Combine(root, "receipt.lookup"), FileMode.Create, FileAccess.ReadWrite, FileShare.Read, 1, FileOptions.RandomAccess);
            lookup.SetLength(LookupHeadsBytes);
            LoadReceipts(); LoadSegments(); LoadDeliveryReceipts();
            if (retainedBytes + pendingBytes + SegmentHeadroom > options.MaximumOutboxBytes)
                throw new IOException("Recovered realtime source files exceed configured disk reservation.");
            OpenSegment();
            writes = Channel.CreateBounded<Write>(new BoundedChannelOptions(options.MaximumPendingSources * 2) { SingleReader = true, FullMode = BoundedChannelFullMode.Wait });
            writerTask = RunWriterAsync();
        }
        catch { lookup?.Dispose(); deliveryReceipts?.Dispose(); receipts?.Dispose(); lease.Dispose(); throw; }
    }
    public IReadOnlyList<RealtimePendingGeneration> PendingGenerations()
    {
        lock (gate) return pending.Values.Where(entry => !string.IsNullOrEmpty(entry.Value.Source.Dataset) && entry.Value.Source.SourceGenerationId != Guid.Empty)
            .GroupBy(entry => (Dataset: entry.Value.Source.Dataset!, Generation: entry.Value.Source.SourceGenerationId))
            .Select(group => new RealtimePendingGeneration(group.Key.Dataset, group.Key.Generation, group.Min(entry => entry.Ordinal), group.Count()))
            .OrderBy(value => value.FirstCaptureOrdinal).ToArray();
    }
    public int PendingGenerationCount(string dataset, Guid generation)
    { lock (gate) return pending.Values.Count(entry => entry.Value.Source.Dataset == dataset && entry.Value.Source.SourceGenerationId == generation); }
    public int PendingCount { get { lock (gate) return pending.Count; } }
    public long PendingBytes { get { lock (gate) return pendingBytes; } }
    public long RetainedSegmentBytes { get { lock (gate) return retainedBytes; } }
    public long ReceiptBytes { get { lock (gate) return receipts.Length + deliveryReceipts.Length; } }
    public Exception? Failure => Volatile.Read(ref failed);
    internal bool IsArchiveCommitted(CapturedRealtimeSource source)
    {
        if (!archiveOnly) throw new InvalidOperationException("Not an archive journal.");
        lock (gate)
        {
            if (pending.ContainsKey(source.Key)) return false;
            var receipt = FindReceipt(source.Key);
            return receipt is not null && receipt.SourceSha256 == source.SourceHash && receipt.ManifestSha256 == source.ManifestHash;
        }
    }
    internal IEnumerable<CapturedRealtimeSource> ReadCommittedArchive()
    {
        if (!archiveOnly) throw new InvalidOperationException("Not an archive journal.");
        foreach (var path in Directory.EnumerateFiles(root, "*.rto").Order(StringComparer.Ordinal))
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var limit = stream.Length; var header = new byte[36]; var staged = new List<CapturedRealtimeSource>();
            while (stream.Position + 4 <= limit)
            {
                stream.ReadExactly(header.AsSpan(0, 4)); var length = BinaryPrimitives.ReadInt32LittleEndian(header);
                if (length == 0)
                {
                    foreach (var item in staged) if (IsArchiveCommitted(item)) yield return item;
                    staged.Clear(); continue;
                }
                if (length <= 0 || length > options.MaximumPayloadBytes + 65536) throw new InvalidDataException("Invalid archive frame length.");
                if (stream.Position + 32L + length > limit) break;
                stream.ReadExactly(header.AsSpan(4)); var body = new byte[length]; stream.ReadExactly(body);
                if (!CryptographicOperations.FixedTimeEquals(header.AsSpan(4), SHA256.HashData(body))) throw new InvalidDataException("Archive frame checksum mismatch.");
                staged.Add(RealtimeMailboxCodec.Decode(body, options.MaximumPayloadBytes, true));
                if (staged.Count > 64) throw new InvalidDataException("Archive commit group exceeds its declared bound.");
            }
        }
    }
    internal RealtimeSessionArchivePrefixFile[] ArchivePrefixFiles()
    {
        if (!archiveOnly) throw new InvalidOperationException("Not an archive journal.");
        lock (gate)
        {
            if (pending.Count != 0) throw new IOException("Unsettled archive records prevent a committed prefix.");
            var directory = Path.Combine(options.Directory, options.Epoch);
            return Directory.EnumerateFiles(root, "*.rto").Append(receipts.Name).Append(deliveryReceipts.Name)
                .Append(Path.Combine(directory, "archive-only.protocol")).Order(StringComparer.Ordinal)
                .Select(path => new RealtimeSessionArchivePrefixFile(Path.GetRelativePath(directory, path), new FileInfo(path).Length)).ToArray();
        }
    }
    public string? CapacityFailure
    {
        get
        {
            lock (gate)
            {
                if (pending.Count >= options.MaximumPendingSources) return "Realtime pending source capacity exhausted; producer retains new work.";
                if (retainedBytes + pendingBytes + 65537L + SegmentHeadroom > options.MaximumOutboxBytes)
                    return "Realtime source disk reservation exhausted; producer retains new work.";
                if (receipts.Length + (pending.Count + 1L) * ReceiptSize > options.MaximumReceiptBytes)
                    return "Realtime receipt horizon capacity exhausted; authoritative archive maintenance is required.";
                return null;
            }
        }
    }
    public CapturedRealtimeSource[] Pending()
    { lock (gate) return orderedPending.Where(e => e.Captured.Task.IsCompletedSuccessfully && !e.Completing).Select(e => e.Value).ToArray(); }

    public async ValueTask<RealtimeCaptureReceipt> CaptureAsync(CapturedRealtimeSource value, CancellationToken token)
    {
        if (value.ArchiveOwnershipOnly != archiveOnly) throw new InvalidDataException("Source ownership kind does not match journal mode.");
        Entry entry; bool duplicate;
        lock (gate)
        {
            ThrowIfUnavailable();
            duplicate = pending.TryGetValue(value.Key, out entry!);
            if (!duplicate && FindReceipt(value.Key) is { } prior)
            { if (prior.SourceSha256 != value.SourceHash) throw new InvalidDataException("Realtime source identity reused with changed bytes/provenance."); return prior with { AlreadyCaptured = true }; }
            if (duplicate)
            {
                if (entry.Value.SourceHash != value.SourceHash) throw new InvalidDataException("Realtime source identity changed.");
            }
            else
            {
                if (pending.Count >= options.MaximumPendingSources || retainedBytes + pendingBytes + ReservedSize(value) + SegmentHeadroom > options.MaximumOutboxBytes
                    || receipts.Length + (pending.Count + 1L) * ReceiptSize > options.MaximumReceiptBytes)
                    throw new IOException("Realtime source outbox/receipt capacity exhausted; source must retain publication.");
                entry = new(StampArchiveArrival(value)) { Ordinal = ++captureOrdinal }; pending.Add(value.Key, entry); orderedPending.Add(entry);IndexPendingOrder(entry); pendingBytes += ReservedSize(value);
                if (!writes.Writer.TryWrite(new(entry, false)))
                { pending.Remove(value.Key); orderedPending.Remove(entry);RetirePendingOrder(entry); pendingBytes -= ReservedSize(value); throw new IOException("Realtime source capture writer is unavailable."); }
            }
        }
        var receipt = await entry.Captured.Task.WaitAsync(token).ConfigureAwait(false);
        return duplicate ? receipt with { AlreadyCaptured = true } : receipt;
    }
    long SegmentHeadroom => SegmentSize + (options.MaximumPayloadBytes + 65536L) * options.CaptureBatchSize;
    public async ValueTask<IReadOnlyList<RealtimeCaptureReceipt>> CaptureBatchAsync(IReadOnlyList<CapturedRealtimeSource> values, CancellationToken token)
    {
        if (values.Count is < 1 or > 64) throw new InvalidDataException("Realtime capture batch size is outside bounds.");
        if (values.Any(value => value.ArchiveOwnershipOnly != archiveOnly)) throw new InvalidDataException("Source ownership kind does not match journal mode.");
        var results = new Task<RealtimeCaptureReceipt>[values.Count];
        lock (gate)
        {
            ThrowIfUnavailable();
            var created = new Dictionary<string, Entry>(StringComparer.Ordinal);
            var createdOrder = new List<Entry>();
            var duplicates = new bool[values.Count];
            for (var index = 0; index < values.Count; index++)
            {
                var value = values[index];
                if (pending.TryGetValue(value.Key, out var existing) || created.TryGetValue(value.Key, out existing))
                {
                    if (existing.Value.SourceHash != value.SourceHash) throw new InvalidDataException("Batch source identity changed.");
                    results[index] = existing.Captured.Task; duplicates[index] = true;
                }
                else if (FindReceipt(value.Key) is { } receipt)
                {
                    if (receipt.SourceSha256 != value.SourceHash) throw new InvalidDataException("Batch source conflicts with durable receipt.");
                    results[index] = Task.FromResult(receipt with { AlreadyCaptured = true });
                }
                else
                {
                    var entry = new Entry(StampArchiveArrival(value)); created.Add(value.Key, entry); createdOrder.Add(entry); results[index] = entry.Captured.Task;
                }
            }
            var reservation = created.Values.Sum(entry => ReservedSize(entry.Value));
            if (pending.Count + created.Count > options.MaximumPendingSources
                || retainedBytes + pendingBytes + reservation + SegmentHeadroom > options.MaximumOutboxBytes
                || receipts.Length + (pending.Count + created.Count) * (long)ReceiptSize > options.MaximumReceiptBytes
                || writes.Reader.Count + created.Count > options.MaximumPendingSources * 2)
                throw new IOException("Atomic realtime batch cannot fit; caller retains every unaccepted source.");
            foreach (var entry in createdOrder)
            { entry.Ordinal = ++captureOrdinal; pending.Add(entry.Value.Key, entry); orderedPending.Add(entry);IndexPendingOrder(entry); pendingBytes += ReservedSize(entry.Value); }
            foreach (var entry in createdOrder)
            {
                if (writes.Writer.TryWrite(new(entry, false))) continue;
                var error = new IOException("Atomic realtime batch writer failed; original source batch must be recovered.");
                Volatile.Write(ref failed, error); foreach (var abandoned in createdOrder) abandoned.Captured.TrySetException(error);
                throw error;
            }
            for (var index = 0; index < values.Count; index++)
                if (duplicates[index]) results[index] = DuplicateAsync(results[index]);
        }
        return await Task.WhenAll(results).WaitAsync(token).ConfigureAwait(false);
        static async Task<RealtimeCaptureReceipt> DuplicateAsync(Task<RealtimeCaptureReceipt> value)
            => (await value.ConfigureAwait(false)) with { AlreadyCaptured = true };
    }
    static long ReservedSize(CapturedRealtimeSource source) => source.Source.Payload.Length + 65536L;
    public CapturedRealtimeSource[] OrderForDelivery(IEnumerable<CapturedRealtimeSource> sources)
    { lock (gate) return sources.OrderBy(source => pending.TryGetValue(source.Key, out var entry) ? entry.Ordinal : long.MinValue).ToArray(); }
    public bool CanDeliverOrdered(IReadOnlyList<CapturedRealtimeSource> sources, string mailbox)
    {
        lock (gate)
        {
            if(!mailboxPendingOrder.TryGetValue(mailbox,out var order))
            {
                foreach(var source in sources)
                    if(pending.TryGetValue(source.Key,out var entry)&&!entry.Delivered.Contains(mailbox))return false;
                return true;
            }
            using var expected=order.GetEnumerator();
            foreach(var source in sources)
            {
                if(!pending.TryGetValue(source.Key,out var entry)||entry.Delivered.Contains(mailbox))continue;
                if(!expected.MoveNext()||!ReferenceEquals(expected.Current,entry))return false;
            }
            return true;
        }
    }
    public bool IsDestinationCompleted(CapturedRealtimeSource source, string mailbox)
    {
        if (archiveOnly) throw new InvalidOperationException("Archive ownership never proves handler completion.");
        lock (gate)
        {
            if (pending.TryGetValue(source.Key, out var entry))
            { if (entry.Value.SourceHash != source.SourceHash || entry.Value.ManifestHash != source.ManifestHash) throw new InvalidDataException("Source proof mismatch."); return entry.Delivered.Contains(mailbox); }
            var receipt = FindReceipt(source.Key);
            if (receipt is null) throw new InvalidDataException("Broker delivery has no original source ownership evidence.");
            if (receipt.SourceSha256 != source.SourceHash || receipt.ManifestSha256 != source.ManifestHash) throw new InvalidDataException("Completed source proof mismatch.");
            return true;
        }
    }
    public async ValueTask RecordDestinationCompletedAsync(CapturedRealtimeSource source, string mailbox, CancellationToken token)
    {
        if (archiveOnly) throw new InvalidOperationException("Archive-only records have no domain handler settlement.");
        TaskCompletionSource done;
        lock (gate)
        {
            ThrowIfUnavailable();
            if (!pending.TryGetValue(source.Key, out var entry))
            { if (FindReceipt(source.Key) is { } proof && proof.SourceSha256 == source.SourceHash && proof.ManifestSha256 == source.ManifestHash) return; throw new InvalidDataException("Uncaptured destination completion."); }
            if (entry.Value.SourceHash != source.SourceHash || entry.Value.ManifestHash != source.ManifestHash || !entry.Destinations.Contains(mailbox))
                throw new InvalidDataException("Destination not owned by original source.");
            if (entry.Delivered.Contains(mailbox)) return;
            if (!entry.PendingDelivery.TryGetValue(mailbox, out done!))
            {
                if (liveDestinationProofSlots >= options.MaximumReceiptBytes / 128)
                    throw new IOException("Durable destination receipt capacity exhausted.");
                done = new(TaskCreationOptions.RunContinuationsAsynchronously); entry.PendingDelivery.Add(mailbox, done);liveDestinationProofSlots++;
                if (!writes.Writer.TryWrite(new(entry, false, done, mailbox)))
                { entry.PendingDelivery.Remove(mailbox);liveDestinationProofSlots--; throw new IOException("Destination receipt writer unavailable."); }
            }
        }
        await done.Task.WaitAsync(token).ConfigureAwait(false);
    }
    public async ValueTask ConfirmBrokerOwnershipAsync(CapturedRealtimeSource value, CancellationToken token)
    {
        if (archiveOnly || value.ArchiveOwnershipOnly) throw new InvalidOperationException("Archive ownership is not broker/handler settlement.");
        await ConfirmOwnedAsync(value, token).ConfigureAwait(false);
    }
    public async ValueTask ConfirmBrokerOwnershipBatchAsync(IReadOnlyList<CapturedRealtimeSource> values, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Count is < 1 or > 64) throw new ArgumentOutOfRangeException(nameof(values));
        if (archiveOnly) throw new InvalidOperationException("Archive ownership is not broker/handler settlement.");
        token.ThrowIfCancellationRequested();
        Task[] confirmations;
        lock (gate)
        {
            ThrowIfUnavailable();
            // Validate the entire group before accepting any completion. The writer
            // cannot take its commit group until every member has been enqueued.
            foreach (var value in values)
            {
                if (value.ArchiveOwnershipOnly) throw new InvalidOperationException("Archive ownership is not broker/handler settlement.");
                if (pending.TryGetValue(value.Key, out var entry))
                {
                    if (entry.Value.SourceHash != value.SourceHash || entry.Value.ManifestHash != value.ManifestHash)
                        throw new InvalidDataException("Source completion proof mismatch.");
                    if (entry.Destinations.Any(target => !entry.Delivered.Contains(target)))
                        throw new InvalidOperationException("Required destination completion proof is missing.");
                }
                else if (FindReceipt(value.Key) is not { } proof || proof.SourceSha256 != value.SourceHash || proof.ManifestSha256 != value.ManifestHash)
                    throw new InvalidDataException("Source completion has no matching ownership evidence.");
            }
            confirmations = new Task[values.Count];
            for (var index = 0; index < values.Count; index++)
                confirmations[index] = ConfirmOwnedAsync(values[index], token).AsTask();
        }
        await Task.WhenAll(confirmations).ConfigureAwait(false);
    }
    internal async ValueTask ConfirmArchiveOwnershipAsync(CapturedRealtimeSource value, CancellationToken token)
    {
        if (!archiveOnly || !value.ArchiveOwnershipOnly) throw new InvalidOperationException("Normal required work cannot use archive settlement.");
        await ConfirmOwnedAsync(value, token).ConfigureAwait(false);
    }
    async ValueTask ConfirmOwnedAsync(CapturedRealtimeSource value, CancellationToken token)
    {
        Task doneTask;
        lock (gate)
        {
            ThrowIfUnavailable();
            if (!pending.TryGetValue(value.Key, out var entry)) return;
            if (!archiveOnly && entry.Destinations.Any(target => !entry.Delivered.Contains(target)))
                throw new InvalidOperationException("Required destination completion proof is missing.");
            if (entry.Completing) doneTask = entry.Completion!.Task;
            else
            {
                entry.Completing = true; var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                entry.Completion = done; doneTask = done.Task;
                if (!writes.Writer.TryWrite(new(entry, true, done)))
                { entry.Completing = false; entry.Completion = null; throw new IOException("Realtime receipt writer unavailable."); }
            }
        }
        await doneTask.WaitAsync(token).ConfigureAwait(false);
    }
    async Task RunWriterAsync()
    {
        var batch = new List<Write>(options.CaptureBatchSize);
        try
        {
            var headerBytes = new byte[36];
            while (await writes.Reader.WaitToReadAsync().ConfigureAwait(false))
            {
                batch.Clear(); while (batch.Count < options.CaptureBatchSize && writes.Reader.TryRead(out var item)) batch.Add(item);
                if (batch.Count < options.CaptureBatchSize && options.CaptureMaximumDelay > TimeSpan.Zero)
                {
                    await Task.WhenAny(writes.Reader.WaitToReadAsync().AsTask(), Task.Delay(options.CaptureMaximumDelay)).ConfigureAwait(false);
                    while (batch.Count < options.CaptureBatchSize && writes.Reader.TryRead(out var item)) batch.Add(item);
                }
                lock (gate)
                {
                    // Producers enqueue an atomic group under gate. The initial
                    // read can race its first item; collect the rest after acquiring
                    // that same gate before deciding which durable files to flush.
                    while (batch.Count < options.CaptureBatchSize && writes.Reader.TryRead(out var item)) batch.Add(item);
                    var liveBytes = liveDestinationProofSlots * 128;
                    if (deliveryReceipts.Length + batch.Count(item => item.Destination is not null) * 128L > options.MaximumReceiptBytes
                        || deliveryReceipts.Length > Math.Max(16L * 1024 * 1024, liveBytes * 3)) CompactDeliveryReceipts();
                }
                var captured = false; var completed = false; var delivered = false;
                foreach (var item in batch)
                {
                    if (item.Destination is { } destination)
                    {
                        WriteDeliveryProof(deliveryReceipts, item.Entry.Value, destination); delivered = true;
                    }
                    else if (item.Completion) { AppendReceipt(item.Entry.Value); completed = true; }
                    else
                    {
                        var bytes = RealtimeMailboxCodec.Encode(item.Entry.Value);
                        Span<byte> header = headerBytes; BinaryPrimitives.WriteInt32LittleEndian(header, bytes.Length); SHA256.HashData(bytes, header[4..]);
                        segment.Write(header); segment.Write(bytes); item.Entry.Segment = segmentPath;
                        segmentPending[segmentPath] = segmentPending.GetValueOrDefault(segmentPath) + 1; retainedBytes += 36 + bytes.Length; captured = true;
                    }
                }
                if (captured) { segment.Write(new byte[4]); retainedBytes += 4; FlushWriter(segment); }
                if (delivered) FlushWriter(deliveryReceipts);
                if (completed) FlushWriter(receipts);
                lock (gate)
                {
                    foreach (var item in batch)
                    {
                        if (item.Destination is { } destination)
                        { item.Entry.Delivered.Add(destination); item.Entry.PendingDelivery.Remove(destination);RetirePendingOrder(item.Entry,destination); item.Done!.TrySetResult(); }
                        else if (!item.Completion) item.Entry.Captured.TrySetResult(item.Entry.Value.Receipt(false));
                        else
                        {
                            var entry = item.Entry; Remember(entry.Value.Key, entry.Value.Receipt(true));
                            pending.Remove(entry.Value.Key); orderedPending.Remove(entry);RetirePendingOrder(entry);liveDestinationProofSlots-=entry.Delivered.Count; pendingBytes -= ReservedSize(entry.Value);
                            if (segmentPending.TryGetValue(entry.Segment, out var remaining)) segmentPending[entry.Segment] = remaining - 1;
                            item.Done!.TrySetResult();
                        }
                    }
                    if (segment.Length >= SegmentSize) { segment.Dispose(); OpenSegment(); }
                    foreach (var path in segmentPending.Where(p => !archiveOnly && p.Key != segmentPath && p.Value == 0).Select(p => p.Key).ToArray())
                    { retainedBytes -= new FileInfo(path).Length; File.Delete(path); segmentPending.Remove(path); }
                    // Every frame in this active segment now has a flushed full-source
                    // receipt. Reclaim it even below the rotation size, otherwise a
                    // finite disk budget could refuse all new work after drain.
                    if (!archiveOnly && segmentPending.GetValueOrDefault(segmentPath) == 0 && segment.Length != 0)
                    {
                        var retired = segment.Length; segment.SetLength(0); segment.Position = 0; FlushWriter(segment);
                        retainedBytes -= retired;
                    }
                }
            }
        }
        catch (Exception error)
        {
            Volatile.Write(ref failed, error); writes.Writer.TryComplete(error);
            foreach (var item in batch) { item.Entry.Captured.TrySetException(error); item.Done?.TrySetException(error); }
            while (writes.Reader.TryRead(out var item)) { item.Entry.Captured.TrySetException(error); item.Done?.TrySetException(error); }
        }
    }
    void OpenSegment()
    {
        segmentPath = Path.Combine(root, (++sequence).ToString("D16", System.Globalization.CultureInfo.InvariantCulture) + ".rto");
        segment = new(segmentPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read, 65536);
        segmentPending[segmentPath] = 0;
    }
    void LoadSegments()
    {
        var headerBytes = new byte[36];
        foreach (var path in Directory.EnumerateFiles(root, "*.rto").Order(StringComparer.Ordinal))
        {
            sequence = Math.Max(sequence, long.Parse(Path.GetFileNameWithoutExtension(path), System.Globalization.CultureInfo.InvariantCulture));
            using var file = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read, 65536);
            long committed = 0; var staged = new List<Entry>(); segmentPending[path] = 0;
            Span<byte> header = headerBytes;
            while (file.Position < file.Length)
            {
                if (file.Length - file.Position < 4) break;
                file.ReadExactly(header[..4]); var length = BinaryPrimitives.ReadInt32LittleEndian(header);
                if (length == 0)
                {
                    foreach (var entry in staged)
                    {
                        if (FindReceipt(entry.Value.Key) is { } saved)
                        { if (saved.SourceSha256 != entry.Value.SourceHash || saved.ManifestSha256 != entry.Value.ManifestHash)
                                throw new InvalidDataException("Recovered source conflicts with durable receipt."); continue; }
                        if (pending.ContainsKey(entry.Value.Key)) continue;
                        if (pending.Count >= options.MaximumPendingSources || pendingBytes + ReservedSize(entry.Value) + SegmentHeadroom > options.MaximumOutboxBytes)
                            throw new IOException("Recovered realtime source backlog exceeds configured capacity.");
                        entry.Ordinal = ++captureOrdinal; entry.Segment = path; entry.Captured.TrySetResult(entry.Value.Receipt(true)); pending.Add(entry.Value.Key, entry); orderedPending.Add(entry);IndexPendingOrder(entry);
                        pendingBytes += ReservedSize(entry.Value); segmentPending[path]++;
                    }
                    staged.Clear(); committed = file.Position; continue;
                }
                if (length <= 0 || length > options.MaximumPayloadBytes + 65536) throw new InvalidDataException("Invalid realtime outbox frame size.");
                if (file.Length - file.Position < 32L + length) break;
                file.ReadExactly(header[4..]); var body = new byte[length]; file.ReadExactly(body);
                if (!CryptographicOperations.FixedTimeEquals(header[4..], SHA256.HashData(body))) throw new InvalidDataException("Realtime source outbox checksum mismatch.");
                var decoded = RealtimeMailboxCodec.Decode(body, options.MaximumPayloadBytes, archiveOnly);
                if (decoded.RecordedAtUtc is { } recorded) archiveRecordedTicks = Math.Max(archiveRecordedTicks, recorded.UtcTicks);
                staged.Add(new(decoded));
            }
            if (file.Length != committed) { file.SetLength(committed); file.Flush(true); }
            if (!archiveOnly && segmentPending[path] == 0)
            {
                // Completed bodies are redundant only because LoadReceipts validated
                // their full-source authority first. No time-based retirement.
                file.Dispose(); File.Delete(path); segmentPending.Remove(path);
            }
            else retainedBytes += file.Length;
        }
    }
    static void WriteDeliveryProof(FileStream file, CapturedRealtimeSource value, string destination)
    {
        Span<byte> row = stackalloc byte[128]; Convert.FromHexString(value.Key).CopyTo(row);
        Convert.FromHexString(value.SourceHash).CopyTo(row[32..64]); Convert.FromHexString(destination).CopyTo(row[64..96]);
        SHA256.HashData(row[..96], row[96..]); file.Write(row);
    }
    void CompactDeliveryReceipts()
    {
        // A completed source receipt proves every handler and replaces its individual proofs.
        // Retain every partial-source proof; never prune by age or arbitrary message count.
        var path = deliveryReceipts.Name; var temporary = path + ".compact";
        using (var file = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None, 65536))
        {
            foreach (var entry in pending.Values)
                foreach (var destination in entry.Delivered) WriteDeliveryProof(file, entry.Value, destination);
            file.Flush(true);
        }
        deliveryReceipts.Dispose(); File.Move(temporary, path, true);
        deliveryReceipts = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read, 65536);
        deliveryReceipts.Position = deliveryReceipts.Length;
    }
    void LoadDeliveryReceipts()
    {
        var complete = deliveryReceipts.Length / 128 * 128;
        if (complete != deliveryReceipts.Length) { deliveryReceipts.SetLength(complete); deliveryReceipts.Flush(true); }
        if (complete > options.MaximumReceiptBytes) throw new IOException("Destination receipt archive exceeds configured capacity.");
        Span<byte> row = stackalloc byte[128]; deliveryReceipts.Position = 0;
        while (deliveryReceipts.Position < complete)
        {
            deliveryReceipts.ReadExactly(row);
            if (!CryptographicOperations.FixedTimeEquals(row[96..], SHA256.HashData(row[..96]))) throw new InvalidDataException("Destination receipt checksum mismatch.");
            if (!pending.TryGetValue(Convert.ToHexStringLower(row[..32]), out var entry)) continue;
            if (entry.Value.SourceHash != Convert.ToHexStringLower(row[32..64])) throw new InvalidDataException("Destination receipt source conflict.");
            var destination = Convert.ToHexStringLower(row[64..96]);
            if (!entry.Destinations.Contains(destination)) throw new InvalidDataException("Destination receipt manifest mismatch.");
            if(entry.Delivered.Add(destination)){liveDestinationProofSlots++;RetirePendingOrder(entry,destination);}
        }
        deliveryReceipts.Position = complete;
    }
    void LoadReceipts()
    {
        // Only the receipt archive is authority; side indexes are derived and rebuilt at startup.
        var complete = receipts.Length / ReceiptSize * ReceiptSize;
        if (complete != receipts.Length) { receipts.SetLength(complete); receipts.Flush(true); }
        if (complete > options.MaximumReceiptBytes) throw new IOException("Realtime receipt archive exceeds configured bounds.");
        Span<byte> row = stackalloc byte[ReceiptSize]; receipts.Position = 0;
        while (receipts.Position < complete)
        {
            var offset = receipts.Position; receipts.ReadExactly(row);
            if (!CryptographicOperations.FixedTimeEquals(row[116..], SHA256.HashData(row[..116]))) throw new InvalidDataException("Realtime receipt archive checksum mismatch.");
            IndexReceipt(row[..32], offset); Remember(Convert.ToHexStringLower(row[..32]), DecodeReceipt(row));
        }
        receipts.Position = receipts.Length;
    }
    void AppendReceipt(CapturedRealtimeSource value)
    {
        Span<byte> row = stackalloc byte[ReceiptSize]; Convert.FromHexString(value.Key).CopyTo(row);
        Convert.FromHexString(value.SourceHash).CopyTo(row[32..64]); value.Source.SourceEventId.TryWriteBytes(row[64..80]); Convert.FromHexString(value.ManifestHash).CopyTo(row[80..112]);
        BinaryPrimitives.WriteInt32LittleEndian(row[112..116], value.Destinations.Length); SHA256.HashData(row[..116], row[116..]);
        var offset = receipts.Position; receipts.Write(row);
        // Index becomes visible only with the flushed receipt while the same capture gate is held below.
        lock (gate) IndexReceipt(row[..32], offset);
    }
    void IndexReceipt(ReadOnlySpan<byte> key, long offset)
    {
        var bits = bloom[key[0]]; var first = BinaryPrimitives.ReadUInt32LittleEndian(key[4..]); var step = BinaryPrimitives.ReadUInt32LittleEndian(key[8..]) | 1;
        for (uint i = 0; i < 4; i++) { var bit = (int)((first + i * step) & 524287); bits[bit >> 3] |= (byte)(1 << (bit & 7)); }
        var bucket = (BinaryPrimitives.ReadUInt32LittleEndian(key) & (LookupBuckets - 1)) * 8L;
        Span<byte> head = stackalloc byte[8]; ReadAt(lookup, head, bucket);
        Span<byte> node = stackalloc byte[LookupNodeBytes]; key.CopyTo(node);
        BinaryPrimitives.WriteInt64LittleEndian(node[32..], offset); head.CopyTo(node[40..]);
        RandomAccess.Write(lookup.SafeFileHandle, node, lookupLength);
        BinaryPrimitives.WriteInt64LittleEndian(head, lookupLength);
        RandomAccess.Write(lookup.SafeFileHandle, head, bucket); lookupLength += LookupNodeBytes;
    }
    RealtimeCaptureReceipt? FindReceipt(string key)
    {
        if (recent.TryGetValue(key, out var value)) return value;
        var hash = Convert.FromHexString(key); var bits = bloom[hash[0]]; var first = BinaryPrimitives.ReadUInt32LittleEndian(hash.AsSpan(4)); var step = BinaryPrimitives.ReadUInt32LittleEndian(hash.AsSpan(8)) | 1;
        for (uint i = 0; i < 4; i++) { var bit = (int)((first + i * step) & 524287); if ((bits[bit >> 3] & (1 << (bit & 7))) == 0) return null; }
        Span<byte> head = stackalloc byte[8]; Span<byte> node = stackalloc byte[LookupNodeBytes]; Span<byte> row = stackalloc byte[ReceiptSize];
        var bucket = (BinaryPrimitives.ReadUInt32LittleEndian(hash) & (LookupBuckets - 1)) * 8L;
        ReadAt(lookup, head, bucket); var next = BinaryPrimitives.ReadInt64LittleEndian(head);
        long visited = 0;
        while (next != 0)
        {
            if (next < LookupHeadsBytes || next + LookupNodeBytes > lookupLength || ++visited > receipts.Length / ReceiptSize + 1)
                throw new InvalidDataException("Derived receipt index is corrupt; rebuild from the authoritative archive.");
            ReadAt(lookup, node, next); next = BinaryPrimitives.ReadInt64LittleEndian(node[40..]);
            if (!node[..32].SequenceEqual(hash)) continue;
            var offset = BinaryPrimitives.ReadInt64LittleEndian(node[32..]);
            if (offset < 0 || offset + ReceiptSize > receipts.Length) throw new InvalidDataException("Derived receipt offset is outside authority.");
            ReadAt(receipts, row, offset);
            if (!row[..32].SequenceEqual(hash)) continue;
            if (!CryptographicOperations.FixedTimeEquals(row[116..], SHA256.HashData(row[..116]))) throw new InvalidDataException("Realtime capture receipt checksum mismatch.");
            value = DecodeReceipt(row); Remember(key, value); return value;
        }
        return null;
    }
    static void ReadAt(FileStream file, Span<byte> bytes, long offset)
    {
        var read = 0;
        while (read < bytes.Length)
        {
            var count = RandomAccess.Read(file.SafeFileHandle, bytes[read..], offset + read);
            if (count == 0) throw new EndOfStreamException(); read += count;
        }
    }
    static RealtimeCaptureReceipt DecodeReceipt(ReadOnlySpan<byte> row) => new(new Guid(row[64..80]), Convert.ToHexStringLower(row[32..64]), Convert.ToHexStringLower(row[80..112]), BinaryPrimitives.ReadInt32LittleEndian(row[112..116]), true);
    void Remember(string key, RealtimeCaptureReceipt receipt)
    {
        if (recent.TryAdd(key, receipt)) recentKeys.Enqueue(key);
        while (recent.Count > RecentLimit) recent.Remove(recentKeys.Dequeue());
    }
    void ThrowIfUnavailable()
    { ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this); if (failed is { } error) throw new IOException("Realtime source journal failed; retained source recovery required.", error); }
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        writes.Writer.TryComplete(); await writerTask.ConfigureAwait(false); segment.Dispose(); lookup.Dispose(); deliveryReceipts.Dispose(); receipts.Dispose(); lease.Dispose();
    }
}
