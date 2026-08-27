using FluentAssertions;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.RegimeDiscovery;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Model;

namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.RegimeDiscovery;

/// <summary>Qualifies deterministic RD-11 and RD-12 specialist and Fusion calculations.</summary>
public sealed class RegimeDiscoveryCalculationModelTests
{
    /// <summary>Confirms the approved Trend formula produces the expected golden score.</summary>
    [Fact]
    public void Trend_golden_vector_is_deterministic()
    {
        var result = new TrendRegimeCalculationModel().Calculate(CreateInput());

        result.IsComplete.Should().BeTrue();
        result.Score.Should().Be(0.796750m);
        result.Direction.Should().Be(RegimeDirection.Up);
        result.Strength.Should().Be(TrendRegimeStrength.Strong);
        result.Phase.Should().Be(TrendRegimePhase.Established);
        result.Confidence.Should().Be(0.982500m);
    }

    /// <summary>Confirms required missing evidence produces an incomplete specialist result.</summary>
    [Fact]
    public void Missing_required_trend_metric_fails_the_specialist()
    {
        var input = CreateInput();
        input = input with
        {
            Snapshot = input.Snapshot with
            {
                Observations = input.Snapshot.Observations
                    .Where(observation => !(observation.Metric == RegimeDiscoverySignalMetric.Ema200 &&
                                            observation.SignalKey.TimeFrame == TimeFrameType.FifteenMinutes))
                    .ToArray()
            }
        };

        var result = new TrendRegimeCalculationModel().Calculate(input);

        result.IsComplete.Should().BeFalse();
        result.Reasons.Should().Contain(reason =>
            reason.Code == RegimeDiscoveryReasonCodes.RequiredDataMissing &&
            reason.Severity == RegimeReasonSeverity.Failure);
    }

    /// <summary>Confirms severe volatility produces explicit no-new-trade evidence.</summary>
    [Fact]
    public void Extreme_volatility_restricts_new_trades()
    {
        var input = Replace(CreateInput(), RegimeDiscoverySignalMetric.VixLevel, TimeFrameType.Daily, 35m);
        input = Replace(input, RegimeDiscoverySignalMetric.VxFrontSecondRatio, TimeFrameType.Daily, 1.08m);

        var result = new VolatilityRegimeCalculationModel().Calculate(input);

        result.IsComplete.Should().BeTrue();
        result.NoNewTrade.Should().BeTrue();
        result.Reasons.Should().Contain(reason => reason.Code == RegimeDiscoveryReasonCodes.VolatilityExtreme);
    }

    /// <summary>Confirms breakout classification has precedence over expansion and trend classifications.</summary>
    [Fact]
    public void Market_structure_breakout_has_first_precedence()
    {
        var input = Replace(CreateInput(), RegimeDiscoverySignalMetric.BollingerWidthRatio,
            TimeFrameType.Daily, 1.40m);

        var result = new MarketStructureRegimeCalculationModel().Calculate(input);

        result.Classification.Should().Be(MarketStructureClassification.BreakingOut);
        result.Breakout.Should().Be(MarketBreakoutState.Up);
        result.Score.Should().Be(0.300000m);
    }

    /// <summary>Confirms the exact Fusion formula and deterministic restrictions.</summary>
    [Fact]
    public void Fusion_golden_vector_is_deterministic()
    {
        var result = new MarketRegimeFusionModel().Calculate(
            new TrendRegimeResult
            {
                IsComplete = true,
                Direction = RegimeDirection.Up,
                Score = 0.80m,
                Confidence = 0.80m
            },
            new VolatilityRegimeResult
            {
                IsComplete = true,
                Level = VolatilityRegimeLevel.Normal,
                Score = 0.50m,
                Confidence = 0.80m
            },
            new MarketStructureRegimeResult
            {
                IsComplete = true,
                Classification = MarketStructureClassification.Trending,
                Direction = RegimeDirection.Up,
                Score = 0.40m,
                Confidence = 0.80m
            },
            new MarketRegimeFusionConfiguration());

        result.DirectionalScore.Should().Be(0.660000m);
        result.RiskAdjustedConviction.Should().Be(0.495000m);
        result.Confidence.Should().Be(0.760000m);
        result.Direction.Should().Be(RegimeDirection.Up);
        result.Restrictions.Should().BeEmpty();
    }

    /// <summary>Confirms sequential and thread-pool-parallel coordination serialize identically.</summary>
    [Fact]
    public async Task Sequential_and_parallel_results_are_byte_equivalent()
    {
        var model = new RegimeDiscoveryCalculationModel();
        var input = CreateInput();

        var sequential = await model.CalculateAsync(input, RegimeDiscoveryExecutionMode.Sequential);
        var parallel = await model.CalculateAsync(input, RegimeDiscoveryExecutionMode.ThreadPoolParallel);

        MessagePackSerializer.Serialize(parallel).Should().Equal(MessagePackSerializer.Serialize(sequential));
        sequential.Fusion.IsComplete.Should().BeTrue();
        sequential.Fusion.DirectionalScore.Should().Be(0.622888m);
    }

    internal static RegimeDiscoveryCalculationInput CreateInput()
    {
        var parameterSet = RegimeDiscoveryParameterSet.CreateDefault(
            Guid.Parse("0198E212-3C00-7000-8000-000000000201"),
            Guid.Parse("0198E212-3C00-7000-8000-000000000202"),
            TimeFrameType.Daily);
        var observations = new List<RegimeDiscoverySignalObservation>();
        foreach (var frame in parameterSet.Horizon.TimeFrames)
        {
            Add(observations, frame.TimeFrame, RegimeDiscoverySignalMetric.CurrentPrice, 105m);
            Add(observations, frame.TimeFrame, RegimeDiscoverySignalMetric.Ema20, 103m);
            Add(observations, frame.TimeFrame, RegimeDiscoverySignalMetric.Ema50, 101m);
            Add(observations, frame.TimeFrame, RegimeDiscoverySignalMetric.Ema200, 99m);
            Add(observations, frame.TimeFrame, RegimeDiscoverySignalMetric.Ema20Slope, 0.08m);
            Add(observations, frame.TimeFrame, RegimeDiscoverySignalMetric.Ema50Slope, 0.06m);
            Add(observations, frame.TimeFrame, RegimeDiscoverySignalMetric.Ema200Slope, 0.04m);
            Add(observations, frame.TimeFrame, RegimeDiscoverySignalMetric.Rsi14, 65m);
            Add(observations, frame.TimeFrame, RegimeDiscoverySignalMetric.Rsi14Slope, 2m);
            Add(observations, frame.TimeFrame, RegimeDiscoverySignalMetric.Adx14, 30m);
            Add(observations, frame.TimeFrame, RegimeDiscoverySignalMetric.PlusDi14, 30m);
            Add(observations, frame.TimeFrame, RegimeDiscoverySignalMetric.MinusDi14, 15m);
            Add(observations, frame.TimeFrame, RegimeDiscoverySignalMetric.MacdHistogram, 0.5m);
            Add(observations, frame.TimeFrame, RegimeDiscoverySignalMetric.Atr14, 2m);
        }
        Add(observations, TimeFrameType.Daily, RegimeDiscoverySignalMetric.CurrentPrice, 105m);
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
        var entityId = IntrinsicTimeStrategyWorkflowEntityId.Create(
            new FuturesItiSignalEntityId("ES-202609", new DateOnly(2026, 8, 26), TimeFrameType.Daily));
        return new RegimeDiscoveryCalculationInput
        {
            ResultId = Guid.Parse("0198E212-3C00-7000-8000-000000000203"),
            WorkflowId = new StrategyWorkflowId(Guid.Parse("0198E212-3C00-7000-8000-000000000204")),
            EntityId = entityId,
            TriggerEventId = Guid.Parse("0198E212-3C00-7000-8000-000000000205"),
            ParameterSet = parameterSet,
            Snapshot = new RegimeDiscoveryMarketSignalSnapshot
            {
                SnapshotId = Guid.Parse("0198E212-3C00-7000-8000-000000000206"),
                CacheRevision = 10,
                MarketSeriesIdentity = MarketSeriesIdentity.ForContract("ES-202609"),
                TargetHorizon = TimeFrameType.Daily,
                CapturedAtUtc = Utc(16, 0),
                MarketDataAsOfUtc = Utc(15, 59),
                Observations = observations.ToArray()
            },
            ProducedAtUtc = Utc(16, 0)
        };
    }

    static RegimeDiscoveryCalculationInput Replace(
        RegimeDiscoveryCalculationInput input,
        RegimeDiscoverySignalMetric metric,
        TimeFrameType timeFrame,
        decimal value) => input with
    {
        Snapshot = input.Snapshot with
        {
            Observations = input.Snapshot.Observations.Select(observation =>
                observation.Metric == metric && observation.SignalKey.TimeFrame == timeFrame
                    ? observation with { Value = value }
                    : observation).ToArray()
        }
    };

    static void Add(
        ICollection<RegimeDiscoverySignalObservation> observations,
        TimeFrameType timeFrame,
        RegimeDiscoverySignalMetric metric,
        decimal value) => observations.Add(new RegimeDiscoverySignalObservation
        {
            Metric = metric,
            SignalKey = new MarketAnalyticsSignalKey(
                MarketSeriesIdentity.ForContract("ES-202609"), Kind(metric), timeFrame, $"{metric}.v1"),
            Value = value,
            MarketDataAsOfUtc = Utc(15, 59),
            CalculatedAtUtc = Utc(15, 59),
            SourceSequence = observations.Count + 1,
            SchemaVersion = 1,
            CalculationVersion = "1",
            IsWarm = true,
            IsValid = true,
            Availability = RegimeDiscoverySignalAvailability.Available,
            FreshnessFactor = 0.95m,
            SignalIdentity = $"ES-202609.{metric}.{timeFrame}"
        });

    static MarketAnalyticsSignalKind Kind(RegimeDiscoverySignalMetric metric) => metric switch
    {
        RegimeDiscoverySignalMetric.Ema20 or RegimeDiscoverySignalMetric.Ema50 or
            RegimeDiscoverySignalMetric.Ema200 or RegimeDiscoverySignalMetric.Ema20Slope or
            RegimeDiscoverySignalMetric.Ema50Slope or RegimeDiscoverySignalMetric.Ema200Slope or
            RegimeDiscoverySignalMetric.Ema20Interaction => MarketAnalyticsSignalKind.Ema,
        RegimeDiscoverySignalMetric.Rsi14 or RegimeDiscoverySignalMetric.Rsi14Slope =>
            MarketAnalyticsSignalKind.Rsi,
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

    static DateTime Utc(int hour, int minute) =>
        new(2026, 8, 26, hour, minute, 0, DateTimeKind.Utc);
}
