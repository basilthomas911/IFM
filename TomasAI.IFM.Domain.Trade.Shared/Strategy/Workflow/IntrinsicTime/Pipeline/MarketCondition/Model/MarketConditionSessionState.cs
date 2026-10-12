using TomasAI.IFM.Domain.Trade.Shared;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;

[MessagePackObject]
public sealed record MarketConditionSessionState
{
    [Key(0)] public MarketSessionStatus Status { get; init; }
    [Key(1)] public bool IsEntryWindow { get; init; }
    [Key(2)] public TimeSpan ExchangeLocalTime { get; init; }
    [Key(3)] public DayOfWeek ExchangeLocalWeekday { get; init; }
    [Key(4)] public MarketSourceObservation Observation { get; init; } = new();
}
