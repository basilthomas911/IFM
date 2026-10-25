using TomasAI.IFM.Domain.Trade.Shared;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment;

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
