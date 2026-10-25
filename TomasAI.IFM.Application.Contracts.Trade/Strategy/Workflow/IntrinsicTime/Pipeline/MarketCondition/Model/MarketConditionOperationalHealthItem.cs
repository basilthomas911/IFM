using TomasAI.IFM.Domain.Trade.Shared;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;

[MessagePackObject]
public sealed record MarketConditionOperationalHealthItem
{
    [Key(0)] public string SourceId { get; init; } = string.Empty;
    [Key(1)] public MarketOperationalStatus Status { get; init; }
    [Key(2)] public MarketSourceObservation Observation { get; init; } = new();
}
