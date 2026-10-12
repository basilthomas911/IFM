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
        try { await (SaveAsync(entry)).ConfigureAwait(false); EodHistoryMetrics.Pending.Add(1); }
        catch { Interlocked.Decrement(ref pending); throw; }
    }

    /// <summary>Reads a bounded ordered batch without loading the entire backlog into memory.</summary>
    public async Task<List<EodHistoryEntry>> ReadBatchAsync(int maximum)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maximum, 1);
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
            var stream = File.OpenRead(name);
            await using var configuredStream = stream.ConfigureAwait(false);
            var entry = await (JsonSerializer.DeserializeAsync<EodHistoryEntry>(stream)).ConfigureAwait(false)
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
        var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None, 4096,
            FileOptions.Asynchronous | FileOptions.WriteThrough);
        await using (stream.ConfigureAwait(false))
        {
            await (JsonSerializer.SerializeAsync(stream, entry)).ConfigureAwait(false);
            await (stream.FlushAsync()).ConfigureAwait(false);
            #pragma warning disable CA1849 // FlushAsync does not request physical disk durability for the outage spool.
            stream.Flush(flushToDisk: true);
            #pragma warning restore CA1849
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
