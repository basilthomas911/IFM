using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Reference;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Model;

/// <summary>Generates a bounded, deterministic reference catalog by exercising the production fusion model.</summary>
public sealed class RegimeDiscoveryDecisionReferenceGenerator(MarketRegimeFusionModel? fusionModel = null)
{
    readonly MarketRegimeFusionModel _fusionModel = fusionModel ?? new();

    public RegimeDiscoveryDecisionReferenceDto[] Generate() => Cases.Select(Generate).ToArray();

    RegimeDiscoveryDecisionReferenceDto Generate(ReferenceCase value)
    {
        var trendScore = DirectionScore(value.TrendDirection);
        var structureScore = DirectionScore(value.StructureDirection);
        var confidence = value.LowConfidence ? 0.30m : 0.85m;
        var trend = new TrendRegimeResult
        {
            IsComplete = true,
            Direction = value.TrendDirection,
            Phase = value.TrendPhase,
            Strength = trendScore == 0m ? TrendRegimeStrength.None : TrendRegimeStrength.Strong,
            Score = trendScore,
            Confidence = confidence,
            TimeFrameAgreement = confidence
        };
        var volatility = new VolatilityRegimeResult
        {
            IsComplete = true,
            Level = value.VolatilityLevel,
            Change = value.VolatilityChange,
            TermStructure = value.TermStructure,
            Score = VolatilityScore(value.VolatilityLevel),
            Confidence = confidence,
            NoNewTrade = value.VolatilityLevel == VolatilityRegimeLevel.Extreme
        };
        var structure = new MarketStructureRegimeResult
        {
            IsComplete = true,
            Classification = value.StructureClassification,
            Direction = value.StructureDirection,
            Breakout = value.StructureClassification == MarketStructureClassification.BreakingOut
                ? value.StructureDirection == RegimeDirection.Down ? MarketBreakoutState.Down : MarketBreakoutState.Up
                : MarketBreakoutState.None,
            Score = structureScore,
            Confidence = confidence
        };
        var decision = _fusionModel.Calculate(trend, volatility, structure, new MarketRegimeFusionConfiguration());
        return new RegimeDiscoveryDecisionReferenceDto
        {
            CaseCode = value.Code,
            Name = value.Name,
            CoverageTags = value.Tags,
            TrendDirection = trend.Direction,
            TrendPhase = trend.Phase,
            TrendStrength = trend.Strength,
            TrendScore = trend.Score,
            TrendConfidence = trend.Confidence,
            TrendTimeFrameAgreement = trend.TimeFrameAgreement,
            VolatilityLevel = volatility.Level,
            VolatilityChange = volatility.Change,
            TermStructure = volatility.TermStructure,
            VolatilityScore = volatility.Score,
            VolatilityConfidence = volatility.Confidence,
            StructureClassification = structure.Classification,
            StructureDirection = structure.Direction,
            Breakout = structure.Breakout,
            StructureScore = structure.Score,
            StructureConfidence = structure.Confidence,
            DecisionDirection = decision.Direction,
            DirectionalScore = decision.DirectionalScore,
            RiskAdjustedConviction = decision.RiskAdjustedConviction,
            DecisionConfidence = decision.Confidence,
            ConfidenceBand = decision.ConfidenceBand,
            Quality = decision.Quality,
            Restrictions = decision.Restrictions,
            Reasons = decision.Reasons.Select(reason => reason.Code).ToArray()
        };
    }

    static decimal DirectionScore(RegimeDirection direction) => direction switch
    {
        RegimeDirection.Up => 0.8m,
        RegimeDirection.Down => -0.8m,
        _ => 0m
    };

    static decimal VolatilityScore(VolatilityRegimeLevel level) => level switch
    {
        VolatilityRegimeLevel.Low => 0.15m,
        VolatilityRegimeLevel.Normal => 0.40m,
        VolatilityRegimeLevel.High => 0.65m,
        VolatilityRegimeLevel.Extreme => 0.85m,
        _ => 0m
    };

    static readonly ReferenceCase[] Cases =
    [
        Case("RD-REF-001", "Established bullish trend", ["DirectionalAlignment", "Bullish"],
            RegimeDirection.Up, TrendRegimePhase.Established, VolatilityRegimeLevel.Normal,
            VolatilityRegimeChange.Stable, VxTermStructureRegime.Contango,
            MarketStructureClassification.Trending, RegimeDirection.Up),
        Case("RD-REF-002", "Established bearish trend", ["DirectionalAlignment", "Bearish"],
            RegimeDirection.Down, TrendRegimePhase.Established, VolatilityRegimeLevel.Normal,
            VolatilityRegimeChange.Stable, VxTermStructureRegime.Contango,
            MarketStructureClassification.Trending, RegimeDirection.Down),
        Case("RD-REF-003", "Quiet range contraction", ["Neutral", "Contraction", "Range"],
            RegimeDirection.Neutral, TrendRegimePhase.RangeBound, VolatilityRegimeLevel.Low,
            VolatilityRegimeChange.Contracting, VxTermStructureRegime.Contango,
            MarketStructureClassification.Ranging, RegimeDirection.Neutral),
        Case("RD-REF-004", "Emerging bullish structure", ["Emerging", "Bullish"],
            RegimeDirection.Up, TrendRegimePhase.Emerging, VolatilityRegimeLevel.Low,
            VolatilityRegimeChange.Stable, VxTermStructureRegime.Flat,
            MarketStructureClassification.Trending, RegimeDirection.Up),
        Case("RD-REF-005", "Exhausting trend with expansion", ["Transition", "Expansion", "Exhaustion"],
            RegimeDirection.Up, TrendRegimePhase.Exhausting, VolatilityRegimeLevel.High,
            VolatilityRegimeChange.Expanding, VxTermStructureRegime.Backwardation,
            MarketStructureClassification.Expanding, RegimeDirection.Up),
        Case("RD-REF-006", "Bearish reversal with expansion", ["Transition", "Expansion", "Reversal"],
            RegimeDirection.Down, TrendRegimePhase.Reversing, VolatilityRegimeLevel.High,
            VolatilityRegimeChange.Expanding, VxTermStructureRegime.Backwardation,
            MarketStructureClassification.Trending, RegimeDirection.Down),
        Case("RD-REF-007", "Directional specialist conflict", ["SpecialistConflict"],
            RegimeDirection.Up, TrendRegimePhase.Established, VolatilityRegimeLevel.Normal,
            VolatilityRegimeChange.Stable, VxTermStructureRegime.Flat,
            MarketStructureClassification.Trending, RegimeDirection.Down),
        Case("RD-REF-008", "Neutral trend with bullish breakout", ["StructureLed", "Breakout"],
            RegimeDirection.Neutral, TrendRegimePhase.RangeBound, VolatilityRegimeLevel.Normal,
            VolatilityRegimeChange.Stable, VxTermStructureRegime.Contango,
            MarketStructureClassification.BreakingOut, RegimeDirection.Up),
        Case("RD-REF-009", "Directional transition", ["StructuralTransition"],
            RegimeDirection.Up, TrendRegimePhase.Established, VolatilityRegimeLevel.Normal,
            VolatilityRegimeChange.Stable, VxTermStructureRegime.Flat,
            MarketStructureClassification.Transitioning, RegimeDirection.Neutral),
        Case("RD-REF-010", "Extreme volatility blocker", ["VolatilityBoundary", "NoNewTrade"],
            RegimeDirection.Up, TrendRegimePhase.Established, VolatilityRegimeLevel.Extreme,
            VolatilityRegimeChange.Expanding, VxTermStructureRegime.Backwardation,
            MarketStructureClassification.Expanding, RegimeDirection.Up),
        Case("RD-REF-011", "Low-confidence mixed market", ["LowConfidence", "Mixed"],
            RegimeDirection.Up, TrendRegimePhase.Emerging, VolatilityRegimeLevel.Normal,
            VolatilityRegimeChange.Stable, VxTermStructureRegime.Flat,
            MarketStructureClassification.Ranging, RegimeDirection.Neutral, true),
        Case("RD-REF-012", "Neutral compression", ["Neutral", "Compression"],
            RegimeDirection.Neutral, TrendRegimePhase.RangeBound, VolatilityRegimeLevel.Low,
            VolatilityRegimeChange.Contracting, VxTermStructureRegime.Contango,
            MarketStructureClassification.Compressing, RegimeDirection.Neutral)
    ];

    static ReferenceCase Case(string code, string name, string[] tags, RegimeDirection trendDirection,
        TrendRegimePhase trendPhase, VolatilityRegimeLevel volatilityLevel,
        VolatilityRegimeChange volatilityChange, VxTermStructureRegime termStructure,
        MarketStructureClassification structureClassification, RegimeDirection structureDirection,
        bool lowConfidence = false) => new(code, name, tags, trendDirection, trendPhase, volatilityLevel,
            volatilityChange, termStructure, structureClassification, structureDirection, lowConfidence);

    sealed record ReferenceCase(string Code, string Name, string[] Tags, RegimeDirection TrendDirection,
        TrendRegimePhase TrendPhase, VolatilityRegimeLevel VolatilityLevel,
        VolatilityRegimeChange VolatilityChange, VxTermStructureRegime TermStructure,
        MarketStructureClassification StructureClassification, RegimeDirection StructureDirection,
        bool LowConfidence);
}
