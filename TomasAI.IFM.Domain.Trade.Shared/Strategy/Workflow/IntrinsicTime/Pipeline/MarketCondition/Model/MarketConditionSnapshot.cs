using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;

[MessagePackObject]
public sealed record MarketSourceObservation
{
    [Key(0)] public string SourceId { get; init; } = string.Empty;
    [Key(1)] public DateTime SourceTimestampUtc { get; init; }
    [Key(2)] public DateTime ReceivedAtUtc { get; init; }
    [Key(3)] public long SequenceId { get; init; }
    [Key(4)] public MarketSourceAvailability Availability { get; init; }
    [Key(5)] public MarketSourceValidity Validity { get; init; }
    [Key(6)] public decimal AgeSeconds { get; init; }
}

[MessagePackObject]
public sealed record MarketConditionFuturesQuote
{
    [Key(0)] public decimal BidPrice { get; init; }
    [Key(1)] public decimal AskPrice { get; init; }
    [Key(2)] public decimal BidSize { get; init; }
    [Key(3)] public decimal AskSize { get; init; }
    [Key(4)] public decimal LastPrice { get; init; }
    [Key(5)] public decimal OneMinuteMoveAtr { get; init; }
    [Key(6)] public MarketSourceObservation QuoteObservation { get; init; } = new();
    [Key(7)] public MarketSourceObservation TradeObservation { get; init; } = new();
}

[MessagePackObject]
public sealed record MarketConditionOptionChainQuality
{
    [Key(0)] public int CandidateContractCount { get; init; }
    [Key(1)] public int ValidQuoteCount { get; init; }
    [Key(2)] public int EligibleExpirationCount { get; init; }
    [Key(3)] public bool HasCalls { get; init; }
    [Key(4)] public bool HasPuts { get; init; }
    [Key(5)] public decimal ValidQuoteCoverage { get; init; }
    [Key(6)] public decimal MedianRelativeSpread { get; init; }
    [Key(7)] public decimal P90RelativeSpread { get; init; }
    [Key(8)] public decimal MedianBidSize { get; init; }
    [Key(9)] public decimal MedianAskSize { get; init; }
    [Key(10)] public decimal UnderlyingMismatch { get; init; }
    [Key(11)] public MarketSourceObservation Observation { get; init; } = new();
}

[MessagePackObject]
public sealed record MarketConditionSessionState
{
    [Key(0)] public MarketSessionStatus Status { get; init; }
    [Key(1)] public bool IsEntryWindow { get; init; }
    [Key(2)] public TimeSpan ExchangeLocalTime { get; init; }
    [Key(3)] public DayOfWeek ExchangeLocalWeekday { get; init; }
    [Key(4)] public MarketSourceObservation Observation { get; init; } = new();
}

[MessagePackObject]
public sealed record MarketConditionEventRiskState
{
    [Key(0)] public MarketEventRiskStatus Status { get; init; }
    [Key(1)] public string EventId { get; init; } = string.Empty;
    [Key(2)] public string Category { get; init; } = string.Empty;
    [Key(3)] public MarketSourceObservation Observation { get; init; } = new();
}

[MessagePackObject]
public sealed record MarketConditionVolatilityShockState
{
    [Key(0)] public decimal FiveMinuteRelativeIncrease { get; init; }
    [Key(1)] public MarketSourceObservation Observation { get; init; } = new();
}

[MessagePackObject]
public sealed record MarketConditionOperationalHealthItem
{
    [Key(0)] public string SourceId { get; init; } = string.Empty;
    [Key(1)] public MarketOperationalStatus Status { get; init; }
    [Key(2)] public MarketSourceObservation Observation { get; init; } = new();
}

[MessagePackObject]
public sealed record MarketConditionWorkflowEligibilityState
{
    [Key(0)] public bool EntriesEnabled { get; init; } = true;
    [Key(1)] public DateTime RegimeProducedAtUtc { get; init; }
    [Key(2)] public DateTime TriggerProducedAtUtc { get; init; }
}

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
    [Key(15)] public MarketConditionOperationalHealthItem[] OperationalHealth
    {
        get => _operationalHealth is null ? null! : [.. _operationalHealth];
        init => _operationalHealth = value is null ? null : [.. value];
    }
    [Key(16)] public MarketConditionWorkflowEligibilityState WorkflowEligibility { get; init; } = new();
    [Key(17)] public MarketSourceObservation[] DataQualityItems
    {
        get => _dataQualityItems is null ? null! : [.. _dataQualityItems];
        init => _dataQualityItems = value is null ? null : [.. value];
    }
    [Key(18)] public string SnapshotSha256 { get; init; } = string.Empty;
}

public enum MarketConditionCaptureOutcome : byte { Undefined = 0, Success = 1, KnownBlocked = 2, Failed = 3 }

[MessagePackObject]
public sealed record MarketConditionSnapshotCaptureResult
{
    [Key(0)] public MarketConditionCaptureOutcome Outcome { get; init; }
    [Key(1)] public MarketConditionSnapshot Snapshot { get; init; } = new();
    [Key(2)] public MarketConditionFailureCategory FailureCategory { get; init; }
    [Key(3)] public string ReasonCode { get; init; } = string.Empty;
    [Key(4)] public string SafeMessage { get; init; } = string.Empty;
}
