using System.Collections.Concurrent;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Realtime;

/// <summary>Actor-local last successfully projected ES EOD value.</summary>
internal sealed class FuturesEodTradeReadCache
{
    readonly ConcurrentDictionary<FuturesEodDataId, Entry> _values = new();

    internal bool TryGet(FuturesEodDataId id, long sequence, out FuturesEodDataV2ReadModel? value,
        out bool stale)
    {
        if (_values.TryGetValue(id, out var entry))
        {
            value = entry.Value;
            stale = sequence > 0 && entry.Sequence > 0 && sequence <= entry.Sequence;
            return true;
        }
        value = null;
        stale = false;
        return false;
    }

    internal void Set(FuturesEodDataId id, long sequence, FuturesEodDataV2ReadModel value)
        => _values[id] = new Entry(sequence, value);

    internal void Invalidate(FuturesEodDataId id) => _values.TryRemove(id, out _);

    internal void Clear() => _values.Clear();

    readonly record struct Entry(long Sequence, FuturesEodDataV2ReadModel Value);
}
