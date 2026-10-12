using System.Collections.Concurrent;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;

/// <summary>Records only successfully persisted and published closed observations.</summary>
public static class FuturesTradeSessionBarPublicationProgress
{
    static readonly ConcurrentDictionary<(string Contract, DateOnly Date, TimeFrameType Frame),
        (DateTimeOffset Through, DateTime PublishedUtc)> Published = new();
    public static void Record(FuturesTradeSessionBarReadModel bar)
    {
        ArgumentNullException.ThrowIfNull(bar);
        if (Published.Count > 256)
            foreach (var key in Published.Keys.Where(x => x.Date < bar.ValueDate)) Published.TryRemove(key, out _);
        Published[(bar.ContractId, bar.ValueDate, bar.TimeFrame)] = (bar.LastMarketEventUtc, DateTime.UtcNow);
    }
    public static (DateTimeOffset Through, DateTime PublishedUtc)? Get(string contract, DateOnly date, TimeFrameType frame)
        => Published.TryGetValue((contract, date, frame), out var value) ? value : null;
}
