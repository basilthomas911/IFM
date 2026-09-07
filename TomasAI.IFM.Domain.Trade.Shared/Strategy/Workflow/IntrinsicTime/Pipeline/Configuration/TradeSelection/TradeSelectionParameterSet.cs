using MessagePack;
using System.Text.Json.Serialization;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.TradeSelection;

public enum UnknownEvidencePolicy : byte { NoTrade=1 }
[MessagePackObject]
public sealed record TradeSelectionParameterSet
{
    [Key(0), JsonRequired] public short SchemaVersion { get; init; }
    [Key(1), JsonRequired] public Guid ParameterSetId { get; init; }
    [Key(2), JsonRequired] public int Version { get; init; }
    [Key(3), JsonRequired] public string ProfileCode { get; init; } = string.Empty;
    [Key(4), JsonRequired] public string InstrumentRoot { get; init; } = string.Empty;
    [Key(5), JsonRequired] public TimeFrameType TargetHorizon { get; init; }
    [Key(6), JsonRequired] public decimal MinimumRegimeConfidence { get; init; }
    [Key(7), JsonRequired] public decimal MinimumAssessmentConfidence { get; init; }
    RegimeDirection[] _AllowedRegimeDirections = [];
    [Key(8), JsonRequired] public RegimeDirection[] AllowedRegimeDirections { get => [.. _AllowedRegimeDirections]; init => _AllowedRegimeDirections = value is null ? [] : [.. value]; }
    TrendRegimePhase[] _AllowedTrendPhases = [];
    [Key(9), JsonRequired] public TrendRegimePhase[] AllowedTrendPhases { get => [.. _AllowedTrendPhases]; init => _AllowedTrendPhases = value is null ? [] : [.. value]; }
    TrendRegimeStrength[] _AllowedTrendStrengths = [];
    [Key(10), JsonRequired] public TrendRegimeStrength[] AllowedTrendStrengths { get => [.. _AllowedTrendStrengths]; init => _AllowedTrendStrengths = value is null ? [] : [.. value]; }
    RegimeOverallQuality[] _AllowedRegimeQualities = [];
    [Key(11), JsonRequired] public RegimeOverallQuality[] AllowedRegimeQualities { get => [.. _AllowedRegimeQualities]; init => _AllowedRegimeQualities = value is null ? [] : [.. value]; }
    VolatilityRegimeLevel[] _AllowedRegimeVolatilityLevels = [];
    [Key(12), JsonRequired] public VolatilityRegimeLevel[] AllowedRegimeVolatilityLevels { get => [.. _AllowedRegimeVolatilityLevels]; init => _AllowedRegimeVolatilityLevels = value is null ? [] : [.. value]; }
    VolatilityRegimeChange[] _AllowedRegimeVolatilityChanges = [];
    [Key(13), JsonRequired] public VolatilityRegimeChange[] AllowedRegimeVolatilityChanges { get => [.. _AllowedRegimeVolatilityChanges]; init => _AllowedRegimeVolatilityChanges = value is null ? [] : [.. value]; }
    MarketStructureClassification[] _AllowedStructureClassifications = [];
    [Key(14), JsonRequired] public MarketStructureClassification[] AllowedStructureClassifications { get => [.. _AllowedStructureClassifications]; init => _AllowedStructureClassifications = value is null ? [] : [.. value]; }
    RegimeRestriction[] _RejectedInheritedRestrictions = [];
    [Key(15), JsonRequired] public RegimeRestriction[] RejectedInheritedRestrictions { get => [.. _RejectedInheritedRestrictions]; init => _RejectedInheritedRestrictions = value is null ? [] : [.. value]; }
    AssessmentCondition[] _AllowedAssessmentConditions = [];
    [Key(16), JsonRequired] public AssessmentCondition[] AllowedAssessmentConditions { get => [.. _AllowedAssessmentConditions]; init => _AllowedAssessmentConditions = value is null ? [] : [.. value]; }
    AssessmentLiquidity[] _AllowedLiquidity = [];
    [Key(17), JsonRequired] public AssessmentLiquidity[] AllowedLiquidity { get => [.. _AllowedLiquidity]; init => _AllowedLiquidity = value is null ? [] : [.. value]; }
    MarketSessionStatus[] _AllowedSessions = [];
    [Key(18), JsonRequired] public MarketSessionStatus[] AllowedSessions { get => [.. _AllowedSessions]; init => _AllowedSessions = value is null ? [] : [.. value]; }
    AssessmentEventContext[] _AllowedEventRisk = [];
    [Key(19), JsonRequired] public AssessmentEventContext[] AllowedEventRisk { get => [.. _AllowedEventRisk]; init => _AllowedEventRisk = value is null ? [] : [.. value]; }
    AssessmentStress[] _AllowedStress = [];
    [Key(20), JsonRequired] public AssessmentStress[] AllowedStress { get => [.. _AllowedStress]; init => _AllowedStress = value is null ? [] : [.. value]; }
    AssessmentVolatility[] _AllowedVolatilityBehavior = [];
    [Key(21), JsonRequired] public AssessmentVolatility[] AllowedVolatilityBehavior { get => [.. _AllowedVolatilityBehavior]; init => _AllowedVolatilityBehavior = value is null ? [] : [.. value]; }
    AssessmentTriggerAlignment[] _AllowedTriggerAlignment = [];
    [Key(22), JsonRequired] public AssessmentTriggerAlignment[] AllowedTriggerAlignment { get => [.. _AllowedTriggerAlignment]; init => _AllowedTriggerAlignment = value is null ? [] : [.. value]; }
    MarketConditionDataQuality[] _AllowedAssessmentDataQuality = [];
    [Key(23), JsonRequired] public MarketConditionDataQuality[] AllowedAssessmentDataQuality { get => [.. _AllowedAssessmentDataQuality]; init => _AllowedAssessmentDataQuality = value is null ? [] : [.. value]; }
    [Key(24), JsonRequired] public UnknownEvidencePolicy UnknownEvidencePolicy { get; init; }
    SelectionVariantRule[] _VariantRules = [];
    [Key(25), JsonRequired] public SelectionVariantRule[] VariantRules { get => [.. _VariantRules]; init => _VariantRules = value is null ? [] : [.. value]; }
    [Key(26), JsonRequired] public string RankingPolicyVersion { get; init; } = string.Empty;
    [Key(27), JsonRequired] public int MaximumAssignments { get; init; }
    [Key(28), JsonRequired] public int MaximumCandidates { get; init; }
    [Key(29), JsonRequired] public int MaximumCatalogDefinitions { get; init; }
    [Key(30), JsonRequired] public int MaximumBindingPayloadBytes { get; init; }
    [Key(31), JsonRequired] public int MaximumExecutionMilliseconds { get; init; }
    [Key(32), JsonRequired] public int ResultLifetimeSeconds { get; init; }
    [Key(33), JsonRequired] public int FutureClockSkewSeconds { get; init; }
    [Key(34), JsonRequired] public int MaximumResultPayloadBytes { get; init; }
    [Key(35), JsonRequired] public string ReasonCodeCatalogVersion { get; init; } = string.Empty;
    [Key(36), JsonRequired] public string SummaryTemplateVersion { get; init; } = string.Empty;
    [Key(37), JsonRequired] public string DirectionMappingVersion { get; init; } = string.Empty;
}
[MessagePackObject]
public sealed record SelectionVariantRule
{
    [Key(0), JsonRequired] public string BuilderCapabilityCode { get; init; } = string.Empty;
    [Key(1), JsonRequired] public int BuilderCapabilityVersion { get; init; }
    [Key(2), JsonRequired] public string Side { get; init; } = string.Empty;
    [Key(3), JsonRequired] public string Bias { get; init; } = string.Empty;
    [Key(4), JsonRequired] public string PremiumMode { get; init; } = string.Empty;
    RegimeDirection[] _AllowedRegimeDirections = [];
    [Key(5), JsonRequired] public RegimeDirection[] AllowedRegimeDirections { get => [.. _AllowedRegimeDirections]; init => _AllowedRegimeDirections = value is null ? [] : [.. value]; }
    TrendRegimePhase[] _AllowedTrendPhases = [];
    [Key(6), JsonRequired] public TrendRegimePhase[] AllowedTrendPhases { get => [.. _AllowedTrendPhases]; init => _AllowedTrendPhases = value is null ? [] : [.. value]; }
    TrendRegimeStrength[] _AllowedTrendStrengths = [];
    [Key(7), JsonRequired] public TrendRegimeStrength[] AllowedTrendStrengths { get => [.. _AllowedTrendStrengths]; init => _AllowedTrendStrengths = value is null ? [] : [.. value]; }
    MarketStructureClassification[] _AllowedStructureClassifications = [];
    [Key(8), JsonRequired] public MarketStructureClassification[] AllowedStructureClassifications { get => [.. _AllowedStructureClassifications]; init => _AllowedStructureClassifications = value is null ? [] : [.. value]; }
    AssessmentCondition[] _AllowedAssessmentConditions = [];
    [Key(9), JsonRequired] public AssessmentCondition[] AllowedAssessmentConditions { get => [.. _AllowedAssessmentConditions]; init => _AllowedAssessmentConditions = value is null ? [] : [.. value]; }
    AssessmentVolatility[] _AllowedVolatilityBehavior = [];
    [Key(10), JsonRequired] public AssessmentVolatility[] AllowedVolatilityBehavior { get => [.. _AllowedVolatilityBehavior]; init => _AllowedVolatilityBehavior = value is null ? [] : [.. value]; }
    [Key(11), JsonRequired] public int Preference { get; init; }
}
[MessagePackObject]
public sealed record TradeSelectionSpecializedRules
{
    [Key(0), JsonRequired] public short SchemaVersion { get; init; }
    SelectionVariantRule[] _Rules = [];
    [Key(1), JsonRequired] public SelectionVariantRule[] Rules { get => [.. _Rules]; init => _Rules = value is null ? [] : [.. value]; }
}
