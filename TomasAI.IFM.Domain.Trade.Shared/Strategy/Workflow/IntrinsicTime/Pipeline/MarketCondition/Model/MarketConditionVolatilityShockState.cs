using TomasAI.IFM.Domain.Trade.Shared;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;

[MessagePackObject]
public sealed record MarketConditionVolatilityShockState
{
    [Key(0)] public decimal FiveMinuteRelativeIncrease { get; init; }
    [Key(1)] public MarketSourceObservation Observation { get; init; } = new();
}
