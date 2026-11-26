namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Realtime.Projector;

/// <summary>Frozen EOD result receipts. The projector gate serializes all access; receipts remain on disk after completion.</summary>
internal sealed class EodCaptureJournal : IDisposable
{
    const int MaximumPending = 4096, MaximumPendingBytes = 16 * 1024 * 1024;
    readonly string root;
    readonly FileStream lease;
    readonly SortedDictionary<string, EodHistoryEntry> pending = new(StringComparer.Ordinal);
    readonly Dictionary<Guid, EodHistoryEntry> byId = [];
    readonly Dictionary<Guid, long?> durableSequences = [];
    readonly Dictionary<string, Guid[]> progressFiles = [];
    [MessagePack.MessagePackObject(AllowPrivate = true)]
    internal sealed record Progress([property: MessagePack.Key(0)] Guid Id, [property: MessagePack.Key(1)] int Stage, [property: MessagePack.Key(2)] long? Sequence);
    long ordinal = DateTime.UtcNow.Ticks;
    long bytes;
    int disposed;
    internal int Pending => pending.Count;
    internal IReadOnlyList<EodHistoryEntry> Batch(int count) => pending.Values.Take(count).ToArray();
    string Done(Guid id) => Path.Combine(root, "receipts", id.ToString("N")[..2], id.ToString("N") + ".eodr");
    internal EodCaptureJournal(string directory)
    {
        root = Path.Combine(Path.GetFullPath(directory), "recovery-v1");
        Directory.CreateDirectory(Path.Combine(root, "pending"));
        lease = new(Path.Combine(root, "owner.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        try
        {
            foreach (var path in Directory.EnumerateFiles(Path.Combine(root, "pending"), "*.eodr").Order(StringComparer.Ordinal))
            {
                var item = EodHistorySpool.ReadBinaryAsync<EodHistoryEntry>(path, "IFMEOD04").GetAwaiter().GetResult();
                Validate(item); item.FileName = path;
                if (File.Exists(Done(item.Event.Id))) { Validate(EodHistorySpool.ReadBinaryAsync<EodHistoryEntry>(Done(item.Event.Id), "IFMEOD04").GetAwaiter().GetResult()); File.Delete(path); continue; }
                Add(item);
                if (!long.TryParse(Path.GetFileName(path).Split('-')[0], out var sequence)) throw new InvalidDataException("Invalid EOD capture ordinal.");
                ordinal = Math.Max(ordinal, sequence);
            }
            foreach (var path in System.IO.Directory.EnumerateFiles(Path.Combine(root, "pending"), "*.eodg").Order(StringComparer.Ordinal))
            {
                var progress = EodHistorySpool.ReadBinaryAsync<Progress[]>(path, "IFMEOD05").GetAwaiter().GetResult();
                if (progress.Length is < 1 or > 512) throw new InvalidDataException("Invalid EOD progress group.");
                foreach (var update in progress)
                {
                    if (update.Stage is < 1 or > 2 || update.Id == Guid.Empty) throw new InvalidDataException("Invalid EOD progress stage.");
                    if (!byId.TryGetValue(update.Id, out var item))
                    {
                        if (!File.Exists(Done(update.Id))) throw new InvalidDataException("EOD progress has no capture owner.");
                        continue;
                    }
                    item.Stage = Math.Max(item.Stage, update.Stage);
                    if (item.Row is { } row && update.Sequence is { } id)
                    {
                        if (row.HistorySequenceId is { } previous && previous != id) throw new InvalidDataException("Conflicting EOD history ID.");
                        row.HistorySequenceId = id;
                    }
                    durableSequences[update.Id] = item.Row?.HistorySequenceId;
                }
                progressFiles.Add(path, progress.Select(p => p.Id).ToArray());
                ordinal = Math.Max(ordinal, long.Parse(Path.GetFileName(path).Split('-')[0], System.Globalization.CultureInfo.InvariantCulture));
            }
            CleanupProgress();
        }
        catch { EodHistoryMetrics.Pending.Add(-byId.Count); lease.Dispose(); throw; }
    }
    static void Validate(EodHistoryEntry item)
    {
        if (item.SchemaVersion != 1 || item.EventKind is < 0 or > 2 || item.Event.Id == Guid.Empty
            || item.Event.CommandId == Guid.Empty || item.CompletionBytes.Length == 0 || item.Stage is < 0 or > 3
            || item.Completion.Id == Guid.Empty || item.Completion.CommandId != item.Event.CommandId)
            throw new InvalidDataException("Invalid EOD capture receipt.");
    }
    void Add(EodHistoryEntry item)
    {
        var size = item.EventBytes.Length + item.CompletionBytes.Length;
        if (pending.Count >= MaximumPending || bytes + size > MaximumPendingBytes) throw new IOException("EOD capture journal pending capacity exhausted.");
        if (!byId.TryAdd(item.Event.Id, item)) throw new InvalidDataException("Duplicate pending EOD capture ID.");
        pending.Add(item.FileName, item); bytes += size;
        durableSequences[item.Event.Id] = item.Row?.HistorySequenceId;
        EodHistoryMetrics.Pending.Add(1);
    }
    internal async ValueTask<EodHistoryEntry?> FindAsync(Guid id)
    {
        if (byId.TryGetValue(id, out var value)) return value;
        var path = Done(id);
        if (!File.Exists(path)) return null;
        var item = await EodHistorySpool.ReadBinaryAsync<EodHistoryEntry>(path, "IFMEOD04").ConfigureAwait(false);
        Validate(item);
        if (item.Event.Id != id || item.Stage != 3) throw new InvalidDataException("EOD completed receipt identity mismatch.");
        return item;
    }
    internal async ValueTask PrepareAsync(EodHistoryEntry item)
    {
        Validate(item);
        if (pending.Count >= MaximumPending || bytes + item.EventBytes.Length + item.CompletionBytes.Length > MaximumPendingBytes)
            throw new IOException("EOD capture journal pending capacity exhausted.");
        item.FileName = Path.Combine(root, "pending", $"{++ordinal:D19}-{item.Event.Id:N}.eodr");
        try { await EodHistorySpool.SaveBinaryAsync(item.FileName, item, "IFMEOD04").ConfigureAwait(false); }
        catch
        {
            // A durable rename may have committed before the OS reported a flush error.
            if (File.Exists(item.FileName))
            {
                var committed = await EodHistorySpool.ReadBinaryAsync<EodHistoryEntry>(item.FileName, "IFMEOD04").ConfigureAwait(false);
                Validate(committed); committed.FileName = item.FileName; Add(committed);
            }
            throw;
        }
        Add(item);
    }
    internal ValueTask SaveAsync(EodHistoryEntry item) => new(EodHistorySpool.SaveBinaryAsync(item.FileName, item, "IFMEOD04"));
    internal async ValueTask AdvanceAsync(IReadOnlyList<EodHistoryEntry> items, int stage)
    {
        if (items.Count == 0) return;
        var progress = items.Where(item => item.Stage < stage || durableSequences[item.Event.Id] != item.Row?.HistorySequenceId)
            .Select(item => new Progress(item.Event.Id, stage, item.Row?.HistorySequenceId)).ToArray();
        if (progress.Length == 0) return;
        var path = Path.Combine(root, "pending", $"{++ordinal:D19}-progress.eodg");
        await EodHistorySpool.SaveBinaryAsync(path, progress, "IFMEOD05").ConfigureAwait(false);
        progressFiles.Add(path, progress.Select(item => item.Id).ToArray());
        foreach (var item in items) { item.Stage = Math.Max(item.Stage, stage); durableSequences[item.Event.Id] = item.Row?.HistorySequenceId; }
    }
    void CleanupProgress()
    {
        foreach (var entry in progressFiles.Where(entry => !entry.Value.Any(byId.ContainsKey)).ToArray())
        { File.Delete(entry.Key); progressFiles.Remove(entry.Key); }
    }
    internal async ValueTask CompleteAsync(EodHistoryEntry item)
    {
        var path = Done(item.Event.Id); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await EodHistorySpool.SaveBinaryAsync(path, item, "IFMEOD04").ConfigureAwait(false);
        // A crash between receipt commit and unlink is reconciled from the completed receipt at startup.
        File.Delete(item.FileName);
        pending.Remove(item.FileName); byId.Remove(item.Event.Id);
        durableSequences.Remove(item.Event.Id);
        bytes -= item.EventBytes.Length + item.CompletionBytes.Length;
        EodHistoryMetrics.Pending.Add(-1);
        CleanupProgress();
    }
    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        EodHistoryMetrics.Pending.Add(-Pending); lease.Dispose();
    }
}
