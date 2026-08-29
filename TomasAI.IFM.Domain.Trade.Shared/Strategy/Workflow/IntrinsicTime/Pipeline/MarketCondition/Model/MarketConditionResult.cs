using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;

[MessagePackObject]
public sealed record MarketConditionEvidenceItem
{
    [Key(0)] public MarketConditionEvidenceArea Area { get; init; }
    [Key(1)] public string FeatureCode { get; init; } = string.Empty;
    [Key(2)] public decimal ObservedValue { get; init; }
    [Key(3)] public string ObservedText { get; init; } = string.Empty;
    [Key(4)] public string Unit { get; init; } = string.Empty;
    [Key(5)] public decimal NormalizedValue { get; init; }
    [Key(6)] public decimal WeightedContribution { get; init; }
    [Key(7)] public string SourceId { get; init; } = string.Empty;
    [Key(8)] public DateTime SourceTimestampUtc { get; init; }
    [Key(9)] public long SequenceId { get; init; }
    [Key(10)] public MarketSourceAvailability Availability { get; init; }
    [Key(11)] public MarketFreshnessState Freshness { get; init; }
    [Key(12)] public string ReasonCode { get; init; } = string.Empty;
}

[MessagePackObject]
public sealed record MarketConditionBlockingReason
{
    [Key(0)] public MarketConditionEvidenceArea Area { get; init; }
    [Key(1)] public string ReasonCode { get; init; } = string.Empty;
    [Key(2)] public string SourceId { get; init; } = string.Empty;
}

/// <summary>
/// Carries a non-binding, evidence-derived hint for Trade Selection. The primary Market Condition decision remains
/// authoritative market language; downstream policy may accept, reject, rerank, or augment this hint.
/// </summary>
[MessagePackObject]
public sealed record MarketConditionOutputHint
{
    [Key(0)] public MarketConditionTradeType TradeType { get; init; }
    [Key(1)] public TimeFrameType TimeFrame { get; init; }
    [Key(2)] public MarketConditionHintSuitability Suitability { get; init; }
    [Key(3)] public decimal Confidence { get; init; }
    [Key(4)] public string ReasonCode { get; init; } = string.Empty;
    [Key(5)] public bool IsAdvisory { get; init; } = true;
}

[MessagePackObject]
public sealed record MarketConditionResult
{
    MarketConditionEvidenceItem[]? _evidenceItems = [];
    MarketConditionEvidenceItem[]? _conflictingEvidenceItems = [];
    MarketConditionBlockingReason[]? _blockingReasons = [];
    string[]? _reasons = [];
    MarketConditionOutputHint[]? _outputHints = [];

    public const ushort CurrentSchemaVersion = 2;
    [Key(0)] public ushort SchemaVersion { get; init; } = CurrentSchemaVersion;
    [Key(1)] public Guid ResultId { get; init; }
    [Key(2)] public StrategyWorkflowId WorkflowId { get; init; }
    [Key(3)] public IntrinsicTimeStrategyWorkflowEntityId EntityId { get; init; }
    [Key(4)] public int FundId { get; init; }
    [Key(5)] public string InstrumentRoot { get; init; } = "ES";
    [Key(6)] public TimeFrameType TargetHorizon { get; init; }
    [Key(7)] public Guid TriggerEventId { get; init; }
    [Key(8)] public long InputWorkflowRevision { get; init; }
    [Key(9)] public Guid StrategyParameterSetId { get; init; }
    [Key(10)] public int StrategyParameterSetVersion { get; init; }
    [Key(11)] public Guid MarketConditionParameterSetId { get; init; }
    [Key(12)] public int MarketConditionParameterSetVersion { get; init; }
    [Key(13)] public Guid SnapshotId { get; init; }
    [Key(14)] public string SnapshotSha256 { get; init; } = string.Empty;
    [Key(15)] public DateTime EvaluatedAtUtc { get; init; }
    [Key(16)] public DateTime ValidUntilUtc { get; init; }
    [Key(17)] public DateTime MarketDataAsOfUtc { get; init; }
    [Key(18)] public MarketTradeability Tradeability { get; init; }
    [Key(19)] public MarketConditionType ConditionType { get; init; }
    [Key(20)] public MarketConditionDirection Direction { get; init; }
    [Key(21)] public MarketConditionPhase Phase { get; init; }
    [Key(22)] public decimal Strength { get; init; }
    [Key(23)] public decimal Confidence { get; init; }
    [Key(24)] public MarketConditionVolatilityBehavior VolatilityBehavior { get; init; }
    [Key(25)] public MarketConditionLiquidityQuality LiquidityQuality { get; init; }
    [Key(26)] public MarketConditionDataQuality DataQuality { get; init; }
    [Key(27)] public MarketConditionUpstreamAlignment UpstreamAlignment { get; init; }
    [Key(28)] public MarketConditionEvidenceItem[] EvidenceItems
    {
        get => _evidenceItems is null ? null! : [.. _evidenceItems];
        init => _evidenceItems = value is null ? null : [.. value];
    }
    [Key(29)] public MarketConditionEvidenceItem[] ConflictingEvidenceItems
    {
        get => _conflictingEvidenceItems is null ? null! : [.. _conflictingEvidenceItems];
        init => _conflictingEvidenceItems = value is null ? null : [.. value];
    }
    [Key(30)] public MarketConditionBlockingReason[] BlockingReasons
    {
        get => _blockingReasons is null ? null! : [.. _blockingReasons];
        init => _blockingReasons = value is null ? null : [.. value];
    }
    [Key(31)] public string PrimaryReasonCode { get; init; } = string.Empty;
    [Key(32)] public string[] Reasons
    {
        get => _reasons is null ? null! : [.. _reasons];
        init => _reasons = value is null ? null : [.. value];
    }
    [Key(33)] public string SummaryText { get; init; } = string.Empty;
    /// <summary>Gets extensible, non-binding hints emitted after the primary decision is complete.</summary>
    [Key(34)] public MarketConditionOutputHint[] OutputHints
    {
        get => _outputHints is null ? [] : [.. _outputHints];
        init => _outputHints = value is null ? null : [.. value];
    }
}
