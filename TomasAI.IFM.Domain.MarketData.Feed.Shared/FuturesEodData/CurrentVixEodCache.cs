using System.Collections.Concurrent;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
namespace TomasAI.IFM.Domain.MarketData.Feed.Shared;

/// <summary>Current VX snapshots, isolated by contract and trading date.</summary>
public sealed class CurrentVixEodCache
{
    /// <summary>Gets the cache shared by current-session readers.</summary>
    public static CurrentVixEodCache Shared { get; } = new();
    readonly ConcurrentDictionary<(string, DateOnly), VixFuturesEodDataReadModel> entries = new();
    readonly ConcurrentDictionary<string, DateOnly> latestDates = new();
    /// <summary>Reads an immutable current VX snapshot.</summary>
    public bool TryGet(string contract, DateOnly date, out VixFuturesEodDataReadModel? value)
        {
        var found = entries.TryGetValue((contract, date), out value);
        if (found) CurrentEodCacheMetrics.Hits.Add(1); else CurrentEodCacheMetrics.Misses.Add(1);
        return found;
    }
    /// <summary>Publishes one snapshot from the ordered contract worker.</summary>
    public void Publish(VixFuturesEodDataReadModel snapshot)
        {
        latestDates.TryGetValue(snapshot.ContractId, out var previous);
        var latest = latestDates.AddOrUpdate(snapshot.ContractId, snapshot.ValueDate, (_, old) => snapshot.ValueDate > old ? snapshot.ValueDate : old);
        if (latest > previous)
            foreach (var key in entries.Keys.Where(key => key.Item1 == snapshot.ContractId && key.Item2 < latest.AddDays(-1)))
                entries.TryRemove(key, out _);
        entries[(snapshot.ContractId, snapshot.ValueDate)] = snapshot;
    }
    /// <summary>Loads persisted state on a miss without overwriting a concurrent live observation.</summary>
    public async Task<VixFuturesEodDataReadModel?> ReadAsync(string contract, DateOnly date,
        Func<Task<VixFuturesEodDataReadModel?>> fallback)
    {
        if (TryGet(contract, date, out var current)) return current;
        VixFuturesEodDataReadModel? stored;
        try { stored = await fallback().ConfigureAwait(false); }
        catch when (TryGet(contract, date, out current)) { return current; }
        // Only the ordered live writer owns cache entries; fallback reads remain fresh.
        return TryGet(contract, date, out current) ? current : stored;
    }
}
