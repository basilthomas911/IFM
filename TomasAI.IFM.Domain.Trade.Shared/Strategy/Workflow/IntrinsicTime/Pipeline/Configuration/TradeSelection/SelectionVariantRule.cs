using MessagePack;
using System.Text.Json.Serialization;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.TradeSelection;
[MessagePackObject]
public sealed record SelectionVariantRule
{
    [Key(0), JsonRequired] public string BuilderCapabilityCode { get; init; } = string.Empty;
    [Key(1), JsonRequired] public int BuilderCapabilityVersion { get; init; }
    [Key(2), JsonRequired] public string Side { get; init; } = string.Empty;
    [Key(3), JsonRequired] public string Bias { get; init; } = string.Empty;
    [Key(4), JsonRequired] public string PremiumMode { get; init; } = string.Empty;
    RegimeDirection[] _AllowedRegimeDirections = [];
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public trade model contract.")]
    [Key(5), JsonRequired] public RegimeDirection[] AllowedRegimeDirections { get => [.. _AllowedRegimeDirections]; init => _AllowedRegimeDirections = value is null ? [] : [.. value]; }
    TrendRegimePhase[] _AllowedTrendPhases = [];
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public trade model contract.")]
    [Key(6), JsonRequired] public TrendRegimePhase[] AllowedTrendPhases { get => [.. _AllowedTrendPhases]; init => _AllowedTrendPhases = value is null ? [] : [.. value]; }
    TrendRegimeStrength[] _AllowedTrendStrengths = [];
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public trade model contract.")]
    [Key(7), JsonRequired] public TrendRegimeStrength[] AllowedTrendStrengths { get => [.. _AllowedTrendStrengths]; init => _AllowedTrendStrengths = value is null ? [] : [.. value]; }
    MarketStructureClassification[] _AllowedStructureClassifications = [];
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public trade model contract.")]
    [Key(8), JsonRequired] public MarketStructureClassification[] AllowedStructureClassifications { get => [.. _AllowedStructureClassifications]; init => _AllowedStructureClassifications = value is null ? [] : [.. value]; }
    AssessmentCondition[] _AllowedAssessmentConditions = [];
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public trade model contract.")]
    [Key(9), JsonRequired] public AssessmentCondition[] AllowedAssessmentConditions { get => [.. _AllowedAssessmentConditions]; init => _AllowedAssessmentConditions = value is null ? [] : [.. value]; }
    AssessmentVolatility[] _AllowedVolatilityBehavior = [];
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public trade model contract.")]
    [Key(10), JsonRequired] public AssessmentVolatility[] AllowedVolatilityBehavior { get => [.. _AllowedVolatilityBehavior]; init => _AllowedVolatilityBehavior = value is null ? [] : [.. value]; }
    [Key(11), JsonRequired] public int Preference { get; init; }
}
