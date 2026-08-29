using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.RegimeDiscovery;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Model;

namespace TomasAI.IFM.Domain.Trade.VerificationTests.Strategy.IntrinsicTime.RegimeDiscovery;

public static class RegimeDiscoveryScenarioDataBuilder
{
    public static readonly DateTime MarketDataAsOfUtc = new(2026, 8, 28, 15, 59, 0, DateTimeKind.Utc);
    public static readonly DateTime ProducedAtUtc = new(2026, 8, 28, 16, 0, 0, DateTimeKind.Utc);

    public static RegimeDiscoveryCalculationInput CreateInput(
        RegimeDiscoveryScenario scenario,
        TimeFrameType horizon,
        decimal freshnessFactor = 0.95m)
    {
        var parameterSet = CreateParameterSet(horizon);
        var contractId = $"ES-RDV-{scenario.Name}-{horizon}";
        var marketSeries = MarketSeriesIdentity.ForContract(contractId);
        var request = RegimeDiscoverySnapshotRequestFactory.Create(marketSeries, parameterSet);
        var observations = request.Requirements
            .Where(requirement => !scenario.OmittedMetrics.Contains(requirement.Metric))
            .Select((requirement, index) => new RegimeDiscoverySignalObservation
            {
                Metric = requirement.Metric,
                SignalKey = new MarketAnalyticsSignalKey(
                    marketSeries,
                    Kind(requirement.Metric),
                    requirement.TimeFrame,
                    requirement.CalculationConfigurationId),
                Value = scenario.Value(requirement.Metric),
                MarketDataAsOfUtc = MarketDataAsOfUtc,
                CalculatedAtUtc = MarketDataAsOfUtc,
                SourceSequence = index + 1,
                SchemaVersion = 1,
                CalculationVersion = "1",
                IsWarm = true,
                IsValid = true,
                Availability = RegimeDiscoverySignalAvailability.Available,
                FreshnessFactor = freshnessFactor,
                SignalIdentity = $"{contractId}.{requirement.Metric}.{requirement.TimeFrame}"
            })
            .ToArray();
        var entityId = IntrinsicTimeStrategyWorkflowEntityId.Create(new FuturesItiSignalEntityId(
            contractId,
            DateOnly.FromDateTime(ProducedAtUtc),
            horizon));
        return new RegimeDiscoveryCalculationInput
        {
            ResultId = Guid.Parse("0198E212-3C00-7000-8000-00000000A001"),
            WorkflowId = new StrategyWorkflowId(Guid.Parse("0198E212-3C00-7000-8000-00000000A002")),
            EntityId = entityId,
            TriggerEventId = Guid.Parse("0198E212-3C00-7000-8000-00000000A003"),
            ParameterSet = parameterSet,
            Snapshot = new RegimeDiscoveryMarketSignalSnapshot
            {
                SnapshotId = Guid.Parse("0198E212-3C00-7000-8000-00000000A004"),
                CacheRevision = 1,
                MarketSeriesIdentity = marketSeries,
                TargetHorizon = horizon,
                CapturedAtUtc = ProducedAtUtc,
                MarketDataAsOfUtc = MarketDataAsOfUtc,
                Observations = observations
            },
            ProducedAtUtc = ProducedAtUtc
        };
    }

    public static RegimeDiscoveryParameterSet CreateParameterSet(TimeFrameType horizon) =>
        RegimeDiscoveryParameterSet.CreateDefault(
            Guid.Parse("0198E212-3C00-7000-8000-00000000B001"),
            horizon switch
            {
                TimeFrameType.Daily => Guid.Parse("0198E212-3C00-7000-8000-00000000B101"),
                TimeFrameType.Weekly => Guid.Parse("0198E212-3C00-7000-8000-00000000B102"),
                TimeFrameType.Monthly => Guid.Parse("0198E212-3C00-7000-8000-00000000B103"),
                _ => throw new ArgumentOutOfRangeException(nameof(horizon), horizon, null)
            },
            horizon);

    public static MarketAnalyticsSignalKind Kind(RegimeDiscoverySignalMetric metric) => metric switch
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
}
