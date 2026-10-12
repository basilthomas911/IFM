using TomasAI.IFM.Domain.Trade.Shared;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;

[MessagePackObject]
public sealed record MarketConditionBlockingReason
{
    [Key(0)] public MarketConditionEvidenceArea Area { get; init; }
    [Key(1)] public string ReasonCode { get; init; } = string.Empty;
    [Key(2)] public string SourceId { get; init; } = string.Empty;
}
