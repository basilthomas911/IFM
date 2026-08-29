using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Reference;

/// <summary>One generated, representative and non-authoritative Regime Discovery decision example.</summary>
[MessagePackObject]
public sealed record RegimeDiscoveryDecisionReferenceDto
{
    public const ushort CurrentGeneratorVersion = 1;

    [Key(0)] public string PipelineStage { get; init; } = "RegimeDiscovery";
    [Key(1)] public ushort GeneratorVersion { get; init; } = CurrentGeneratorVersion;
    [Key(2)] public ushort DecisionSchemaVersion { get; init; } = RegimeDiscoveryResult.CurrentSchemaVersion;
    [Key(3)] public string CoverageKind { get; init; } = "RepresentativePairwise";
    [Key(4)] public bool IsAuthoritative { get; init; }
    [Key(5)] public bool IsCompleteEnumeration { get; init; }
    [Key(6)] public string CaseCode { get; init; } = string.Empty;
    [Key(7)] public string Name { get; init; } = string.Empty;
    [Key(8)] public string[] CoverageTags { get; init; } = [];
    [Key(9)] public RegimeDirection TrendDirection { get; init; }
    [Key(10)] public TrendRegimePhase TrendPhase { get; init; }
    [Key(11)] public TrendRegimeStrength TrendStrength { get; init; }
    [Key(12)] public decimal TrendScore { get; init; }
    [Key(13)] public decimal TrendConfidence { get; init; }
    [Key(14)] public decimal TrendTimeFrameAgreement { get; init; }
    [Key(15)] public VolatilityRegimeLevel VolatilityLevel { get; init; }
    [Key(16)] public VolatilityRegimeChange VolatilityChange { get; init; }
    [Key(17)] public VxTermStructureRegime TermStructure { get; init; }
    [Key(18)] public decimal VolatilityScore { get; init; }
    [Key(19)] public decimal VolatilityConfidence { get; init; }
    [Key(20)] public MarketStructureClassification StructureClassification { get; init; }
    [Key(21)] public RegimeDirection StructureDirection { get; init; }
    [Key(22)] public MarketBreakoutState Breakout { get; init; }
    [Key(23)] public decimal StructureScore { get; init; }
    [Key(24)] public decimal StructureConfidence { get; init; }
    [Key(25)] public RegimeDirection DecisionDirection { get; init; }
    [Key(26)] public decimal DirectionalScore { get; init; }
    [Key(27)] public decimal RiskAdjustedConviction { get; init; }
    [Key(28)] public decimal DecisionConfidence { get; init; }
    [Key(29)] public RegimeConfidenceBand ConfidenceBand { get; init; }
    [Key(30)] public RegimeOverallQuality Quality { get; init; }
    [Key(31)] public RegimeRestriction[] Restrictions { get; init; } = [];
    [Key(32)] public string[] Reasons { get; init; } = [];
}
