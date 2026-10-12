using MessagePack;
using System.Text.Json.Serialization;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.TradeSelection;

/// <summary>Explicit engineering profiles; no startup publication or randomly generated identities.</summary>
public static class TradeSelectionDefaultProfiles
{
    // Saved engineering authoring identities. Calling this factory does not insert or publish configuration.
    public static readonly Guid DailyProfileId = Guid.Parse("ec56ea27-d625-4bb2-a6a1-f4ac3c2ef701");
    public static readonly Guid WeeklyProfileId = Guid.Parse("ec56ea27-d625-4bb2-a6a1-f4ac3c2ef702");
    public static readonly Guid MonthlyProfileId = Guid.Parse("ec56ea27-d625-4bb2-a6a1-f4ac3c2ef703");
    public static TradeSelectionParameterSet[] EngineeringDefaults() => [Create(DailyProfileId, TimeFrameType.Daily), Create(WeeklyProfileId, TimeFrameType.Weekly), Create(MonthlyProfileId, TimeFrameType.Monthly)];

    public static TradeSelectionParameterSet Create(Guid id, TimeFrameType horizon, int version = 1)
    {
        var result = new TradeSelectionParameterSet
        {
            SchemaVersion = 1,
            ParameterSetId = id,
            Version = version,
            ProfileCode = $"TS.ES.{horizon}.Test",
            InstrumentRoot = "ES",
            TargetHorizon = horizon,
            MinimumRegimeConfidence = 0.50m,
            MinimumAssessmentConfidence = 0.50m,
            AllowedRegimeDirections = [RegimeDirection.Up, RegimeDirection.Down, RegimeDirection.Neutral],
            AllowedTrendPhases = [TrendRegimePhase.RangeBound, TrendRegimePhase.Emerging, TrendRegimePhase.Established],
            AllowedTrendStrengths = [TrendRegimeStrength.None, TrendRegimeStrength.Weak, TrendRegimeStrength.Moderate, TrendRegimeStrength.Strong, TrendRegimeStrength.Extreme],
            AllowedRegimeQualities = [RegimeOverallQuality.Acceptable, RegimeOverallQuality.High],
            AllowedRegimeVolatilityLevels = [VolatilityRegimeLevel.Low, VolatilityRegimeLevel.Normal, VolatilityRegimeLevel.High],
            AllowedRegimeVolatilityChanges = [VolatilityRegimeChange.Contracting, VolatilityRegimeChange.Stable, VolatilityRegimeChange.Expanding],
            AllowedStructureClassifications = [MarketStructureClassification.Trending, MarketStructureClassification.Ranging, MarketStructureClassification.Compressing, MarketStructureClassification.Expanding, MarketStructureClassification.BreakingOut],
            RejectedInheritedRestrictions = [RegimeRestriction.NoNewTrade, RegimeRestriction.DirectionConflict, RegimeRestriction.LowConfidence, RegimeRestriction.Transition],
            AllowedAssessmentConditions = [AssessmentCondition.Directional, AssessmentCondition.RangeBound, AssessmentCondition.VolatilityExpansion, AssessmentCondition.VolatilityContraction],
            AllowedLiquidity = [AssessmentLiquidity.Healthy, AssessmentLiquidity.Degraded],
            AllowedSessions = [MarketSessionStatus.Open],
            AllowedEventRisk = [AssessmentEventContext.Clear],
            AllowedStress = [AssessmentStress.Normal],
            AllowedVolatilityBehavior = [AssessmentVolatility.Stable, AssessmentVolatility.Expanding, AssessmentVolatility.Contracting],
            AllowedTriggerAlignment = [AssessmentTriggerAlignment.Aligned, AssessmentTriggerAlignment.Neutral, AssessmentTriggerAlignment.NotApplicable],
            AllowedAssessmentDataQuality = [MarketConditionDataQuality.Healthy, MarketConditionDataQuality.Degraded],
            UnknownEvidencePolicy = UnknownEvidencePolicy.NoTrade,
            VariantRules = Rules(),
            RankingPolicyVersion = "ts-rank-v1",
            MaximumAssignments = 16,
            MaximumCandidates = 64,
            MaximumCatalogDefinitions = 256,
            MaximumBindingPayloadBytes = 262144,
            MaximumExecutionMilliseconds = 2000,
            ResultLifetimeSeconds = 30,
            FutureClockSkewSeconds = 2,
            MaximumResultPayloadBytes = 262144,
            ReasonCodeCatalogVersion = "ts-reasons-v1",
            SummaryTemplateVersion = "ts-summary-v1",
            DirectionMappingVersion = "ts-direction-v1",
        };
        TradeSelectionPolicy.Validate(result);
        return result;
    }

    static SelectionVariantRule[] Rules()
    {
        List<SelectionVariantRule> rules = [];
        foreach (var bias in new[] { "Bullish", "Bearish" })
            rules.Add(Rule("Future", string.Equals(bias,"Bullish",StringComparison.Ordinal) ? "Long" : "Short", bias, "None", 10));
        rules.Add(Rule("CallVertical", "Long", "Bullish", "Debit", 20));
        rules.Add(Rule("PutVertical", "Long", "Bearish", "Debit", 20));
        rules.Add(Rule("PutVertical", "Short", "Bullish", "Credit", 30));
        rules.Add(Rule("CallVertical", "Short", "Bearish", "Credit", 30));
        foreach (var side in new[] { "Short", "Long" })
            foreach (var bias in new[] { "Balanced", "Bullish", "Bearish" })
                rules.Add(Rule("IronCondor", side, bias, string.Equals(side,"Short",StringComparison.Ordinal) ? "Credit" : "Debit", string.Equals(side,"Short",StringComparison.Ordinal) ? 40 : 50));
        return [.. rules];
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1508", Justification = "The credit branch remains reachable when balanced is false and credit is true; retain the existing strategy rule truth table across the nested switch expressions.")]
    static SelectionVariantRule Rule(string builder, string side, string bias, string premium, int preference)
    {
        var condor = string.Equals(builder,"IronCondor",StringComparison.Ordinal); var balanced = string.Equals(bias,"Balanced",StringComparison.Ordinal); var credit = string.Equals(premium,"Credit",StringComparison.Ordinal);
        return new()
        {
            BuilderCapabilityCode = builder,
            BuilderCapabilityVersion = 1,
            Side = side,
            Bias = bias,
            PremiumMode = premium,
            Preference = preference,
            AllowedRegimeDirections = [(balanced) switch { true => RegimeDirection.Neutral, false => (string.Equals(bias,"Bullish",StringComparison.Ordinal)) switch { true => RegimeDirection.Up, false => RegimeDirection.Down } }],
            AllowedTrendPhases = (balanced) switch { true => (credit) switch { true => [TrendRegimePhase.RangeBound, TrendRegimePhase.Established], false => [TrendRegimePhase.RangeBound, TrendRegimePhase.Emerging, TrendRegimePhase.Established] }, false => [TrendRegimePhase.Emerging, TrendRegimePhase.Established] },
            AllowedTrendStrengths = (balanced && credit) switch { true => [TrendRegimeStrength.None, TrendRegimeStrength.Weak, TrendRegimeStrength.Moderate], false => (condor) switch { true => (credit) switch { true => [TrendRegimeStrength.Weak, TrendRegimeStrength.Moderate, TrendRegimeStrength.Strong], false => (balanced) switch { true => [TrendRegimeStrength.None, TrendRegimeStrength.Weak, TrendRegimeStrength.Moderate], false => [TrendRegimeStrength.Weak, TrendRegimeStrength.Moderate, TrendRegimeStrength.Strong, TrendRegimeStrength.Extreme] } }, false => [TrendRegimeStrength.Moderate, TrendRegimeStrength.Strong, TrendRegimeStrength.Extreme] } },
            AllowedStructureClassifications = (balanced) switch { true => (credit) switch { true => [MarketStructureClassification.Ranging, MarketStructureClassification.Compressing], false => [MarketStructureClassification.Ranging, MarketStructureClassification.Expanding, MarketStructureClassification.BreakingOut] }, false => (credit) switch { true => [MarketStructureClassification.Trending, MarketStructureClassification.Ranging, MarketStructureClassification.Compressing], false => [MarketStructureClassification.Trending, MarketStructureClassification.Expanding, MarketStructureClassification.BreakingOut] } },
            AllowedAssessmentConditions = (balanced) switch { true => (credit) switch { true => [AssessmentCondition.RangeBound, AssessmentCondition.VolatilityContraction], false => [AssessmentCondition.VolatilityExpansion] }, false => (credit) switch { true => [AssessmentCondition.Directional, AssessmentCondition.VolatilityContraction], false => [AssessmentCondition.Directional, AssessmentCondition.VolatilityExpansion] } },
            AllowedVolatilityBehavior = (condor && !credit) switch { true => [AssessmentVolatility.Expanding], false => (credit) switch { true => [AssessmentVolatility.Stable, AssessmentVolatility.Contracting], false => (string.Equals(builder,"Future",StringComparison.Ordinal)) switch { true => [AssessmentVolatility.Stable, AssessmentVolatility.Expanding, AssessmentVolatility.Contracting], false => [AssessmentVolatility.Stable, AssessmentVolatility.Expanding] } } }
        };
    }
}
