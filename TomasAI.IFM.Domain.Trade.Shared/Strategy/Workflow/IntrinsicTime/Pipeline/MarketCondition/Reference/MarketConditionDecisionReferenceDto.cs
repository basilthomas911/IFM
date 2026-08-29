using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Reference;

/// <summary>One generated, representative and non-authoritative Market Condition decision example.</summary>
[MessagePackObject]
public sealed record MarketConditionDecisionReferenceDto
{
    public const ushort CurrentGeneratorVersion = 1;

    [Key(0)] public string PipelineStage { get; init; } = "MarketCondition";
    [Key(1)] public ushort GeneratorVersion { get; init; } = CurrentGeneratorVersion;
    [Key(2)] public ushort DecisionSchemaVersion { get; init; } = MarketConditionResult.CurrentSchemaVersion;
    [Key(3)] public string CoverageKind { get; init; } = "RepresentativePairwise";
    [Key(4)] public bool IsAuthoritative { get; init; }
    [Key(5)] public bool IsCompleteEnumeration { get; init; }
    [Key(6)] public string CaseCode { get; init; } = string.Empty;
    [Key(7)] public string Name { get; init; } = string.Empty;
    [Key(8)] public string[] CoverageTags { get; init; } = [];
    [Key(9)] public TimeFrameType TargetHorizon { get; init; }
    [Key(10)] public RegimeDirection RegimeDirection { get; init; }
    [Key(11)] public TrendRegimePhase TrendPhase { get; init; }
    [Key(12)] public VolatilityRegimeLevel VolatilityLevel { get; init; }
    [Key(13)] public VolatilityRegimeChange VolatilityChange { get; init; }
    [Key(14)] public VxTermStructureRegime TermStructure { get; init; }
    [Key(15)] public MarketStructureClassification StructureClassification { get; init; }
    [Key(16)] public MarketBreakoutState Breakout { get; init; }
    [Key(17)] public bool TriggerConflict { get; init; }
    [Key(18)] public bool OptionQualityBlocked { get; init; }
    [Key(19)] public bool RegimeNoNewTrade { get; init; }
    [Key(20)] public MarketTradeability Tradeability { get; init; }
    [Key(21)] public MarketConditionType ConditionType { get; init; }
    [Key(22)] public MarketConditionDirection Direction { get; init; }
    [Key(23)] public MarketConditionPhase Phase { get; init; }
    [Key(24)] public decimal Strength { get; init; }
    [Key(25)] public decimal Confidence { get; init; }
    [Key(26)] public MarketConditionVolatilityBehavior VolatilityBehavior { get; init; }
    [Key(27)] public MarketConditionLiquidityQuality LiquidityQuality { get; init; }
    [Key(28)] public MarketConditionDataQuality DataQuality { get; init; }
    [Key(29)] public MarketConditionUpstreamAlignment UpstreamAlignment { get; init; }
    [Key(30)] public string PrimaryReasonCode { get; init; } = string.Empty;
    [Key(31)] public string[] Reasons { get; init; } = [];
    [Key(32)] public string[] BlockingReasons { get; init; } = [];
    [Key(33)] public string[] EvidenceFeatures { get; init; } = [];
    [Key(34)] public MarketConditionTradeType HintTradeType { get; init; }
    [Key(35)] public TimeFrameType HintTimeFrame { get; init; }
    [Key(36)] public MarketConditionHintSuitability HintSuitability { get; init; }
    [Key(37)] public decimal HintConfidence { get; init; }
    [Key(38)] public string HintReasonCode { get; init; } = string.Empty;
    [Key(39)] public bool HintIsAdvisory { get; init; }
}
