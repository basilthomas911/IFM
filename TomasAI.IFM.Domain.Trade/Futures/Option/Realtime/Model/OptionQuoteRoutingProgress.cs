using System.Collections.Concurrent;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Realtime.Model;

/// <summary>Actor-owned evidence that quote routing reaches each contract's durable position owner.</summary>
public sealed class OptionQuoteRoutingProgress
{
    readonly ConcurrentDictionary<string,long> failures = new(StringComparer.Ordinal);
    /// <summary>Clears only the successful contract's failure interval; other contracts cannot hide its stalled dispatch.</summary>
    public void Record(string contractId, bool success, DateTime observedAtUtc)
    {
        if (success) failures.TryRemove(contractId,out _);
        else if (failures.Count < 4096) failures.TryAdd(contractId,observedAtUtc.Ticks);
    }
    /// <summary>Allows transient failures but rejects thirty seconds without an accepted reply for any failed contract.</summary>
    public bool IsHealthy(DateTime nowUtc)
        => failures.Values.All(since => nowUtc.Ticks - since < TimeSpan.FromSeconds(30).Ticks);
}
