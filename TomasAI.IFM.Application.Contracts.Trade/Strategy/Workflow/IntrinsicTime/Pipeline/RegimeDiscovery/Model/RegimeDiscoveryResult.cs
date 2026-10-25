using TomasAI.IFM.Domain.Trade.Shared;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;

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
    [Key(16)]
    public RegimeDiscoveryDecision Decision
    {
        get => _decision;
        init => _decision = value ?? new();
    }
    /// <summary>Gets the compatibility alias for the final decision.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S1133", Justification = "Retain historical serialized model members for existing event-log deserialization.")]
    [IgnoreMember]
    [Obsolete("Use Decision. Fusion is retained as a source-compatibility alias.")]
    public RegimeDiscoveryDecision Fusion
    {
        get => _decision;
        init => _decision = value ?? new();
    }
    /// <summary>Gets supporting observation evidence in deterministic order.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public trade model contract.")]
    [Key(17)] public RegimeDiscoveryEvidence[] SupportingEvidence { get; init; } = [];
    /// <summary>Gets the overall quality copied from the final decision.</summary>
    [Key(18)] public RegimeOverallQuality OverallQuality { get; init; }
    /// <summary>Gets the overall confidence copied from the final decision.</summary>
    [Key(19)] public decimal OverallConfidence { get; init; }
    /// <summary>Gets all stable reasons in deterministic order.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public trade model contract.")]
    [Key(20)] public RegimeDiscoveryReason[] Reasons { get; init; } = [];
    /// <summary>Gets the deterministic human-readable summary.</summary>
    [Key(21)] public string SummaryText { get; init; } = string.Empty;
}
