using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.RegimeDiscovery;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Model;

/// <summary>Builds exact, deterministic snapshot requirements from one frozen parameter set.</summary>
public static class RegimeDiscoverySnapshotRequestFactory
{
    static readonly RegimeDiscoverySignalMetric[] TrendMetrics =
    [
        RegimeDiscoverySignalMetric.CurrentPrice,
        RegimeDiscoverySignalMetric.Ema20,
        RegimeDiscoverySignalMetric.Ema50,
        RegimeDiscoverySignalMetric.Ema200,
        RegimeDiscoverySignalMetric.Ema20Slope,
        RegimeDiscoverySignalMetric.Ema50Slope,
        RegimeDiscoverySignalMetric.Ema200Slope,
        RegimeDiscoverySignalMetric.Rsi14,
        RegimeDiscoverySignalMetric.Rsi14Slope,
        RegimeDiscoverySignalMetric.Adx14,
        RegimeDiscoverySignalMetric.PlusDi14,
        RegimeDiscoverySignalMetric.MinusDi14,
        RegimeDiscoverySignalMetric.MacdHistogram,
        RegimeDiscoverySignalMetric.Atr14
    ];

    static readonly RegimeDiscoverySignalMetric[] TargetRequiredMetrics =
    [
        RegimeDiscoverySignalMetric.CurrentPrice,
        RegimeDiscoverySignalMetric.AtrBaselineRatio,
        RegimeDiscoverySignalMetric.VixLevel,
        RegimeDiscoverySignalMetric.VxFrontSecondRatio,
        RegimeDiscoverySignalMetric.BollingerWidthRatio,
        RegimeDiscoverySignalMetric.BollingerPosition,
        RegimeDiscoverySignalMetric.Ema20Interaction,
        RegimeDiscoverySignalMetric.AtrNormalizedRange,
        RegimeDiscoverySignalMetric.RollingHigh20,
        RegimeDiscoverySignalMetric.RollingLow20,
        RegimeDiscoverySignalMetric.BreakoutDistanceAtr,
        RegimeDiscoverySignalMetric.ItiDirection,
        RegimeDiscoverySignalMetric.ItiBandLevel,
        RegimeDiscoverySignalMetric.ItiReversalLevel
    ];

    /// <summary>Creates an exact snapshot request for one market series and frozen parameter set.</summary>
    /// <param name="marketSeriesIdentity">Provider-neutral market series.</param>
    /// <param name="parameterSet">Frozen Regime Discovery parameters.</param>
    /// <returns>The complete immutable request.</returns>
    public static RegimeDiscoveryMarketSignalSnapshotRequest Create(
        MarketSeriesIdentity marketSeriesIdentity,
        RegimeDiscoveryParameterSet parameterSet)
    {
        ArgumentNullException.ThrowIfNull(parameterSet);
        var requirements = new List<RegimeDiscoverySignalRequirement>();
        foreach (var frame in parameterSet.Horizon.TimeFrames)
            requirements.AddRange(TrendMetrics.Select(metric => Requirement(
                metric, frame.TimeFrame, frame.IsRequired, frame.MaximumAgeSeconds, frame.Weight)));
        requirements.AddRange(TargetRequiredMetrics.Select(metric => Requirement(
            metric, parameterSet.TargetHorizon, true, TargetMaximumAge(parameterSet.TargetHorizon), 1m)));
        requirements.Add(Requirement(RegimeDiscoverySignalMetric.RealizedVolatilityPercentile,
            parameterSet.TargetHorizon, false, TargetMaximumAge(parameterSet.TargetHorizon),
            parameterSet.Volatility.RealizedVolatilityWeight));
        requirements.Add(Requirement(RegimeDiscoverySignalMetric.PriorVolatilityComposite,
            parameterSet.TargetHorizon, false, TargetMaximumAge(parameterSet.TargetHorizon), 1m));
        return new RegimeDiscoveryMarketSignalSnapshotRequest
        {
            MarketSeriesIdentity = marketSeriesIdentity,
            TargetHorizon = parameterSet.TargetHorizon,
            Requirements = requirements
                .GroupBy(value => (value.Metric, value.TimeFrame))
                .Select(group => group.OrderByDescending(value => value.IsRequired).First())
                .OrderBy(value => value.TimeFrame)
                .ThenBy(value => value.Metric)
                .ToArray(),
            FutureClockSkewSeconds = parameterSet.Freshness.FutureClockSkewSeconds,
            SupportedSchemaVersions = parameterSet.DataQuality.SupportedSignalSchemaVersions.ToArray(),
            ApprovedCalculationVersions = parameterSet.DataQuality.ApprovedCalculationVersions.ToArray(),
            CaptureAttempts = parameterSet.DataQuality.SnapshotCaptureAttempts
        };
    }

    static RegimeDiscoverySignalRequirement Requirement(
        RegimeDiscoverySignalMetric metric,
        TimeFrameType timeFrame,
        bool required,
        int maximumAgeSeconds,
        decimal weight) => new()
        {
            Metric = metric,
            TimeFrame = timeFrame,
            IsRequired = required,
            CalculationConfigurationId = $"{metric}.v1",
            MaximumAgeSeconds = maximumAgeSeconds,
            Weight = weight
        };

    static int TargetMaximumAge(TimeFrameType horizon) => horizon switch
    {
        TimeFrameType.Daily => 96 * 60 * 60,
        TimeFrameType.Weekly => 7 * 24 * 60 * 60,
        TimeFrameType.Monthly => 31 * 24 * 60 * 60,
        _ => throw new ArgumentOutOfRangeException(nameof(horizon), horizon, null)
    };
}
