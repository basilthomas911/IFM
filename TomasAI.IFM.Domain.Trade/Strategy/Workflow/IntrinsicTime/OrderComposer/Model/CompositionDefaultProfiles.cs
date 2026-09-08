using System.Collections.Immutable;
using System.Text.Json;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Model;

/// <summary>Authors explicit engineering hypotheses for reviewed publication; never runs as a startup seed or fallback.</summary>
public static class CompositionDefaultProfiles
{
    public static OrderCompositionRules Create(IEnumerable<StrategyCatalogDefinition> variants, TimeFrameType horizon, string pricerVersion)
    {
        var rules = new OrderCompositionRules { SchemaVersion = 1, AlgorithmVersion = CompositionRulesContract.AlgorithmVersion,
            PricerVersion = pricerVersion, SupportedHorizon = horizon, InstrumentRoot = "ES", Currency = "USD",
            VariantRules = variants.Select(v => new CompositionVariantRules
            {
                VariantKey = v.Key, StructureKey = v.Parent!, BaseParameters = Parameters(horizon) with
                { TargetNetDelta = v.Settings.GetProperty("TargetNetDelta").GetDecimal(), BalanceTolerance = v.Settings.GetProperty("BalanceTolerance").GetDecimal() },
                AllowedWidths = v.Settings.GetProperty("MaximumWingWidth").GetDecimal() == 0 ? [] :
                    new[] { 5m, 10m, 15m, 20m }.Where(w => w >= v.Settings.GetProperty("MinimumWingWidth").GetDecimal()
                        && w <= v.Settings.GetProperty("MaximumWingWidth").GetDecimal()).ToImmutableArray(),
                RequireSymmetricWings = v.Settings.GetProperty("SymmetricWings").GetBoolean(),
                DeltaUnits = "UnderlyingEquivalent", RankingVersion = CompositionRulesContract.RankingVersion,
                HardBounds = [], AdjustmentRules = []
            }).ToImmutableArray() };
        CompositionRulesContract.Validate(rules); return rules;
    }

    public static CompositionParameters Parameters(TimeFrameType horizon) => new()
    {
        LoadingMilliseconds = 5000, ExecutionMilliseconds = 5000, CandidateLifetimeMilliseconds = 2000,
        MaximumQuoteAgeMilliseconds = 1000, MaximumQuoteSkewMilliseconds = 250, MinimumDisplayedSize = 1,
        ParticipationFraction = .10m, MaximumUnderlyingSpreadTicks = 4, MaximumLegSpreadTicks = 8, MaximumComboSpreadTicks = 16,
        TargetDaysToExpiry = horizon switch { TimeFrameType.Daily => 30, TimeFrameType.Weekly => 45, TimeFrameType.Monthly => 60, _ => throw new ArgumentException("Unsupported horizon.") },
        MinimumDaysToExpiry = horizon switch { TimeFrameType.Daily => 7, TimeFrameType.Weekly => 14, _ => 21 },
        MaximumDaysToExpiry = horizon switch { TimeFrameType.Daily => 60, TimeFrameType.Weekly => 90, _ => 120 },
        TargetLegDelta = .35m, LegDeltaTolerance = .10m, TargetPutDelta = .20m, TargetCallDelta = .20m,
        TargetNetDelta = 0, BalanceTolerance = .05m, MinimumCreditToWidth = .10m, MaximumDebitToWidth = .60m,
        MinimumCreditTicks = 1, MinimumRewardToRisk = .10m, MidpointToNaturalFraction = .50m, MaximumAdverseMoveTicks = 0,
        FeePerContract = 2.50m, SlippageTicksPerLeg = 1, FuturesPlannedDistance = 20, FuturesStressDistance = 100, FuturesRollHours = 120
    };
}
