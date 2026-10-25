using TomasAI.IFM.Domain.Trade.Shared;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;

/// <summary>Contains the deterministic, evidence-derived Regime Discovery decision.</summary>
[MessagePackObject]
public sealed record RegimeDiscoveryDecision
{
    /// <summary>Gets whether Fusion completed successfully.</summary>
    [Key(0)] public bool IsComplete { get; init; }
    /// <summary>Gets the fused direction.</summary>
    [Key(1)] public RegimeDirection Direction { get; init; }
    /// <summary>Gets the signed fused directional score.</summary>
    [Key(2)] public decimal DirectionalScore { get; init; }
    /// <summary>Gets risk-adjusted directional conviction.</summary>
    [Key(3)] public decimal RiskAdjustedConviction { get; init; }
    /// <summary>Gets normalized fused confidence.</summary>
    [Key(4)] public decimal Confidence { get; init; }
    /// <summary>Gets the fused confidence band.</summary>
    [Key(5)] public RegimeConfidenceBand ConfidenceBand { get; init; }
    /// <summary>Gets the final result quality.</summary>
    [Key(6)] public RegimeOverallQuality Quality { get; init; }
    /// <summary>Gets deterministic restrictions for later pipeline stages.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public trade model contract.")]
    [Key(7)] public RegimeRestriction[] Restrictions { get; init; } = [];
    /// <summary>Gets stable Fusion reasons in deterministic order.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public trade model contract.")]
    [Key(8)] public RegimeDiscoveryReason[] Reasons { get; init; } = [];
    /// <summary>Gets the trend phase incorporated into final conviction and restrictions.</summary>
    [Key(9)] public TrendRegimePhase TrendPhase { get; init; }
    /// <summary>Gets the trend strength represented by the decision.</summary>
    [Key(10)] public TrendRegimeStrength TrendStrength { get; init; }
    /// <summary>Gets cross-timeframe trend agreement.</summary>
    [Key(11)] public decimal TrendTimeFrameAgreement { get; init; }
    /// <summary>Gets the volatility level incorporated into the decision.</summary>
    [Key(12)] public VolatilityRegimeLevel VolatilityLevel { get; init; }
    /// <summary>Gets the volatility change incorporated into the decision.</summary>
    [Key(13)] public VolatilityRegimeChange VolatilityChange { get; init; }
    /// <summary>Gets the VX term-structure state incorporated into the decision.</summary>
    [Key(14)] public VxTermStructureRegime TermStructure { get; init; }
    /// <summary>Gets the market-structure classification incorporated into the decision.</summary>
    [Key(15)] public MarketStructureClassification StructureClassification { get; init; }
    /// <summary>Gets the market-structure breakout state incorporated into the decision.</summary>
    [Key(16)] public MarketBreakoutState Breakout { get; init; }
}
