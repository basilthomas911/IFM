using TomasAI.IFM.Domain.Trade.Shared;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;

[MessagePackObject]
public sealed record MarketConditionSnapshot
{
    MarketConditionOperationalHealthItem[]? _operationalHealth = [];
    MarketSourceObservation[]? _dataQualityItems = [];

    public const ushort CurrentSchemaVersion = 1;
    [Key(0)] public Guid SnapshotId { get; init; }
    [Key(1)] public ushort SchemaVersion { get; init; } = CurrentSchemaVersion;
    [Key(2)] public StrategyWorkflowId WorkflowId { get; init; }
    [Key(3)] public IntrinsicTimeStrategyWorkflowEntityId EntityId { get; init; }
    [Key(4)] public int FundId { get; init; }
    [Key(5)] public string InstrumentRoot { get; init; } = "ES";
    [Key(6)] public TimeFrameType TargetHorizon { get; init; }
    [Key(7)] public DateTime EvaluationTimestampUtc { get; init; }
    [Key(8)] public DateTime MarketDataAsOfUtc { get; init; }
    [Key(9)] public long SourceSequenceWatermark { get; init; }
    [Key(10)] public MarketConditionFuturesQuote FuturesQuote { get; init; } = new();
    [Key(11)] public MarketConditionOptionChainQuality OptionChainQuality { get; init; } = new();
    [Key(12)] public MarketConditionSessionState SessionState { get; init; } = new();
    [Key(13)] public MarketConditionEventRiskState EventRiskState { get; init; } = new();
    [Key(14)] public MarketConditionVolatilityShockState VolatilityShockState { get; init; } = new();
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public trade model contract.")]
    [Key(15)]
    public MarketConditionOperationalHealthItem[] OperationalHealth
    {
        get => _operationalHealth is null ? null! : [.. _operationalHealth];
        init => _operationalHealth = value is null ? null : [.. value];
    }
    [Key(16)] public MarketConditionWorkflowEligibilityState WorkflowEligibility { get; init; } = new();
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public trade model contract.")]
    [Key(17)]
    public MarketSourceObservation[] DataQualityItems
    {
        get => _dataQualityItems is null ? null! : [.. _dataQualityItems];
        init => _dataQualityItems = value is null ? null : [.. value];
    }
    [Key(18)] public string SnapshotSha256 { get; init; } = string.Empty;
}
