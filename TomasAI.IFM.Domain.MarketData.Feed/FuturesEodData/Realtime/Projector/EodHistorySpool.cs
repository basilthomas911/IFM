using System.Buffers;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using MessagePack;
using MessagePack.Resolvers;
using System.Runtime.InteropServices;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Realtime.Projector;

/// <summary>Durable, bounded history outbox. Legacy JSON and single-record binary files remain readable.</summary>
internal sealed class EodHistorySpool : IDisposable
{
    const int MaximumIndexedFiles = 4096, MaximumCachedSegments = 16, MaximumSegmentItems = 64;
    static readonly MessagePackSerializerOptions Options = MessagePackSerializerOptions.Standard
        .WithResolver(CompositeResolver.Create(EodSpoolMessagePackResolver.Instance, StandardResolver.Instance));
    sealed class FrameTooLargeException : Exception { }
    // Persist rename metadata before retiring originals. No cross-volume copies are allowed.
#pragma warning disable SYSLIB1054 // Keep these small platform calls independent of unsafe/source-generator settings.
    [DllImport("kernel32.dll", EntryPoint = "MoveFileExW", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool MoveFileEx(string source, string target, uint flags);
    [DllImport("libc", EntryPoint = "open", SetLastError = true)]
    static extern int OpenDirectory([MarshalAs(UnmanagedType.LPUTF8Str)] string path, int flags);
    [DllImport("libc", EntryPoint = "fsync", SetLastError = true)] static extern int SyncDirectory(int descriptor);
    [DllImport("libc", EntryPoint = "close")] static extern int CloseDirectory(int descriptor);
#pragma warning restore SYSLIB1054
    static void DurableReplace(string source, string target)
    {
        if (OperatingSystem.IsWindows())
        {
            static string NativePath(string path) => path.StartsWith(@"\\?\", StringComparison.Ordinal) ? path
                : path.StartsWith(@"\\", StringComparison.Ordinal) ? @"\\?\UNC\" + path[2..] : @"\\?\" + path;
            if (!MoveFileEx(NativePath(source), NativePath(target), 1 | 8)) throw new IOException("Durable EOD rename failed.", new System.ComponentModel.Win32Exception(Marshal.GetLastPInvokeError()));
            return;
        }
        File.Move(source, target, true);
        var descriptor = OpenDirectory(Path.GetDirectoryName(target)!, 0);
        if (descriptor < 0) throw new IOException("Cannot open the EOD journal directory for durable replacement.");
        try { if (SyncDirectory(descriptor) != 0) throw new IOException("Cannot flush the EOD journal directory."); }
        finally { _ = CloseDirectory(descriptor); }
    }
    sealed class FrameWriter : IBufferWriter<byte>, IDisposable
    {
        public byte[] Buffer { get; } = ArrayPool<byte>.Shared.Rent(64_044);
        public int Written { get; private set; } = 44;
        public void Advance(int count) { if (count < 0 || count > 64_044 - Written) throw new FrameTooLargeException(); Written += count; }
        void Require(int hint) { if (hint < 0 || Math.Max(1, hint) > 64_044 - Written) throw new FrameTooLargeException(); }
        public Memory<byte> GetMemory(int sizeHint = 0) { Require(sizeHint); return Buffer.AsMemory(Written, 64_044 - Written); }
        public Span<byte> GetSpan(int sizeHint = 0) { Require(sizeHint); return Buffer.AsSpan(Written, 64_044 - Written); }
        public void Dispose() { Buffer.AsSpan(0, Written).Clear(); ArrayPool<byte>.Shared.Return(Buffer); }
    }
    [MessagePackObject(AllowPrivate = true)]
    internal sealed record SegmentRecord([property: Key(0)] EodHistoryEntry[] Items, [property: Key(1)] string[] OriginalNames);
    internal sealed class SegmentContext(string path, SegmentRecord record)
    {
        public string Path { get; } = path;
        public SegmentRecord Record { get; } = record;
        public bool[] Acknowledged { get; } = new bool[record.Items.Length];
        public int Remaining { get; set; } = record.Items.Length;
    }
    readonly string directory;
    readonly FileStream lease;
    readonly Lock indexGate = new();
    readonly SortedSet<string> index = new(StringComparer.Ordinal);
    // One history consumer owns decoded segments; producer appends touch only the filename index.
    readonly Dictionary<string, SegmentContext> segments = new(StringComparer.Ordinal);
    bool indexComplete = true;
    long ordinal = DateTime.UtcNow.Ticks;
    int pending;
    public int Pending => Volatile.Read(ref pending);

    public EodHistorySpool(string directory)
    {
        this.directory = Path.GetFullPath(directory); Directory.CreateDirectory(this.directory);
        lease = new FileStream(Path.Combine(this.directory, "writer.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        try
        {
            // Completion markers are durable before the final unlink; a crash may
            // leave one behind, but must never replay an already retired source file.
            foreach (var path in Directory.EnumerateFiles(this.directory, "*.done"))
            {
                var completed = ReadBinary<SegmentRecord>(path, "IFMEOD03");
                ValidateSegment(completed); CleanupOriginals(completed);
                File.Delete(Path.ChangeExtension(path, ".seg")); File.Delete(path);
            }
            // A committed segment supersedes exactly its named originals, even if a crash
            // happened before deleting any of them. Validate before reclaiming source files.
            foreach (var path in Directory.EnumerateFiles(this.directory, "*.seg"))
            {
                var record = ReadBinary<SegmentRecord>(path, "IFMEOD03");
                ValidateSegment(record); CleanupOriginals(record);
                pending = checked(pending + record.Items.Length); Index(path, true);
            }
            foreach (var path in PendingFiles().Where(path => Path.GetExtension(path) != ".seg")) { pending++; Index(path, true); }
            EodHistoryMetrics.Pending.Add(pending);
        }
        catch { lease.Dispose(); throw; }
    }
    IEnumerable<string> PendingFiles() => Directory.EnumerateFiles(directory)
        .Where(path => Path.GetExtension(path) is ".json" or ".bin" or ".seg");
    void Index(string path, bool knownOldest = false)
    {
        lock (indexGate)
        {
            // Do not let newly appended files overtake the unindexed tail of a long outage.
            if (!knownOldest && !indexComplete && (index.Count == 0 || StringComparer.Ordinal.Compare(path, index.Max) > 0)) return;
            index.Add(path);
            if (index.Count > MaximumIndexedFiles) { index.Remove(index.Max!); indexComplete = false; }
        }
    }
    public async Task AppendAsync(EodHistoryEntry entry)
    {
        var sequence = Interlocked.Increment(ref ordinal);
        entry.FileName = Path.Combine(directory, $"{sequence:D19}-{entry.Event.Id:N}.bin"); entry.Owner = this;
        Interlocked.Increment(ref pending);
        try { await SaveAsync(entry).ConfigureAwait(false); Index(entry.FileName); EodHistoryMetrics.Pending.Add(1); }
        catch { Interlocked.Decrement(ref pending); throw; }
    }
    public async Task<List<EodHistoryEntry>> ReadBatchAsync(int maximum)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maximum, 1); maximum = Math.Min(maximum, MaximumIndexedFiles);
        bool refill;
        lock (indexGate) { refill = index.Count == 0 && Pending > 0; if (refill) indexComplete = true; }
        if (refill) foreach (var path in PendingFiles()) Index(path, true);
        string[] names; lock (indexGate) names = index.Take(maximum).ToArray();
        var result = new List<EodHistoryEntry>(maximum);
        foreach (var name in names)
        {
            if (result.Count >= maximum) break;
            if (segments.Count >= MaximumCachedSegments && !segments.ContainsKey(name)) break;
            if (Path.GetExtension(name) == ".seg")
            {
                if (!segments.TryGetValue(name, out var context))
                {
                    var record = await ReadBinaryAsync<SegmentRecord>(name, "IFMEOD03").ConfigureAwait(false);
                    ValidateSegment(record); context = Attach(name, record);
                }
                for (var n = 0; n < context.Record.Items.Length && result.Count < maximum; n++)
                    if (!context.Acknowledged[n]) result.Add(context.Record.Items[n]);
            }
            else
            {
                EodHistoryEntry? entry;
                if (Path.GetExtension(name) == ".json")
                {
                    await using var file = new FileStream(name, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous);
                    entry = await JsonSerializer.DeserializeAsync<EodHistoryEntry>(file).ConfigureAwait(false);
                }
                else entry = await ReadBinaryAsync<EodHistoryEntry>(name, "IFMEOD02").ConfigureAwait(false);
                if (entry is null || entry.SchemaVersion != 1) throw new InvalidDataException($"Invalid EOD spool record: {name}");
                entry.FileName = name; entry.Owner = this; result.Add(entry);
            }
        }
        return result;
    }
    SegmentContext Attach(string path, SegmentRecord record)
    {
        var context = new SegmentContext(path, record); segments.Add(path, context);
        for (var n = 0; n < record.Items.Length; n++)
        {
            var entry = record.Items[n]; entry.Owner = this; entry.Segment = context; entry.SegmentIndex = n;
            entry.FileName = Path.Combine(directory, record.OriginalNames[n]);
        }
        return context;
    }
    void ValidateSegment(SegmentRecord record)
    {
        if (record.Items.Length is < 1 or > MaximumSegmentItems || record.Items.Length != record.OriginalNames.Length
            || record.OriginalNames.Distinct(StringComparer.Ordinal).Count() != record.OriginalNames.Length)
            throw new InvalidDataException("Invalid EOD segment membership.");
        foreach (var name in record.OriginalNames)
            if (name != Path.GetFileName(name) || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
                || Path.GetExtension(name) is not (".json" or ".bin"))
                throw new InvalidDataException("An EOD segment source escaped its spool.");
        if (record.Items.Any(entry => entry is null || entry.SchemaVersion != 1 || entry.EventBytes.Length == 0
            || entry.EventKind is < 0 or > 2)) throw new InvalidDataException("Invalid EOD segment schema.");
    }
    void CleanupOriginals(SegmentRecord record)
    {
        foreach (var name in record.OriginalNames) File.Delete(Path.Combine(directory, name));
    }
    internal static async Task<T> ReadBinaryAsync<T>(string path, string magic)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous);
        var length = checked((int)stream.Length);
        if (length is < 45 or > 64_044) throw new InvalidDataException("Invalid EOD frame length.");
        var bytes = ArrayPool<byte>.Shared.Rent(length);
        try
        {
            await stream.ReadExactlyAsync(bytes.AsMemory(0, length)).ConfigureAwait(false);
            return DecodeBinary<T>(bytes, length, magic);
        }
        finally { bytes.AsSpan(0, length).Clear(); ArrayPool<byte>.Shared.Return(bytes); }
    }
    static T ReadBinary<T>(string path, string magic)
    {
        using var stream = File.OpenRead(path);
        var length = checked((int)stream.Length);
        if (length is < 45 or > 64_044) throw new InvalidDataException("Invalid EOD frame length.");
        var bytes = ArrayPool<byte>.Shared.Rent(length);
        try { stream.ReadExactly(bytes.AsSpan(0, length)); return DecodeBinary<T>(bytes, length, magic); }
        finally { bytes.AsSpan(0, length).Clear(); ArrayPool<byte>.Shared.Return(bytes); }
    }
    static T DecodeBinary<T>(byte[] bytes, int length, string magic)
    {
        if (magic.Length != 8 || BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(8)) != length - 44)
            throw new InvalidDataException("Invalid EOD frame header.");
        for (var n = 0; n < 8; n++) if (bytes[n] != magic[n]) throw new InvalidDataException("Invalid EOD frame version.");
        Span<byte> digest = stackalloc byte[32]; SHA256.HashData(bytes.AsSpan(44, length - 44), digest);
        if (!digest.SequenceEqual(bytes.AsSpan(12, 32))) throw new InvalidDataException("EOD frame checksum mismatch.");
        return MessagePackSerializer.Deserialize<T>(bytes.AsMemory(44, length - 44), Options);
    }
    internal static async Task SaveBinaryAsync<T>(string path, T value, string magic)
    {
        using var frame = new FrameWriter(); MessagePackSerializer.Serialize(frame, value, Options);
        System.Text.Encoding.ASCII.GetBytes(magic, frame.Buffer);
        BinaryPrimitives.WriteInt32LittleEndian(frame.Buffer.AsSpan(8), frame.Written - 44);
        SHA256.HashData(frame.Buffer.AsSpan(44, frame.Written - 44), frame.Buffer.AsSpan(12, 32));
        var temporary = path + ".tmp";
        await using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None, 4096,
            FileOptions.Asynchronous | FileOptions.WriteThrough))
        {
            await stream.WriteAsync(frame.Buffer.AsMemory(0, frame.Written)).ConfigureAwait(false);
            await stream.FlushAsync().ConfigureAwait(false);
#pragma warning disable CA1849 // Admission and replacement require an actual physical flush.
            stream.Flush(true);
#pragma warning restore CA1849
        }
        DurableReplace(temporary, path);
    }
    public static async Task SaveAsync(EodHistoryEntry entry)
    {
        if (entry.Segment is { } context) { await SaveBinaryAsync(context.Path, context.Record, "IFMEOD03").ConfigureAwait(false); return; }
        if (Path.GetExtension(entry.FileName) != ".json") { await SaveBinaryAsync(entry.FileName, entry, "IFMEOD02").ConfigureAwait(false); return; }
        var temporary = entry.FileName + ".tmp";
        await using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None, 4096,
            FileOptions.Asynchronous | FileOptions.WriteThrough))
        {
            await JsonSerializer.SerializeAsync(stream, entry).ConfigureAwait(false); await stream.FlushAsync().ConfigureAwait(false);
#pragma warning disable CA1849
            stream.Flush(true);
#pragma warning restore CA1849
        }
        DurableReplace(temporary, entry.FileName);
    }
    static bool TooLarge(Exception error) => error is FrameTooLargeException || error.InnerException is { } inner && TooLarge(inner);
    public static async Task SaveBatchAsync(IReadOnlyCollection<EodHistoryEntry> entries)
    {
        var saved = new HashSet<SegmentContext>(); var loose = new List<EodHistoryEntry>();
        foreach (var entry in entries)
            if (entry.Segment is { } context)
            { if (saved.Add(context)) await SaveBinaryAsync(context.Path, context.Record, "IFMEOD03").ConfigureAwait(false); }
            else loose.Add(entry);
        if (loose.Count == 0) return;
        var owner = loose[0].Owner;
        if (owner is null || loose.Any(entry => !ReferenceEquals(entry.Owner, owner)))
        { foreach (var entry in loose) await SaveAsync(entry).ConfigureAwait(false); return; }
        for (var offset = 0; offset < loose.Count;)
        {
            if (owner.segments.Count >= MaximumCachedSegments)
            { await SaveAsync(loose[offset++]).ConfigureAwait(false); continue; }
            var count = Math.Min(MaximumSegmentItems, loose.Count - offset);
            while (true)
            {
                var items = loose.GetRange(offset, count).ToArray();
                var record = new SegmentRecord(items, items.Select(entry => Path.GetFileName(entry.FileName)).ToArray());
                owner.ValidateSegment(record);
                var path = Path.Combine(owner.directory, Path.GetFileNameWithoutExtension(items[0].FileName) + "-" + Guid.NewGuid().ToString("N") + ".seg");
                try { await SaveBinaryAsync(path, record, "IFMEOD03").ConfigureAwait(false); }
                catch (Exception error) when (TooLarge(error))
                {
                    if (count > 1) { count /= 2; continue; }
                    await SaveAsync(items[0]).ConfigureAwait(false); offset++; break;
                }
                owner.Attach(path, record);
                lock (owner.indexGate) foreach (var entry in items) owner.index.Remove(entry.FileName);
                owner.Index(path, true);
                owner.CleanupOriginals(record); // Source files survive until the segment is durable.
                offset += count; break;
            }
        }
    }
    public void Acknowledge(EodHistoryEntry entry)
    {
        if (entry.Segment is { } context)
        {
            if (context.Acknowledged[entry.SegmentIndex]) return;
            if (context.Remaining == 1)
            {
                // Retain the group if retirement of any superseded source failed.
                CleanupOriginals(context.Record);
                var retired = Path.ChangeExtension(context.Path, ".done");
                DurableReplace(context.Path, retired);
                try { File.Delete(retired); }
                catch (IOException) { EodHistoryMetrics.Failures.Add(1, new KeyValuePair<string, object?>("stage", "retire_cleanup")); }
                lock (indexGate) index.Remove(context.Path);
                segments.Remove(context.Path);
            }
            context.Acknowledged[entry.SegmentIndex] = true; context.Remaining--;
        }
        else { File.Delete(entry.FileName); lock (indexGate) index.Remove(entry.FileName); }
        Interlocked.Decrement(ref pending); EodHistoryMetrics.Pending.Add(-1);
    }
    public void Dispose() { EodHistoryMetrics.Pending.Add(-Pending); lease.Dispose(); }
}
