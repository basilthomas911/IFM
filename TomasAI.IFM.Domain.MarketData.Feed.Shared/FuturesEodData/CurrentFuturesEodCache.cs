using System.Collections.Concurrent;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
namespace TomasAI.IFM.Domain.MarketData.Feed.Shared;

/// <summary>Process-local, versioned current-session snapshots shared by EOD writers and readers.</summary>
public sealed class CurrentFuturesEodCache
{
    /// <summary>Gets the cache owned by this API process.</summary>
    public static CurrentFuturesEodCache Shared { get; } = new();
    readonly ConcurrentDictionary<(string, DateOnly), Entry> entries = new();
    readonly ConcurrentDictionary<string, DateOnly> latestDates = new();
    long revision;
    /// <summary>Identifies this cache lifetime so recovered history cannot acknowledge another process.</summary>
    public Guid OwnerId { get; } = Guid.NewGuid();
    /// <summary>Reads an immutable current snapshot without accessing storage.</summary>
    public bool TryGet(string contractId, DateOnly valueDate, out FuturesEodDataV2ReadModel? snapshot)
    {
        snapshot = entries.TryGetValue((contractId, valueDate), out var entry) ? entry.Snapshot : null;
        if (snapshot is not null) CurrentEodCacheMetrics.Hits.Add(1); else CurrentEodCacheMetrics.Misses.Add(1);
        return snapshot is not null;
    }
    /// <summary>Publishes an ordered live snapshot and returns its persistence version.</summary>
    public long Publish(FuturesEodDataV2ReadModel snapshot)
    {
        using var mutation = TomasAI.IFM.Shared.EventModelActor.RealtimeActorGeneration.EnterMutation();
        var version = Interlocked.Increment(ref revision);
        var now = DateTime.UtcNow;
        latestDates.TryGetValue(snapshot.ContractId, out var previousDate);
        var latest = latestDates.AddOrUpdate(snapshot.ContractId, snapshot.ValueDate, (_, old) => snapshot.ValueDate > old ? snapshot.ValueDate : old);
        if (latest > previousDate)
            foreach (var key in entries.Keys.Where(key => key.Item1 == snapshot.ContractId && key.Item2 < latest.AddDays(-1)))
                entries.TryRemove(key, out _);
        entries.AddOrUpdate((snapshot.ContractId, snapshot.ValueDate),
            _ => new(snapshot, version, 0, now), (_, old) => version > old.Version ? new(snapshot, version, old.PersistedVersion, now) : old);
        return version;
    }
    /// <summary>Records persistence without replacing a newer live snapshot.</summary>
    public void MarkPersisted(string contractId, DateOnly valueDate, long version)
    {
        if (entries.TryGetValue((contractId, valueDate), out var old))
            entries.AddOrUpdate((contractId, valueDate), old, (_, current) => current with { PersistedVersion = Math.Max(current.PersistedVersion, version) });
    }
    /// <summary>Loads storage only on a miss; a concurrent live update always wins.</summary>
    public async Task<FuturesEodDataV2ReadModel?> ReadAsync(string contractId, DateOnly valueDate,
        Func<Task<FuturesEodDataV2ReadModel?>> fallback)
    {
        if (TryGet(contractId, valueDate, out var current)) return current;
        FuturesEodDataV2ReadModel? stored;
        try { stored = await fallback().ConfigureAwait(false); }
        catch when (TryGet(contractId, valueDate, out current)) { return current; }
        // Storage fallback is not a live-cache seed: legacy writers may update it independently.
        return TryGet(contractId, valueDate, out current) ? current : stored;
    }
    /// <summary>Reads observation and persistence metadata without treating persistence as live readiness.</summary>
    public bool TryGetStatus(string contractId, DateOnly valueDate, out DateTime observedAtUtc, out bool isPersisted)
    {
        var found = entries.TryGetValue((contractId, valueDate), out var entry);
        observedAtUtc = entry?.ObservedAtUtc ?? default;
        isPersisted = entry is not null && entry.PersistedVersion >= entry.Version;
        return found;
    }
    sealed record Entry(FuturesEodDataV2ReadModel Snapshot, long Version, long PersistedVersion, DateTime ObservedAtUtc);
}
