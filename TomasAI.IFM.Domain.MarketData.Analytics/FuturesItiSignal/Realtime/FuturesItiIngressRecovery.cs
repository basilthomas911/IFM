using System.Buffers.Binary;
using System.Security.Cryptography;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.RealtimePersistence;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Realtime;

/// <summary>Bounded fallback-ingress journal. Frozen commands precede dispatch; completion follows a successful reply.</summary>
public sealed class FuturesItiIngressRecovery : IDisposable
{
    const int MaxFrame = 64 * 1024, PendingLimit = 4096, DefaultCompletedLimit = 8192;
    const uint Magic = 0x31474946; // FIG1
    readonly Dictionary<Guid, Entry> entries = [];
    readonly Queue<Guid> completed = new();
    readonly SortedDictionary<long, Guid> pending = new();
    readonly Dictionary<string, long> completedSources = new(StringComparer.Ordinal);
    readonly HashSet<string> knownSources = new(StringComparer.Ordinal);
    readonly Dictionary<string, (long Ordinal, int Count)> pendingSources = new(StringComparer.Ordinal);
    const int SourceLimit = 1024;
    long retainedBytes;
    const long MaximumRetainedBytes = 16 * 1024 * 1024;
    readonly string? path;
    readonly FileStream? lease;
    FileStream? writer;
    readonly int completedLimit;
    readonly long compactBytes;
    bool faulted;
    long order, lastCompletedTicks;
    readonly SemaphoreSlim gate = new(1, 1);
    sealed record Entry(long Order, string Fingerprint, GenerateFuturesItiSignalCommand Command, bool Completed, int Bytes = 0);
    public int PendingCount => Volatile.Read(ref pendingCount);
    int pendingCount;

    /// <summary>A null directory is explicitly memory-only for isolated tests.</summary>
    public FuturesItiIngressRecovery(string? directory = null, int completedLimit = DefaultCompletedLimit, long compactBytes = 32 * 1024 * 1024)
    {
        if (completedLimit < 1 || compactBytes < 1024) throw new ArgumentOutOfRangeException(nameof(completedLimit));
        this.completedLimit = completedLimit; this.compactBytes = compactBytes;
        if (directory is null) return;
        var root = Path.GetFullPath(directory); Directory.CreateDirectory(root);
        path = Path.Combine(root, "ingress.fig");
        lease = new(Path.Combine(root, "owner.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        try
        {
            long valid = 0;
            if (File.Exists(path))
            {
                using var read = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                var header = new byte[40];
                while (read.Position < read.Length)
                {
                    if (read.Length - read.Position < header.Length) break; // Uncommitted torn tail, never a complete corrupt frame.
                    read.ReadExactly(header);
                    var length = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(4));
                    if (BinaryPrimitives.ReadUInt32LittleEndian(header) != Magic || length is < 1 or > MaxFrame)
                        throw new InvalidDataException("Invalid ITI ingress frame header.");
                    if (read.Length - read.Position < length) break;
                    var body = new byte[length]; read.ReadExactly(body);
                    if (!CryptographicOperations.FixedTimeEquals(SHA256.HashData(body), header.AsSpan(8)))
                        throw new InvalidDataException("Corrupt ITI ingress frame.");
                    Restore(body); valid = read.Position;
                }
            }
            else
            {
                var temporary = path + ".new";
                using (var file = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None)) file.Flush(true);
                AtomicDurableFile.Replace(temporary, path);
            }
            writer = new(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
            writer.SetLength(valid); writer.Position = valid;
        }
        catch { writer?.Dispose(); lease.Dispose(); throw; }
    }

    public async ValueTask<bool> ProcessAsync(Guid id, string fingerprint, DateTime timestamp,
        Func<GenerateFuturesItiSignalCommand?> capture,
        Func<GenerateFuturesItiSignalCommand, CancellationToken, ValueTask<bool>> dispatch, CancellationToken token = default, FuturesItiSourceCursor? cursor = null)
    {
        await gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            AssertHealthy();
            if (entries.TryGetValue(id, out var existing))
            {
                if (!StringComparer.Ordinal.Equals(existing.Fingerprint, fingerprint))
                    throw new InvalidDataException("ITI command identity was reused with conflicting source input.");
                if (existing.Completed) return true;
            }
            else
            {
                // Exact source ordinals, not timestamps, define native order; exchange clocks may repeat or move backwards.
                var key = SourceKey(cursor);
                if (key is not null)
                {
                    if (completedSources.TryGetValue(key, out var high) && cursor!.Ordinal <= high)
                        throw new InvalidDataException("ITI source is outside the retained completed-command window or out of order.");
                    if (!knownSources.Contains(key) && knownSources.Count >= SourceLimit)
                        throw new IOException("ITI source watermark capacity reached; source must retain input.");
                    if (pendingSources.TryGetValue(key, out var waiting) && waiting.Ordinal >= cursor!.Ordinal)
                        throw new InvalidDataException("ITI pending source ordinals are out of order.");
                }
                else
                {
                    if (timestamp.Ticks < lastCompletedTicks)
                        throw new InvalidDataException("Legacy ITI input is outside the retained receipt window or out of order.");
                    if (completed.Count >= completedLimit && entries[completed.Peek()].Command.Timestamp == timestamp)
                        throw new IOException("Legacy ITI receipt capacity for one timestamp reached; source must retain input.");
                }
                if (PendingCount >= PendingLimit) throw new IOException("ITI pending ingress capacity reached; source must retain input.");
                var command = capture();
                if (command is null) return true; // Existing intentional eligibility/input-unavailable policy.
                if (command.CommandId != id || !command.InputsFrozen) throw new InvalidDataException("ITI ingress command is not frozen.");
                var entry = new Entry(++order, fingerprint, command, false);
                var body = Prepare(entry);
                if (retainedBytes + body.Length > MaximumRetainedBytes) throw new IOException("ITI retained-byte capacity reached; source must retain input.");
                Append(body); Add(entry with { Bytes = body.Length });
            }
            return await Drain(dispatch, token).ConfigureAwait(false);
        }
        finally { gate.Release(); }
    }

    public async ValueTask<bool> RecoverAsync(Func<GenerateFuturesItiSignalCommand, CancellationToken, ValueTask<bool>> dispatch,
        CancellationToken token = default)
    {
        await gate.WaitAsync(token).ConfigureAwait(false);
        try { AssertHealthy(); return await Drain(dispatch, token).ConfigureAwait(false); }
        finally { gate.Release(); }
    }
    async ValueTask<bool> Drain(Func<GenerateFuturesItiSignalCommand, CancellationToken, ValueTask<bool>> dispatch, CancellationToken token)
    {
        while (pending.Count > 0)
        {
            var entry = entries[pending.First().Value];
            token.ThrowIfCancellationRequested();
            if (!await dispatch(entry.Command, token).ConfigureAwait(false)) return false;
            token.ThrowIfCancellationRequested();
            var body = new byte[17]; body[0] = 2; entry.Command.CommandId.TryWriteBytes(body.AsSpan(1));
            Append(body); Complete(entry.Command.CommandId);
        }
        return true;
    }
    static string? SourceKey(FuturesItiSourceCursor? cursor) => cursor is { Epoch: var epoch, Ordinal: > 0 }
        && epoch != Guid.Empty && !string.IsNullOrWhiteSpace(cursor.Dataset)
        ? $"{cursor.Dataset}|{cursor.PublisherId}|{cursor.InstrumentId}|{epoch:N}" : null;
    void Add(Entry entry)
    {
        if (SourceKey(entry.Command.SourceCursor) is { } key && knownSources.Add(key) && knownSources.Count > SourceLimit)
            throw new InvalidDataException("ITI source watermark capacity exceeded.");
        entries.Add(entry.Command.CommandId, entry); pending.Add(entry.Order, entry.Command.CommandId);
        if (SourceKey(entry.Command.SourceCursor) is { } source)
        {
            var existing = pendingSources.GetValueOrDefault(source);
            pendingSources[source] = (Math.Max(existing.Ordinal, entry.Command.SourceCursor!.Ordinal), existing.Count + 1);
        }
        pendingCount++; retainedBytes += entry.Bytes;
        if (retainedBytes > MaximumRetainedBytes) throw new InvalidDataException("ITI retained-byte capacity exceeded.");
    }
    void AssertHealthy() { if (faulted) throw new IOException("ITI ingress journal failed; restart recovery is required."); }
    void Complete(Guid id)
    {
        if (!entries.TryGetValue(id, out var entry)) throw new InvalidDataException("ITI completion has no prepared command.");
        if (entry.Completed) return;
        pending.Remove(entry.Order); pendingCount--;
        if (SourceKey(entry.Command.SourceCursor) is { } source)
        {
            var waiting = pendingSources[source];
            if (waiting.Count == 1) pendingSources.Remove(source);
            else pendingSources[source] = (waiting.Ordinal, waiting.Count - 1);
        }
        entries[id] = entry with { Completed = true }; completed.Enqueue(id);
        lastCompletedTicks = Math.Max(lastCompletedTicks, entry.Command.Timestamp.Ticks);
        if (SourceKey(entry.Command.SourceCursor) is { } key)
        {
            completedSources[key] = Math.Max(completedSources.GetValueOrDefault(key), entry.Command.SourceCursor!.Ordinal);
            if (completedSources.Count > SourceLimit) throw new InvalidDataException("ITI source watermark capacity exceeded.");
        }
        while (completed.Count > completedLimit)
        {
            var old = completed.Dequeue(); retainedBytes -= entries[old].Bytes; entries.Remove(old);
        }
    }
    static byte[] Prepare(Entry entry)
    {
        using var memory = new MemoryStream(); using var binary = new BinaryWriter(memory);
        binary.Write((byte)1); binary.Write(entry.Order); binary.Write(entry.Fingerprint);
        var payload = FuturesItiJournalEnvelope.Encode(entry.Command); binary.Write(payload.Length); binary.Write(payload);
        return memory.ToArray();
    }
    void Restore(byte[] body)
    {
        using var memory = new MemoryStream(body); using var binary = new BinaryReader(memory);
        switch (binary.ReadByte())
        {
            case 1:
                var position = binary.ReadInt64(); var fingerprint = binary.ReadString(); var length = binary.ReadInt32();
                if (length is < 1 or > MaxFrame || length != memory.Length - memory.Position) throw new InvalidDataException("Invalid frozen ITI command length.");
                var command = FuturesItiJournalEnvelope.Decode(binary.ReadBytes(length), 0);
                if (entries.ContainsKey(command.CommandId)) throw new InvalidDataException("Duplicate ITI prepare record.");
                Add(new(position, fingerprint, command, false, body.Length)); order = Math.Max(order, position); break;
            case 2: Complete(new Guid(binary.ReadBytes(16))); break;
            case 3: lastCompletedTicks = binary.ReadInt64(); order = binary.ReadInt64(); break;
            case 4:
                var source = binary.ReadString(); completedSources.Add(source, binary.ReadInt64()); knownSources.Add(source);
                if (knownSources.Count > SourceLimit) throw new InvalidDataException("ITI source watermark capacity exceeded.");
                break;
            default: throw new InvalidDataException("Unknown ITI ingress record.");
        }
        if (memory.Position != memory.Length || PendingCount > PendingLimit) throw new InvalidDataException("Invalid ITI ingress record bounds.");
    }
    void Append(byte[] body)
    {
        if (writer is null) return;
        try
        {
            if (writer.Length >= compactBytes) Compact();
            WriteFrame(writer!, body); writer!.Flush(true);
        }
        catch { faulted = true; throw; }
    }
    static void WriteFrame(Stream target, byte[] body)
    {
        if (body.Length is < 1 or > MaxFrame) throw new InvalidDataException("ITI ingress frame exceeds its bound.");
        Span<byte> header = stackalloc byte[40]; BinaryPrimitives.WriteUInt32LittleEndian(header, Magic);
        BinaryPrimitives.WriteInt32LittleEndian(header[4..], body.Length); SHA256.HashData(body, header[8..]);
        target.Write(header); target.Write(body);
    }
    void Compact()
    {
        var temporary = path + ".new";
        using (var file = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            var watermark = new byte[17]; watermark[0] = 3;
            BinaryPrimitives.WriteInt64LittleEndian(watermark.AsSpan(1), lastCompletedTicks);
            BinaryPrimitives.WriteInt64LittleEndian(watermark.AsSpan(9), order); WriteFrame(file, watermark);
            foreach (var source in completedSources)
            {
                using var memory = new MemoryStream(); using var binary = new BinaryWriter(memory);
                binary.Write((byte)4); binary.Write(source.Key); binary.Write(source.Value); WriteFrame(file, memory.ToArray());
            }
            foreach (var id in completed)
            {
                WriteFrame(file, Prepare(entries[id])); var done = new byte[17]; done[0] = 2; id.TryWriteBytes(done.AsSpan(1)); WriteFrame(file, done);
            }
            foreach (var entry in entries.Values.Where(value => !value.Completed).OrderBy(value => value.Order)) WriteFrame(file, Prepare(entry));
            file.Flush(true);
        }
        writer!.Dispose(); writer = null;
        AtomicDurableFile.Replace(temporary, path!);
        writer = new(path!, FileMode.Open, FileAccess.ReadWrite, FileShare.Read); writer.Position = writer.Length;
    }
    public void Dispose() { writer?.Dispose(); lease?.Dispose(); gate.Dispose(); }
}
