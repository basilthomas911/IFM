namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;

/// <summary>Qualifies indicator seed cache requests before database access.</summary>
public static class IndicatorSeedRequestQualification
{
    /// <summary>Requires a versioned, bounded read selection.</summary>
    public static void RequireSelection(string sourceVersion, DateTimeOffset[] intervalEndsUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceVersion);
        ArgumentNullException.ThrowIfNull(intervalEndsUtc);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(intervalEndsUtc.Length, 64);
    }

    /// <summary>Requires unique complete intervals within one contract, timeframe and value date.</summary>
    public static void RequireBatch(string sourceVersion, FuturesTradeSessionBarReadModel[] bars)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceVersion);
        ArgumentNullException.ThrowIfNull(bars);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(bars.Length, 64);
        if (bars.Length == 0) return;

        var first = bars[0];
        var ends = new HashSet<DateTimeOffset>();
        foreach (var bar in bars)
            if (!bar.IsComplete || !bar.IsValid || bar.ContractId != first.ContractId ||
                bar.TimeFrame != first.TimeFrame || bar.ValueDate != first.ValueDate ||
                bar.IntervalStartUtc >= bar.IntervalEndUtc || !ends.Add(bar.IntervalEndUtc))
                throw new ArgumentException("Seed coverage must contain unique qualified intervals of the same contract, timeframe and ValueDate.", nameof(bars));
    }
}
