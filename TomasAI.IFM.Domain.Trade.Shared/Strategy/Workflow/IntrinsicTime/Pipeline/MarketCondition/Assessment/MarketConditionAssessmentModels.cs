using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment;

public enum AssessmentAvailability : byte { Undefined = 0, Available = 1, Unavailable = 2 }
public enum AssessmentCondition : byte { Undefined = 0, Directional = 1, RangeBound = 2, Transition = 3, VolatilityExpansion = 4, VolatilityContraction = 5, Dislocated = 6, Unclassified = 7 }
public enum AssessmentLiquidity : byte { Unknown = 0, Healthy = 1, Degraded = 2, Poor = 3 }
public enum AssessmentStress : byte { Unknown = 0, Normal = 1, Elevated = 2 }
public enum AssessmentEventContext : byte { Unknown = 0, Clear = 1, Elevated = 2 }
public enum AssessmentTriggerAlignment : byte { Unknown = 0, Aligned = 1, Conflicted = 2, Neutral = 3, NotApplicable = 4 }
public enum AssessmentVolatility : byte { Unknown = 0, Stable = 1, Expanding = 2, Contracting = 3, Shock = 4 }

[MessagePackObject]
public sealed record AssessmentObservation
{
    [Key(0)] public string SourceId { get; init; } = string.Empty;
    [Key(1)] public DateTime ObservedAtUtc { get; init; }
    [Key(2)] public DateTime ReceivedAtUtc { get; init; }
    [Key(3)] public long Sequence { get; init; }
    [Key(4)] public MarketSourceAvailability Availability { get; init; }
    [Key(5)] public MarketSourceValidity Validity { get; init; }
    [Key(6)] public decimal? Value { get; init; }
    [Key(7)] public string Unit { get; init; } = string.Empty;
    [Key(8)] public string Reason { get; init; } = string.Empty;
}

[MessagePackObject]
public sealed record AssessmentReferenceQuote(
    [property: Key(0)] decimal Bid, [property: Key(1)] decimal Ask,
    [property: Key(2)] decimal BidSize, [property: Key(3)] decimal AskSize);

[MessagePackObject]
public sealed record MarketConditionAssessmentSnapshot
{
    AssessmentObservation[] _observations = [];
    [Key(0)] public short SchemaVersion { get; init; } = 1;
    [Key(1)] public Guid SnapshotId { get; init; }
    [Key(2)] public string MarketProfileId { get; init; } = string.Empty;
    [Key(3)] public string InstrumentRoot { get; init; } = string.Empty;
    [Key(4)] public TimeFrameType TargetHorizon { get; init; }
    [Key(5)] public string ReferenceInstrumentId { get; init; } = string.Empty;
    [Key(6)] public DateTime EvaluatedAtUtc { get; init; }
    [Key(7)] public AssessmentReferenceQuote? Quote { get; init; }
    [Key(8)] public MarketSessionStatus SessionState { get; init; }
    [Key(9)] public AssessmentEventContext EventContext { get; init; }
    [Key(10)] public AssessmentObservation[] Observations { get => [.. _observations]; init => _observations = value is null ? [] : [.. value.OrderBy(x => x.SourceId, StringComparer.Ordinal)]; }
    [Key(11)] public MarketConditionCalendarDownloadEvidence? CalendarEvidence { get; init; }
    [Key(12)] public string PayloadSha256 { get; init; } = string.Empty;
    public string ComputeHash() => MarketConditionAssessmentHash.Compute(this with { PayloadSha256 = string.Empty, Observations = Observations });
    public MarketConditionAssessmentSnapshot Seal() => this with { PayloadSha256 = ComputeHash() };
}

[MessagePackObject]
public sealed record AssessmentEvidence(
    [property: Key(0)] TimeFrameType Horizon,
    [property: Key(1)] string SourceId,
    [property: Key(2)] string Feature,
    [property: Key(3)] decimal? Value,
    [property: Key(4)] string Unit,
    [property: Key(5)] DateTime ObservedAtUtc,
    [property: Key(6)] decimal AgeSeconds,
    [property: Key(7)] MarketSourceAvailability Availability,
    [property: Key(8)] string Reason,
    [property: Key(9)] long Sequence = 0);

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
    [Key(17)] public AssessmentEvidence[] EvidenceItems { get => [.. _evidence]; init => _evidence = value is null ? [] : [.. value]; }
    [Key(18)] public AssessmentEvidence[] ConflictingEvidenceItems { get => [.. _conflicts]; init => _conflicts = value is null ? [] : [.. value]; }
    [Key(19)] public string[] LimitationReasons { get => [.. _limitations]; init => _limitations = value is null ? [] : [.. value]; }
    [Key(20)] public RegimeRestriction[] InheritedRestrictions { get => [.. _restrictions]; init => _restrictions = value is null ? [] : [.. value]; }
    [Key(21)] public string SummaryText { get; init; } = string.Empty;
    static RegimeDiscoveryDecision? Copy(RegimeDiscoveryDecision? value) => value is null ? null : value with
    { Restrictions = [.. value.Restrictions], Reasons = [.. value.Reasons] };
}

[MessagePackObject]
public sealed record MarketConditionAssessmentResult
{
    [Key(0)] public short SchemaVersion { get; init; } = 1;
    [Key(1)] public Guid ResultId { get; init; }
    [Key(2)] public StrategyWorkflowId WorkflowId { get; init; }
    [Key(3)] public IntrinsicTimeStrategyWorkflowEntityId EntityId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public long InputWorkflowRevision { get; init; }
    [Key(6)] public string MarketProfileId { get; init; } = string.Empty;
    [Key(7)] public string InstrumentRoot { get; init; } = string.Empty;
    [Key(8)] public Guid ParameterSetId { get; init; }
    [Key(9)] public int ParameterSetVersion { get; init; }
    [Key(10)] public string ParameterPayloadSha256 { get; init; } = string.Empty;
    [Key(11)] public Guid RegimeResultId { get; init; }
    [Key(12)] public string RegimePayloadSha256 { get; init; } = string.Empty;
    [Key(13)] public Guid SnapshotId { get; init; }
    [Key(14)] public string SnapshotSha256 { get; init; } = string.Empty;
    [Key(15)] public DateTime EvaluatedAtUtc { get; init; }
    [Key(16)] public TimeFrameType TargetHorizon { get; init; }
    [Key(17)] public HorizonAssessment Assessment { get; init; } = new();
    [Key(18)] public string SummaryText { get; init; } = string.Empty;
    [Key(19)] public MarketConditionCalendarDownloadEvidence? CalendarEvidence { get; init; }
}
