using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;

[MessagePackObject]
public sealed record MarketConditionReadModel
{
    [Key(0)] public StrategyWorkflowId WorkflowId { get; init; }
    [Key(1)] public string WorkflowEntityId { get; init; } = string.Empty;
    [Key(2)] public long InputWorkflowRevision { get; init; }
    [Key(3)] public Guid CommandId { get; init; }
    [Key(4)] public Guid SourceEventId { get; init; }
    [Key(5)] public int FundId { get; init; }
    [Key(6)] public string InstrumentRoot { get; init; } = string.Empty;
    [Key(7)] public TimeFrameType TargetHorizon { get; init; }
    [Key(8)] public Guid ParameterSetId { get; init; }
    [Key(9)] public int ParameterSetVersion { get; init; }
    [Key(10)] public string ParameterPayloadSha256 { get; init; } = string.Empty;
    [Key(11)] public Guid SnapshotId { get; init; }
    [Key(12)] public string SnapshotSha256 { get; init; } = string.Empty;
    [Key(13)] public MarketTradeability Tradeability { get; init; }
    [Key(14)] public MarketConditionType ConditionType { get; init; }
    [Key(15)] public MarketConditionDirection Direction { get; init; }
    [Key(16)] public MarketConditionPhase Phase { get; init; }
    [Key(17)] public decimal Strength { get; init; }
    [Key(18)] public decimal Confidence { get; init; }
    [Key(19)] public string PrimaryReasonCode { get; init; } = string.Empty;
    [Key(20)] public ReadOnlyMemory<byte> ResultPayload { get; init; }
    [Key(21)] public string ResultPayloadSha256 { get; init; } = string.Empty;
    [Key(22)] public DateTime EvaluatedAtUtc { get; init; }
    [Key(23)] public DateTime ValidUntilUtc { get; init; }
    [Key(24)] public DateTime MarketDataAsOfUtc { get; init; }
    [Key(25)] public DateTime CompletedAtUtc { get; init; }
    [Key(26)] public DateTime UpdatedAtUtc { get; init; }
    [Key(27)] public MarketConditionVolatilityBehavior VolatilityBehavior { get; init; }
    [Key(28)] public MarketConditionLiquidityQuality LiquidityQuality { get; init; }
    [Key(29)] public MarketConditionDataQuality DataQuality { get; init; }
    [Key(30)] public MarketConditionUpstreamAlignment UpstreamAlignment { get; init; }
    [Key(31)] public ReadOnlyMemory<byte> EvidencePayload { get; init; }
    [Key(32)] public ReadOnlyMemory<byte> ConflictingEvidencePayload { get; init; }
    [Key(33)] public ReadOnlyMemory<byte> BlockingReasonsPayload { get; init; }
    [Key(34)] public ReadOnlyMemory<byte> ReasonsPayload { get; init; }
    [Key(35)] public string SummaryText { get; init; } = string.Empty;
}
