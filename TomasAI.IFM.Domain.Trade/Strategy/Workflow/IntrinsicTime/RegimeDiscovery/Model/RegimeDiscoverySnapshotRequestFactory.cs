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

    static readonly RegimeDiscoverySignalMetric[] TargetEvidenceRequiredMetrics =
    [
        RegimeDiscoverySignalMetric.Atr14,
        RegimeDiscoverySignalMetric.AtrBaselineRatio,
        RegimeDiscoverySignalMetric.BollingerWidthRatio,
        RegimeDiscoverySignalMetric.BollingerPosition,
        RegimeDiscoverySignalMetric.Ema20Interaction,
        RegimeDiscoverySignalMetric.AtrNormalizedRange,
        RegimeDiscoverySignalMetric.RollingHigh20,
        RegimeDiscoverySignalMetric.RollingLow20,
        RegimeDiscoverySignalMetric.BreakoutDistanceAtr
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
        if (parameterSet.SchemaVersion is not (1 or 2 or 3 or 4 or 5))
            throw new ArgumentException("Unsupported Regime Discovery parameter schema.");
        if (parameterSet.SchemaVersion >= 3 &&
            (parameterSet.Horizon.TimeFrames.Any(frame => frame is null || !frame.IsRequired) ||
             parameterSet.SignalRequirements is null ||
             parameterSet.SignalRequirements.Any(row => row is null || !row.IsRequired)))
            throw new ArgumentException("Schemas 3 through 5 require every included signal and every configured interval.");
        var targetEvidenceTimeFrame = TargetEvidenceTimeFrame(parameterSet);
        var targetEvidenceMaximumAge = parameterSet.Horizon.TimeFrames
            .Single(frame => frame.TimeFrame == targetEvidenceTimeFrame).MaximumAgeSeconds;
        var requirements = new List<RegimeDiscoverySignalRequirement>();
        foreach (var frame in parameterSet.Horizon.TimeFrames)
            requirements.AddRange(TrendMetrics.Select(metric => Requirement(
                metric, frame.TimeFrame, frame.IsRequired, frame.MaximumAgeSeconds, frame.Weight)));
        requirements.AddRange(TargetEvidenceRequiredMetrics.Select(metric => Requirement(
            metric, targetEvidenceTimeFrame, true, targetEvidenceMaximumAge, 1m)));
        requirements.Add(Requirement(RegimeDiscoverySignalMetric.VxFrontSecondRatio,
            TimeFrameType.Daily, true, TargetMaximumAge(TimeFrameType.Daily),
            parameterSet.Volatility.TermStructureWeight));
        requirements.Add(Requirement(RegimeDiscoverySignalMetric.VixLevel,
            TimeFrameType.Daily, false, TargetMaximumAge(TimeFrameType.Daily),
            parameterSet.Volatility.VixWeight));
        requirements.Add(Requirement(RegimeDiscoverySignalMetric.BollingerWidth,
            targetEvidenceTimeFrame, false, targetEvidenceMaximumAge, 0m));
        foreach (var frame in parameterSet.Horizon.TimeFrames)
            requirements.Add(Requirement(RegimeDiscoverySignalMetric.Tdi,
                frame.TimeFrame, false, frame.MaximumAgeSeconds, frame.Weight));
        requirements.Add(Requirement(RegimeDiscoverySignalMetric.RealizedVolatilityPercentile,
            targetEvidenceTimeFrame, false, targetEvidenceMaximumAge,
            parameterSet.Volatility.RealizedVolatilityWeight));
        requirements.Add(Requirement(RegimeDiscoverySignalMetric.PriorVolatilityComposite,
            targetEvidenceTimeFrame, false, targetEvidenceMaximumAge, 1m));
        if (parameterSet.SchemaVersion >= 2)
        {
            var configured = parameterSet.SignalRequirements
                ?? throw new ArgumentException("Explicit signal requirements are required for schemas 2 through 5.");
            if (configured.Length is 0 or > 512 || configured.Any(row => row is null) ||
                configured.Select(row => row.RequirementId).Distinct().Count() != configured.Length ||
                configured.Any(row => row.RequirementId == Guid.Empty) ||
                configured.Select(row => (row.Metric, row.TimeFrame)).Distinct().Count() != configured.Length)
                throw new ArgumentException("Signal rows must have unique nonempty identities and metric/timeframe pairs.");
            foreach (var mandatory in requirements.Where(row => row.IsRequired))
                if (!configured.Any(row => row.Enabled && row.IsRequired && row.Metric == mandatory.Metric &&
                    row.TimeFrame == mandatory.TimeFrame))
                    throw new ArgumentException($"Required calculation dependency {mandatory.Metric}/{mandatory.TimeFrame} cannot be removed.");
            var selected = new List<RegimeDiscoverySignalRequirement>();
            foreach (var row in configured)
            {
                var original = requirements.FirstOrDefault(value => value.Metric == row.Metric && value.TimeFrame == row.TimeFrame);
                if (original is null || row.MaximumAgeSeconds <= 0 ||
                    row.CalculationConfigurationId != original.CalculationConfigurationId)
                    throw new ArgumentException($"Unsupported signal configuration {row.Metric}/{row.TimeFrame}.");
                if (row.Enabled) selected.Add(original with
                {
                    IsRequired = row.IsRequired,
                    MaximumAgeSeconds = row.MaximumAgeSeconds
                });
            }
            requirements = selected;
        }
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

    /// <summary>Selects the longest required producer-backed frame used for target market evidence.</summary>
    public static TimeFrameType TargetEvidenceTimeFrame(RegimeDiscoveryParameterSet parameterSet)
    {
        ArgumentNullException.ThrowIfNull(parameterSet);
        var frames = parameterSet.Horizon.TimeFrames
            .Where(frame => frame.IsRequired)
            .Select(frame => frame.TimeFrame)
            .ToArray();
        if (frames.Length == 0)
            throw new ArgumentException("Regime Discovery requires at least one required evidence timeframe.",
                nameof(parameterSet));
        return frames.OrderBy(Duration).Last();
    }

    static TimeSpan Duration(TimeFrameType timeFrame) => timeFrame switch
    {
        TimeFrameType.FifteenSeconds => TimeSpan.FromSeconds(15),
        TimeFrameType.OneMinute => TimeSpan.FromMinutes(1),
        TimeFrameType.FiveMinutes => TimeSpan.FromMinutes(5),
        TimeFrameType.FifteenMinutes => TimeSpan.FromMinutes(15),
        TimeFrameType.ThirtyMinutes => TimeSpan.FromMinutes(30),
        TimeFrameType.OneHour => TimeSpan.FromHours(1),
        TimeFrameType.FourHours => TimeSpan.FromHours(4),
        TimeFrameType.Daily => TimeSpan.FromDays(1),
        _ => throw new ArgumentOutOfRangeException(nameof(timeFrame), timeFrame,
            "The configured Regime Discovery evidence timeframe is unsupported.")
    };

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
