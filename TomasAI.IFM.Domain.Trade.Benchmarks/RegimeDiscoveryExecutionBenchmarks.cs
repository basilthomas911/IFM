using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.RegimeDiscovery;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Model;

namespace TomasAI.IFM.Domain.Trade.Benchmarks;

/// <summary>Measures deterministic Regime Discovery scheduling for the supported workflow horizons.</summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net10_0, warmupCount: 3, iterationCount: 8)]
public class RegimeDiscoveryExecutionBenchmarks
{
    readonly RegimeDiscoveryCalculationModel calculation = new();
    RegimeDiscoveryCalculationInput[] inputs = [];

    /// <summary>Gets or sets the workflow target horizon.</summary>
    [Params(TimeFrameType.Daily, TimeFrameType.Weekly, TimeFrameType.Monthly)]
    public TimeFrameType Horizon { get; set; }

    /// <summary>Gets or sets the number of concurrently executing workflows.</summary>
    [Params(1, 3)]
    public int ConcurrentWorkflows { get; set; }

    /// <summary>Creates immutable benchmark inputs outside the measured operation.</summary>
    [GlobalSetup]
    public void Setup()
        => inputs = Enumerable.Range(0, ConcurrentWorkflows)
            .Select(index => CreateInput(Horizon, index))
            .ToArray();

    /// <summary>Runs all specialist calculations sequentially within each workflow.</summary>
    [Benchmark(Baseline = true)]
    public Task<RegimeDiscoveryResult[]> Sequential()
        => ExecuteAsync(RegimeDiscoveryExecutionMode.Sequential);

    /// <summary>Runs the three specialist calculations on ordinary thread-pool tasks.</summary>
    [Benchmark]
    public Task<RegimeDiscoveryResult[]> ThreadPoolParallel()
        => ExecuteAsync(RegimeDiscoveryExecutionMode.ThreadPoolParallel);

    Task<RegimeDiscoveryResult[]> ExecuteAsync(RegimeDiscoveryExecutionMode mode)
        => inputs.Length == 1
            ? SingleAsync(mode)
            : Task.WhenAll(inputs.Select(input => Task.Run(
                () => calculation.CalculateAsync(input, mode))));

    async Task<RegimeDiscoveryResult[]> SingleAsync(RegimeDiscoveryExecutionMode mode)
        => [await calculation.CalculateAsync(inputs[0], mode).ConfigureAwait(false)];

    static RegimeDiscoveryCalculationInput CreateInput(TimeFrameType horizon, int index)
    {
        var parameterSet = RegimeDiscoveryParameterSet.CreateDefault(
            Guid.CreateVersion7(), Guid.CreateVersion7(), horizon);
        var observations = new List<RegimeDiscoverySignalObservation>();
        foreach (var frame in parameterSet.Horizon.TimeFrames)
        {
            AddFrame(observations, frame.TimeFrame);
        }
        Add(observations, TimeFrameType.Daily, RegimeDiscoverySignalMetric.VixLevel, 18m);
        Add(observations, TimeFrameType.Daily, RegimeDiscoverySignalMetric.AtrBaselineRatio, 1m);
        Add(observations, TimeFrameType.Daily, RegimeDiscoverySignalMetric.VxFrontSecondRatio, 0.95m);
        Add(observations, TimeFrameType.Daily, RegimeDiscoverySignalMetric.PriorVolatilityComposite, 0.35m);
        Add(observations, TimeFrameType.Daily, RegimeDiscoverySignalMetric.BollingerWidthRatio, 1m);
        Add(observations, TimeFrameType.Daily, RegimeDiscoverySignalMetric.BollingerPosition, 0.5m);
        Add(observations, TimeFrameType.Daily, RegimeDiscoverySignalMetric.Ema20Interaction, 1m);
        Add(observations, TimeFrameType.Daily, RegimeDiscoverySignalMetric.AtrNormalizedRange, 1m);
        Add(observations, TimeFrameType.Daily, RegimeDiscoverySignalMetric.RollingHigh20, 104m);
        Add(observations, TimeFrameType.Daily, RegimeDiscoverySignalMetric.RollingLow20, 96m);
        Add(observations, TimeFrameType.Daily, RegimeDiscoverySignalMetric.BreakoutDistanceAtr, 0.6m);
        Add(observations, TimeFrameType.Daily, RegimeDiscoverySignalMetric.ItiDirection, 1m);
        Add(observations, TimeFrameType.Daily, RegimeDiscoverySignalMetric.ItiBandLevel, 1.2m);
        Add(observations, TimeFrameType.Daily, RegimeDiscoverySignalMetric.ItiReversalLevel, 0.1m);
        var now = new DateTime(2026, 8, 26, 16, 0, 0, DateTimeKind.Utc);
        var entityId = IntrinsicTimeStrategyWorkflowEntityId.Create(
            new FuturesItiSignalEntityId($"ES-202609-{index}", new DateOnly(2026, 8, 26), horizon));
        return new RegimeDiscoveryCalculationInput
        {
            ResultId = Guid.CreateVersion7(),
            WorkflowId = new StrategyWorkflowId(Guid.CreateVersion7()),
            EntityId = entityId,
            TriggerEventId = Guid.CreateVersion7(),
            ParameterSet = parameterSet,
            Snapshot = new RegimeDiscoveryMarketSignalSnapshot
            {
                SnapshotId = Guid.CreateVersion7(),
                CacheRevision = 10,
                MarketSeriesIdentity = MarketSeriesIdentity.ForContract("ES-202609"),
                TargetHorizon = horizon,
                CapturedAtUtc = now,
                MarketDataAsOfUtc = now.AddSeconds(-1),
                Observations = observations.ToArray()
            },
            ProducedAtUtc = now
        };
    }

    static void AddFrame(ICollection<RegimeDiscoverySignalObservation> observations, TimeFrameType frame)
    {
        Add(observations, frame, RegimeDiscoverySignalMetric.CurrentPrice, 105m);
        Add(observations, frame, RegimeDiscoverySignalMetric.Ema20, 103m);
        Add(observations, frame, RegimeDiscoverySignalMetric.Ema50, 101m);
        Add(observations, frame, RegimeDiscoverySignalMetric.Ema200, 99m);
        Add(observations, frame, RegimeDiscoverySignalMetric.Ema20Slope, 0.08m);
        Add(observations, frame, RegimeDiscoverySignalMetric.Ema50Slope, 0.06m);
        Add(observations, frame, RegimeDiscoverySignalMetric.Ema200Slope, 0.04m);
        Add(observations, frame, RegimeDiscoverySignalMetric.Rsi14, 65m);
        Add(observations, frame, RegimeDiscoverySignalMetric.Rsi14Slope, 2m);
        Add(observations, frame, RegimeDiscoverySignalMetric.Adx14, 30m);
        Add(observations, frame, RegimeDiscoverySignalMetric.PlusDi14, 30m);
        Add(observations, frame, RegimeDiscoverySignalMetric.MinusDi14, 15m);
        Add(observations, frame, RegimeDiscoverySignalMetric.MacdHistogram, 0.5m);
        Add(observations, frame, RegimeDiscoverySignalMetric.Atr14, 2m);
    }

    static void Add(
        ICollection<RegimeDiscoverySignalObservation> observations,
        TimeFrameType frame,
        RegimeDiscoverySignalMetric metric,
        decimal value) => observations.Add(new RegimeDiscoverySignalObservation
        {
            Metric = metric,
            SignalKey = new MarketAnalyticsSignalKey(
                MarketSeriesIdentity.ForContract("ES-202609"), Kind(metric), frame, $"{metric}.v1"),
            Value = value,
            MarketDataAsOfUtc = new DateTime(2026, 8, 26, 15, 59, 59, DateTimeKind.Utc),
            CalculatedAtUtc = new DateTime(2026, 8, 26, 15, 59, 59, DateTimeKind.Utc),
            SourceSequence = observations.Count + 1,
            SchemaVersion = 1,
            CalculationVersion = "1",
            IsWarm = true,
            IsValid = true,
            Availability = RegimeDiscoverySignalAvailability.Available,
            FreshnessFactor = 0.95m,
            SignalIdentity = $"ES-202609.{metric}.{frame}"
        });

    static MarketAnalyticsSignalKind Kind(RegimeDiscoverySignalMetric metric) => metric switch
    {
        RegimeDiscoverySignalMetric.Ema20 or RegimeDiscoverySignalMetric.Ema50 or
            RegimeDiscoverySignalMetric.Ema200 or RegimeDiscoverySignalMetric.Ema20Slope or
            RegimeDiscoverySignalMetric.Ema50Slope or RegimeDiscoverySignalMetric.Ema200Slope or
            RegimeDiscoverySignalMetric.Ema20Interaction => MarketAnalyticsSignalKind.Ema,
        RegimeDiscoverySignalMetric.Rsi14 or RegimeDiscoverySignalMetric.Rsi14Slope => MarketAnalyticsSignalKind.Rsi,
        RegimeDiscoverySignalMetric.Adx14 or RegimeDiscoverySignalMetric.PlusDi14 or
            RegimeDiscoverySignalMetric.MinusDi14 => MarketAnalyticsSignalKind.Adx,
        RegimeDiscoverySignalMetric.MacdHistogram => MarketAnalyticsSignalKind.Macd,
        RegimeDiscoverySignalMetric.Atr14 or RegimeDiscoverySignalMetric.AtrBaselineRatio or
            RegimeDiscoverySignalMetric.AtrNormalizedRange => MarketAnalyticsSignalKind.Atr,
        RegimeDiscoverySignalMetric.BollingerWidth or RegimeDiscoverySignalMetric.BollingerWidthRatio or
            RegimeDiscoverySignalMetric.BollingerPosition => MarketAnalyticsSignalKind.BollingerBand,
        RegimeDiscoverySignalMetric.VxFrontSecondRatio or RegimeDiscoverySignalMetric.VixLevel =>
            MarketAnalyticsSignalKind.VxTermStructure,
        RegimeDiscoverySignalMetric.ItiDirection or RegimeDiscoverySignalMetric.ItiBandLevel or
            RegimeDiscoverySignalMetric.ItiReversalLevel or RegimeDiscoverySignalMetric.CurrentPrice =>
            MarketAnalyticsSignalKind.Iti,
        _ => MarketAnalyticsSignalKind.MarketStructure
    };
}
