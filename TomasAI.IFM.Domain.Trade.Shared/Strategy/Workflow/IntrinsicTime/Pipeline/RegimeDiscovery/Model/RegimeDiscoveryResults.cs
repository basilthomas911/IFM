using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;

/// <summary>Contains the deterministic Trend specialist result.</summary>
[MessagePackObject]
public sealed record TrendRegimeResult
{
    /// <summary>Gets whether all required Trend evidence was complete.</summary>
    [Key(0)] public bool IsComplete { get; init; }
    /// <summary>Gets the calculated trend direction.</summary>
    [Key(1)] public RegimeDirection Direction { get; init; }
    /// <summary>Gets the calculated trend strength.</summary>
    [Key(2)] public TrendRegimeStrength Strength { get; init; }
    /// <summary>Gets the calculated trend lifecycle phase.</summary>
    [Key(3)] public TrendRegimePhase Phase { get; init; }
    /// <summary>Gets the signed normalized trend score.</summary>
    [Key(4)] public decimal Score { get; init; }
    /// <summary>Gets the normalized trend confidence.</summary>
    [Key(5)] public decimal Confidence { get; init; }
    /// <summary>Gets the trend confidence band.</summary>
    [Key(6)] public RegimeConfidenceBand ConfidenceBand { get; init; }
    /// <summary>Gets normalized cross-timeframe agreement.</summary>
    [Key(7)] public decimal TimeFrameAgreement { get; init; }
    /// <summary>Gets the normalized Trend evidence in deterministic order.</summary>
    [Key(8)] public RegimeDiscoveryEvidence[] Evidence { get; init; } = [];
    /// <summary>Gets stable Trend reason codes in deterministic order.</summary>
    [Key(9)] public RegimeDiscoveryReason[] Reasons { get; init; } = [];
}

/// <summary>Contains the deterministic Volatility specialist result.</summary>
[MessagePackObject]
public sealed record VolatilityRegimeResult
{
    /// <summary>Gets whether all required Volatility evidence was complete.</summary>
    [Key(0)] public bool IsComplete { get; init; }
    /// <summary>Gets the composite volatility level.</summary>
    [Key(1)] public VolatilityRegimeLevel Level { get; init; }
    /// <summary>Gets the volatility expansion or contraction state.</summary>
    [Key(2)] public VolatilityRegimeChange Change { get; init; }
    /// <summary>Gets the VX term-structure classification.</summary>
    [Key(3)] public VxTermStructureRegime TermStructure { get; init; }
    /// <summary>Gets the normalized unsigned volatility score.</summary>
    [Key(4)] public decimal Score { get; init; }
    /// <summary>Gets the normalized Volatility confidence.</summary>
    [Key(5)] public decimal Confidence { get; init; }
    /// <summary>Gets the Volatility confidence band.</summary>
    [Key(6)] public RegimeConfidenceBand ConfidenceBand { get; init; }
    /// <summary>Gets whether Volatility recommends no new trade.</summary>
    [Key(7)] public bool NoNewTrade { get; init; }
    /// <summary>Gets normalized Volatility evidence in deterministic order.</summary>
    [Key(8)] public RegimeDiscoveryEvidence[] Evidence { get; init; } = [];
    /// <summary>Gets stable Volatility reasons in deterministic order.</summary>
    [Key(9)] public RegimeDiscoveryReason[] Reasons { get; init; } = [];
}

/// <summary>Contains the deterministic Market Structure specialist result.</summary>
[MessagePackObject]
public sealed record MarketStructureRegimeResult
{
    /// <summary>Gets whether all required Market Structure evidence was complete.</summary>
    [Key(0)] public bool IsComplete { get; init; }
    /// <summary>Gets the structure classification.</summary>
    [Key(1)] public MarketStructureClassification Classification { get; init; }
    /// <summary>Gets the optional directional structure bias.</summary>
    [Key(2)] public RegimeDirection Direction { get; init; }
    /// <summary>Gets the breakout state.</summary>
    [Key(3)] public MarketBreakoutState Breakout { get; init; }
    /// <summary>Gets the signed normalized structure score.</summary>
    [Key(4)] public decimal Score { get; init; }
    /// <summary>Gets the normalized Market Structure confidence.</summary>
    [Key(5)] public decimal Confidence { get; init; }
    /// <summary>Gets the Market Structure confidence band.</summary>
    [Key(6)] public RegimeConfidenceBand ConfidenceBand { get; init; }
    /// <summary>Gets normalized Market Structure evidence in deterministic order.</summary>
    [Key(7)] public RegimeDiscoveryEvidence[] Evidence { get; init; } = [];
    /// <summary>Gets stable Market Structure reasons in deterministic order.</summary>
    [Key(8)] public RegimeDiscoveryReason[] Reasons { get; init; } = [];
}

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
    [Key(7)] public RegimeRestriction[] Restrictions { get; init; } = [];
    /// <summary>Gets stable Fusion reasons in deterministic order.</summary>
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

/// <summary>Contains the complete versioned typed output of one Regime Discovery execution.</summary>
[MessagePackObject]
public sealed record RegimeDiscoveryResult
{
    RegimeDiscoveryDecision _decision = new();

    /// <summary>Gets the current serialized result schema version.</summary>
    public const ushort CurrentSchemaVersion = 2;
    /// <summary>Gets the serialized result schema version.</summary>
    [Key(0)] public ushort SchemaVersion { get; init; } = CurrentSchemaVersion;
    /// <summary>Gets the unique deterministic result identity.</summary>
    [Key(1)] public Guid ResultId { get; init; }
    /// <summary>Gets the owning strategy workflow execution.</summary>
    [Key(2)] public StrategyWorkflowId WorkflowId { get; init; }
    /// <summary>Gets the strategy parameter-set identity.</summary>
    [Key(3)] public Guid StrategyParameterSetId { get; init; }
    /// <summary>Gets the strategy parameter-set version.</summary>
    [Key(4)] public int StrategyParameterSetVersion { get; init; }
    /// <summary>Gets the Regime Discovery parameter-set identity.</summary>
    [Key(5)] public Guid RegimeDiscoveryParameterSetId { get; init; }
    /// <summary>Gets the Regime Discovery parameter-set version.</summary>
    [Key(6)] public int RegimeDiscoveryParameterSetVersion { get; init; }
    /// <summary>Gets the frozen signal-snapshot identity.</summary>
    [Key(7)] public Guid SignalSnapshotId { get; init; }
    /// <summary>Gets the workflow routing entity identity.</summary>
    [Key(8)] public IntrinsicTimeStrategyWorkflowEntityId EntityId { get; init; }
    /// <summary>Gets the triggering ITI event identity.</summary>
    [Key(9)] public Guid TriggerEventId { get; init; }
    /// <summary>Gets the latest market-data timestamp represented by this result.</summary>
    [Key(10)] public DateTime MarketDataAsOfUtc { get; init; }
    /// <summary>Gets the UTC result production timestamp.</summary>
    [Key(11)] public DateTime ProducedAtUtc { get; init; }
    /// <summary>Gets the single workflow target horizon.</summary>
    [Key(12)] public TimeFrameType TargetHorizon { get; init; }
    /// <summary>Gets the Trend specialist result.</summary>
    [Key(13)] public TrendRegimeResult Trend { get; init; } = new();
    /// <summary>Gets the Volatility specialist result.</summary>
    [Key(14)] public VolatilityRegimeResult Volatility { get; init; } = new();
    /// <summary>Gets the Market Structure specialist result.</summary>
    [Key(15)] public MarketStructureRegimeResult MarketStructure { get; init; } = new();
    /// <summary>Gets the final evidence-derived Regime Discovery decision.</summary>
    [Key(16)] public RegimeDiscoveryDecision Decision
    {
        get => _decision;
        init => _decision = value ?? new();
    }
    /// <summary>Gets the compatibility alias for the final decision.</summary>
    [IgnoreMember]
    [Obsolete("Use Decision. Fusion is retained as a source-compatibility alias.")]
    public RegimeDiscoveryDecision Fusion
    {
        get => _decision;
        init => _decision = value ?? new();
    }
    /// <summary>Gets supporting observation evidence in deterministic order.</summary>
    [Key(17)] public RegimeDiscoveryEvidence[] SupportingEvidence { get; init; } = [];
    /// <summary>Gets the overall quality copied from the final decision.</summary>
    [Key(18)] public RegimeOverallQuality OverallQuality { get; init; }
    /// <summary>Gets the overall confidence copied from the final decision.</summary>
    [Key(19)] public decimal OverallConfidence { get; init; }
    /// <summary>Gets all stable reasons in deterministic order.</summary>
    [Key(20)] public RegimeDiscoveryReason[] Reasons { get; init; } = [];
    /// <summary>Gets the deterministic human-readable summary.</summary>
    [Key(21)] public string SummaryText { get; init; } = string.Empty;
}
