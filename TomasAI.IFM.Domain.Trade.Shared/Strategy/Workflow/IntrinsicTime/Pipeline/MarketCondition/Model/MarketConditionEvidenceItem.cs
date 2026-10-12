using TomasAI.IFM.Domain.Trade.Shared;
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
