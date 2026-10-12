using TomasAI.IFM.Domain.Trade.Shared;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment;

[MessagePackObject]
public sealed record HorizonAssessment
{
    AssessmentEvidence[] _evidence = [], _conflicts = [];
    string[] _limitations = [];
    RegimeRestriction[] _restrictions = [];
    RegimeDiscoveryDecision? _upstreamContext;
    [Key(0)] public short SchemaVersion { get; init; } = 1;
    [Key(1)] public TimeFrameType Horizon { get; init; }
    [Key(2)] public AssessmentAvailability Availability { get; init; }
    [Key(3)] public Guid RegimeResultId { get; init; }
    [Key(4)] public string RegimePayloadSha256 { get; init; } = string.Empty;
    [Key(5)] public RegimeDiscoveryDecision? UpstreamContext { get => Copy(_upstreamContext); init => _upstreamContext = Copy(value); }
    [Key(6)] public AssessmentCondition? ConditionType { get; init; }
    [Key(7)] public AssessmentVolatility VolatilityBehavior { get; init; }
    [Key(8)] public AssessmentLiquidity LiquidityCondition { get; init; }
    [Key(9)] public MarketSessionStatus SessionState { get; init; }
    [Key(10)] public AssessmentEventContext EventRiskState { get; init; }
    [Key(11)] public AssessmentStress StressState { get; init; }
    [Key(12)] public AssessmentTriggerAlignment TriggerAlignment { get; init; }
    [Key(13)] public decimal? AssessmentConfidence { get; init; }
    [Key(14)] public MarketConditionDataQuality DataQuality { get; init; }
    [Key(15)] public DateTime EvaluatedAtUtc { get; init; }
    [Key(16)] public DateTime? ValidUntilUtc { get; init; }
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public trade model contract.")]
    [Key(17)] public AssessmentEvidence[] EvidenceItems { get => [.. _evidence]; init => _evidence = value is null ? [] : [.. value]; }
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public trade model contract.")]
    [Key(18)] public AssessmentEvidence[] ConflictingEvidenceItems { get => [.. _conflicts]; init => _conflicts = value is null ? [] : [.. value]; }
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public trade model contract.")]
    [Key(19)] public string[] LimitationReasons { get => [.. _limitations]; init => _limitations = value is null ? [] : [.. value]; }
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public trade model contract.")]
    [Key(20)] public RegimeRestriction[] InheritedRestrictions { get => [.. _restrictions]; init => _restrictions = value is null ? [] : [.. value]; }
    [Key(21)] public string SummaryText { get; init; } = string.Empty;
    static RegimeDiscoveryDecision? Copy(RegimeDiscoveryDecision? value) => value is null ? null : value with
    { Restrictions = [.. value.Restrictions], Reasons = [.. value.Reasons] };
}
