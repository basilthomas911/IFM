using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;

namespace TomasAI.IFM.Application.Storage.TradePlanDb;

/// <summary>Bounds chart history reads independently of grid paging and trade-plan persistence.</summary>
internal static class TradePlanChartCache
{
    internal sealed record Series(DateTime EndUtc, StrategyTradePlanChartPoint[] Points);
    private sealed record Entry(long LoadedAt, Series Series);
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static readonly Dictionary<(string, StrategyPositionId, TradeStrategyKind, DateOnly, long?), Entry> Entries = [];

    /// <summary>Shares chart samples for sixty seconds within the same database, position, date and revision scope.</summary>
    internal static async Task<Series> ReadAsync(
        (string, StrategyPositionId, TradeStrategyKind, DateOnly, long?) key,
        Func<Task<Series>> load, CancellationToken cancellationToken)
    {
        await Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var now = Environment.TickCount64;
            if (Entries.TryGetValue(key, out var cached) && now - cached.LoadedAt < 60_000)
                return cached.Series;
            var series = await load().ConfigureAwait(false);
            foreach (var expired in Entries.Where(pair => now - pair.Value.LoadedAt >= 60_000).Select(pair => pair.Key).ToArray())
                Entries.Remove(expired);
            if (Entries.Count >= 128)
                Entries.Remove(Entries.MinBy(pair => pair.Value.LoadedAt).Key);
            Entries[key] = new(Environment.TickCount64, series);
            return series;
        }
        finally { Gate.Release(); }
    }
}
