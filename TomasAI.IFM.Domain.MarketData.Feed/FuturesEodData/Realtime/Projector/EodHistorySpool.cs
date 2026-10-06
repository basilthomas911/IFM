using MessagePack;
using System.Text.Json;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Realtime.Projector;

/// <summary>Local history outbox. Storage outages retain observations independently of the live cache.</summary>
internal sealed class EodHistorySpool : IDisposable
{
    readonly string directory;
    readonly FileStream lease;
    long ordinal = DateTime.UtcNow.Ticks;
    int pending;

    /// <summary>Opens an exclusively owned spool and recovers the pending observation count.</summary>
    public EodHistorySpool(string directory)
    {
        this.directory = Path.GetFullPath(directory);
        Directory.CreateDirectory(this.directory);
        lease = new FileStream(Path.Combine(this.directory, "writer.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        pending = Directory.EnumerateFiles(this.directory, "*.json").Count();
        EodHistoryMetrics.Pending.Add(pending);
    }

    /// <summary>Gets the number of observations awaiting persistence.</summary>
    public int Pending => Volatile.Read(ref pending);

    /// <summary>Atomically saves a live observation to local disk; never contacts a database.</summary>
    public async Task AppendAsync(EodHistoryEntry entry)
    {
        var sequence = Interlocked.Increment(ref ordinal);
        entry.FileName = Path.Combine(directory, $"{sequence:D19}-{entry.Event.Id:N}.json");
        Interlocked.Increment(ref pending);
        try { await SaveAsync(entry); EodHistoryMetrics.Pending.Add(1); }
        catch { Interlocked.Decrement(ref pending); throw; }
    }

    /// <summary>Reads a bounded ordered batch without loading the entire backlog into memory.</summary>
    public async Task<List<EodHistoryEntry>> ReadBatchAsync(int maximum)
    {
        if (maximum < 1) throw new ArgumentOutOfRangeException(nameof(maximum));
        // Keep only the oldest bounded set of filenames, even during a long storage outage.
        var oldest = new PriorityQueue<string, string>(Comparer<string>.Create((left, right) => StringComparer.Ordinal.Compare(right, left)));
        foreach (var name in Directory.EnumerateFiles(directory, "*.json"))
        {
            oldest.Enqueue(name, name);
            if (oldest.Count > maximum) oldest.Dequeue();
        }
        var names = oldest.UnorderedItems.Select(item => item.Element).Order(StringComparer.Ordinal);
        var result = new List<EodHistoryEntry>(maximum);
        foreach (var name in names)
        {
            await using var stream = File.OpenRead(name);
            var entry = await JsonSerializer.DeserializeAsync<EodHistoryEntry>(stream)
                ?? throw new InvalidDataException($"Invalid EOD spool record: {name}");
            if (entry.SchemaVersion != 1) throw new InvalidDataException($"Unsupported EOD spool schema: {entry.SchemaVersion}");
            entry.FileName = name;
            result.Add(entry);
        }
        return result;
    }

    /// <summary>Saves allocated history IDs before submitting any database mutation.</summary>
    public static async Task SaveAsync(EodHistoryEntry entry)
    {
        var temporary = entry.FileName + ".tmp";
        await using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None, 4096,
            FileOptions.Asynchronous | FileOptions.WriteThrough))
        {
            await JsonSerializer.SerializeAsync(stream, entry);
            await stream.FlushAsync();
            stream.Flush(flushToDisk: true);
        }
        File.Move(temporary, entry.FileName, overwrite: true);
    }

    /// <summary>Removes a successfully persisted observation after its best-effort completion notification.</summary>
    public void Acknowledge(EodHistoryEntry entry)
    {
        File.Delete(entry.FileName);
        Interlocked.Decrement(ref pending);
        EodHistoryMetrics.Pending.Add(-1);
    }

    /// <summary>Releases this writer's exclusive ownership without deleting pending history.</summary>
    public void Dispose() { EodHistoryMetrics.Pending.Add(-Pending); lease.Dispose(); }
}

/// <summary>Versioned disk record carrying exact event bytes, calculated snapshots and stable retry identity.</summary>
internal sealed class EodHistoryEntry
{
    public int SchemaVersion { get; init; } = 1;
    public int EventKind { get; init; }
    public byte[] EventBytes { get; init; } = [];
    public BufferedFuturesEodRow? Row { get; init; }
    public VixFuturesEodDataReadModel? Vx { get; init; }
    public long CacheVersion { get; init; }
    public Guid CacheOwnerId { get; init; }
    public DateTime ObservedAtUtc { get; init; } = DateTime.UtcNow;
    [System.Text.Json.Serialization.JsonIgnore] public string FileName { get; set; } = "";
    [System.Text.Json.Serialization.JsonIgnore] public IEvent Event => EventKind switch
    {
        0 => MessagePackSerializer.Deserialize<FuturesEodDataInsertedEvent>(EventBytes),
        1 => MessagePackSerializer.Deserialize<FuturesEodSessionStatisticsUpdatedEvent>(EventBytes),
        2 => MessagePackSerializer.Deserialize<VixFuturesEodDataInsertedEvent>(EventBytes),
        _ => throw new InvalidDataException($"Unsupported EOD spool event kind: {EventKind}")
    };

    public static EodHistoryEntry Create(IEvent source, BufferedFuturesEodRow? row, VixFuturesEodDataReadModel? vx, long version)
        => source switch
        {
            FuturesEodDataInsertedEvent e => new() { EventKind = 0, EventBytes = MessagePackSerializer.Serialize(e), Row = row, CacheVersion = version, CacheOwnerId = CurrentFuturesEodCache.Shared.OwnerId },
            FuturesEodSessionStatisticsUpdatedEvent e => new() { EventKind = 1, EventBytes = MessagePackSerializer.Serialize(e), Row = row, CacheVersion = version, CacheOwnerId = CurrentFuturesEodCache.Shared.OwnerId },
            VixFuturesEodDataInsertedEvent e => new() { EventKind = 2, EventBytes = MessagePackSerializer.Serialize(e), Vx = vx },
            _ => throw new InvalidOperationException($"Unsupported EOD history event: {source.GetType().Name}")
        };
}
