using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.RegimeDiscovery;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery;

/// <summary>Maps Regime Discovery EMA evidence to the initialized Daily baseline.</summary>
public static class RegimeDiscoveryEmaBaseline
{
    /// <summary>Maximum age of the completed Daily baseline, including exchange closures.</summary>
    public const int MaximumAgeSeconds = 96 * 60 * 60;

    /// <summary>Identifies EMA levels and their corresponding Daily slopes.</summary>
    public static bool IsBaseline(RegimeDiscoverySignalMetric metric) => metric is
        RegimeDiscoverySignalMetric.Ema20 or RegimeDiscoverySignalMetric.Ema50 or RegimeDiscoverySignalMetric.Ema200 or
        RegimeDiscoverySignalMetric.Ema20Slope or RegimeDiscoverySignalMetric.Ema50Slope or RegimeDiscoverySignalMetric.Ema200Slope;

    /// <summary>Reads existing per-interval configuration through the Daily baseline convention without rewriting its history.</summary>
    public static RegimeDiscoverySignalConfiguration[] Normalize(IEnumerable<RegimeDiscoverySignalConfiguration> rows) => rows
        .Select(row => IsBaseline(row.Metric) && row.TimeFrame != TimeFrameType.Daily
            ? row with { TimeFrame = TimeFrameType.Daily, MaximumAgeSeconds = MaximumAgeSeconds }
            : row)
        .GroupBy(row => (row.Metric, row.TimeFrame))
        .Select(group => group.First() with
        {
            Enabled = group.Any(row => row.Enabled), IsRequired = group.Any(row => row.IsRequired),
            MaximumAgeSeconds = group.Max(row => row.MaximumAgeSeconds)
        }).ToArray();
}
