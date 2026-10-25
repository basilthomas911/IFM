using TomasAI.IFM.Domain.Trade.Shared;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;

[MessagePackObject]
public sealed record MarketConditionSnapshotCaptureResult
{
    [Key(0)] public MarketConditionCaptureOutcome Outcome { get; init; }
    [Key(1)] public MarketConditionSnapshot Snapshot { get; init; } = new();
    [Key(2)] public MarketConditionFailureCategory FailureCategory { get; init; }
    [Key(3)] public string ReasonCode { get; init; } = string.Empty;
    [Key(4)] public string SafeMessage { get; init; } = string.Empty;
}
