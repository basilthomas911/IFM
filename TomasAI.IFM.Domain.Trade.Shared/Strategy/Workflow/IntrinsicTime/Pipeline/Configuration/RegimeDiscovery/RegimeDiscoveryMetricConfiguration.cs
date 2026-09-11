using System.Security.Cryptography;
using System.Text;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.RegimeDiscovery;
using TomasAI.IFM.Domain.MarketData.Shared;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery;

/// <summary>Marks a payload property as part of the schema from the specified version onward.</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class ParameterSchemaSinceAttribute(int version) : Attribute
{
    public int Version { get; } = version;
}
/// <summary>Configures one timeframe-and-period market-data signal.</summary>
[MessagePackObject]
public sealed record RegimeDiscoverySignalMetricConfiguration
{
    [Key(0)] public Guid RequirementId { get; init; }
    [Key(1)] public SignalMetricsType Metric { get; init; }
    [Key(2)] public TimeFrameType TimeFrame { get; init; }
    [Key(3)] public int PeriodLength { get; init; }
    [Key(4)] public bool Enabled { get; init; } = true;
    [Key(5)] public int MaximumAgeSeconds { get; init; }
    [Key(6)] public bool PrepareAtStartup { get; init; } = true;
    [Key(7)] public bool Monitor { get; init; } = true;
}

/// <summary>Configures one current market, contract, session, or intrinsic-time observation.</summary>
[MessagePackObject]
public sealed record RegimeDiscoveryObservationMetricConfiguration
{
    [Key(0)] public Guid RequirementId { get; init; }
    [Key(1)] public ObservationMetricsType Metric { get; init; }
    [Key(2)] public bool Enabled { get; init; } = true;
    [Key(3)] public int MaximumAgeSeconds { get; init; }
    [Key(4)] public bool PrepareAtStartup { get; init; } = true;
    [Key(5)] public bool Monitor { get; init; } = true;
}
/// <summary>Projects normalized editor selections into the current Regime Discovery evaluator contract.</summary>
public static class RegimeDiscoveryMetricConfigurationProjection
{
    public static RegimeDiscoverySignalMetricConfiguration[] FromLegacySignals(
        Guid parameterSetId, IEnumerable<RegimeDiscoverySignalConfiguration> legacyRows)
    {
        return legacyRows.Select(row => (Row: row, Definition: Definition(row.Metric)))
            .Where(value => value.Definition.HasValue)
            .GroupBy(value => (value.Definition!.Value.Metric, value.Row.TimeFrame,
                value.Definition.Value.PeriodLength))
            .Select(group =>
            {
                var source = group.Select(value => value.Row).ToArray();
                var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(
                    $"{parameterSetId:N}:signal:{group.Key.Metric}:{group.Key.TimeFrame}:{group.Key.PeriodLength}"));
                return new RegimeDiscoverySignalMetricConfiguration
                {
                    RequirementId = new Guid(bytes.AsSpan(0, 16)),
                    Metric = group.Key.Metric,
                    TimeFrame = group.Key.TimeFrame,
                    PeriodLength = group.Key.PeriodLength,
                    Enabled = source.Any(row => row.Enabled),
                    MaximumAgeSeconds = source.Max(row => row.MaximumAgeSeconds),
                    PrepareAtStartup = source.Any(row => row.PrepareAtStartup),
                    Monitor = source.Any(row => row.Monitor)
                };
            })
            .OrderBy(row => row.TimeFrame).ThenBy(row => row.Metric).ThenBy(row => row.PeriodLength)
            .ToArray();
    }

    public static RegimeDiscoveryObservationMetricConfiguration[] FromLegacyObservations(
        Guid parameterSetId, IEnumerable<RegimeDiscoverySignalConfiguration> legacyRows)
    {
        var vx = legacyRows.Where(row => row.Metric is RegimeDiscoverySignalMetric.VxFrontSecondRatio
            or RegimeDiscoverySignalMetric.VxFrontLevel).ToArray();
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(
            $"{parameterSetId:N}:observation:{ObservationMetricsType.VxTermStructure}"));
        return
        [
            new()
            {
                RequirementId = new Guid(bytes.AsSpan(0, 16)),
                Metric = ObservationMetricsType.VxTermStructure,
                Enabled = vx.Any(row => row.Enabled),
                MaximumAgeSeconds = vx.Length == 0 ? 345600 : vx.Max(row => row.MaximumAgeSeconds),
                PrepareAtStartup = vx.Length == 0 || vx.Any(row => row.PrepareAtStartup),
                Monitor = vx.Length == 0 || vx.Any(row => row.Monitor)
            }
        ];
    }
    public static RegimeDiscoverySignalConfiguration[] ToLegacyRequirements(
        Guid parameterSetId,
        IEnumerable<RegimeDiscoverySignalMetricConfiguration> signals,
        IEnumerable<RegimeDiscoveryObservationMetricConfiguration> observations,
        RegimeDiscoveryHorizonConfiguration horizon)
    {
        ArgumentNullException.ThrowIfNull(horizon);
        var targetEvidenceTimeFrame = TargetEvidenceTimeFrame(horizon);
        var rows = signals.SelectMany(row => Metrics(row, targetEvidenceTimeFrame).Select(metric => Create(parameterSetId, metric,
            row.TimeFrame, row.Enabled, row.MaximumAgeSeconds, row.PrepareAtStartup, row.Monitor))).ToList();

        foreach (var observation in observations)
        {
            if (observation.Metric == ObservationMetricsType.VxTermStructure)
                rows.Add(Create(parameterSetId, RegimeDiscoverySignalMetric.VxFrontSecondRatio,
                    TimeFrameType.Daily, observation.Enabled, observation.MaximumAgeSeconds,
                    observation.PrepareAtStartup, observation.Monitor));
            else if (observation.Enabled)
                throw new ArgumentException($"{observation.Metric} is not yet consumed by Regime Discovery.");
        }

        return rows.GroupBy(row => (row.Metric, row.TimeFrame)).Select(group => group.First())
            .OrderBy(row => row.TimeFrame).ThenBy(row => row.Metric).ToArray();
    }

    static RegimeDiscoverySignalConfiguration Create(Guid setId, RegimeDiscoverySignalMetric metric,
        TimeFrameType timeFrame, bool enabled, int maximumAgeSeconds, bool prepare, bool monitor)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{setId:N}:{metric}:{timeFrame}"));
        return new()
        {
            RequirementId = new Guid(bytes.AsSpan(0, 16)),
            Metric = metric,
            TimeFrame = timeFrame,
            Enabled = enabled,
            IsRequired = true,
            MaximumAgeSeconds = maximumAgeSeconds,
            CalculationConfigurationId = $"{metric}.v1",
            PrepareAtStartup = prepare,
            Monitor = monitor
        };
    }

    static (SignalMetricsType Metric, int PeriodLength)? Definition(RegimeDiscoverySignalMetric metric) => metric switch
    {
        RegimeDiscoverySignalMetric.Ema20 or RegimeDiscoverySignalMetric.Ema20Slope => (SignalMetricsType.Ema, 20),
        RegimeDiscoverySignalMetric.Ema50 or RegimeDiscoverySignalMetric.Ema50Slope => (SignalMetricsType.Ema, 50),
        RegimeDiscoverySignalMetric.Ema200 or RegimeDiscoverySignalMetric.Ema200Slope => (SignalMetricsType.Ema, 200),
        RegimeDiscoverySignalMetric.Rsi14 or RegimeDiscoverySignalMetric.Rsi14Slope => (SignalMetricsType.Rsi, 14),
        RegimeDiscoverySignalMetric.Adx14 or RegimeDiscoverySignalMetric.PlusDi14
            or RegimeDiscoverySignalMetric.MinusDi14 => (SignalMetricsType.Adx, 14),
        RegimeDiscoverySignalMetric.MacdHistogram => (SignalMetricsType.Macd, 26),
        RegimeDiscoverySignalMetric.Atr14 or RegimeDiscoverySignalMetric.AtrBaselineRatio => (SignalMetricsType.Atr, 14),
        RegimeDiscoverySignalMetric.BollingerWidth or RegimeDiscoverySignalMetric.BollingerWidthRatio
            or RegimeDiscoverySignalMetric.BollingerPosition => (SignalMetricsType.BollingerBand, 20),
        RegimeDiscoverySignalMetric.Ema20Interaction or RegimeDiscoverySignalMetric.AtrNormalizedRange
            or RegimeDiscoverySignalMetric.RollingHigh20 or RegimeDiscoverySignalMetric.RollingLow20
            or RegimeDiscoverySignalMetric.BreakoutDistanceAtr => (SignalMetricsType.MarketStructure, 20),
        RegimeDiscoverySignalMetric.Tdi => (SignalMetricsType.Tdi, 13),
        _ => null
    };
    static RegimeDiscoverySignalMetric[] Metrics(
        RegimeDiscoverySignalMetricConfiguration row, TimeFrameType targetEvidenceTimeFrame) =>
        (row.Metric, row.PeriodLength) switch
        {
            (SignalMetricsType.Ema, 20) => [RegimeDiscoverySignalMetric.Ema20, RegimeDiscoverySignalMetric.Ema20Slope],
            (SignalMetricsType.Ema, 50) => [RegimeDiscoverySignalMetric.Ema50, RegimeDiscoverySignalMetric.Ema50Slope],
            (SignalMetricsType.Ema, 200) => [RegimeDiscoverySignalMetric.Ema200, RegimeDiscoverySignalMetric.Ema200Slope],
            (SignalMetricsType.Rsi, 14) => [RegimeDiscoverySignalMetric.Rsi14, RegimeDiscoverySignalMetric.Rsi14Slope],
            (SignalMetricsType.Adx, 14) => [RegimeDiscoverySignalMetric.Adx14, RegimeDiscoverySignalMetric.PlusDi14,
                RegimeDiscoverySignalMetric.MinusDi14],
            (SignalMetricsType.Macd, 26) => [RegimeDiscoverySignalMetric.MacdHistogram],
            (SignalMetricsType.Atr, 14) when row.TimeFrame == targetEvidenceTimeFrame
                => [RegimeDiscoverySignalMetric.Atr14, RegimeDiscoverySignalMetric.AtrBaselineRatio],
            (SignalMetricsType.Atr, 14) => [RegimeDiscoverySignalMetric.Atr14],
            (SignalMetricsType.BollingerBand, 20) => [RegimeDiscoverySignalMetric.BollingerWidth,
                RegimeDiscoverySignalMetric.BollingerWidthRatio, RegimeDiscoverySignalMetric.BollingerPosition],
            (SignalMetricsType.MarketStructure, 20) => [RegimeDiscoverySignalMetric.Ema20Interaction,
                RegimeDiscoverySignalMetric.AtrNormalizedRange, RegimeDiscoverySignalMetric.RollingHigh20,
                RegimeDiscoverySignalMetric.RollingLow20, RegimeDiscoverySignalMetric.BreakoutDistanceAtr],
            (SignalMetricsType.Tdi, 13) => [RegimeDiscoverySignalMetric.Tdi],
            _ => throw new ArgumentException(
                $"{row.Metric} period {row.PeriodLength} is not supported by the current Regime Discovery evaluator.")
        };

    static TimeFrameType TargetEvidenceTimeFrame(RegimeDiscoveryHorizonConfiguration horizon)
    {
        var required = horizon.TimeFrames.Where(frame => frame.IsRequired).Select(frame => frame.TimeFrame).ToArray();
        if (required.Length == 0)
            throw new ArgumentException("At least one required observation frame is needed.", nameof(horizon));
        return required.OrderBy(Duration).Last();
    }

    static int Duration(TimeFrameType frame) => frame switch
    {
        TimeFrameType.FifteenSeconds => 15,
        TimeFrameType.OneMinute => 60,
        TimeFrameType.FiveMinutes => 300,
        TimeFrameType.FifteenMinutes => 900,
        TimeFrameType.ThirtyMinutes => 1800,
        TimeFrameType.OneHour => 3600,
        TimeFrameType.FourHours => 14400,
        TimeFrameType.Daily => 86400,
        _ => throw new ArgumentException("Unsupported evidence timeframe.", nameof(frame))
    };
}



