using TomasAI.IFM.Domain.Trade.Shared;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;

[MessagePackObject]
public sealed record MarketConditionWorkflowEligibilityState
{
    [Key(0)] public bool EntriesEnabled { get; init; } = true;
    [Key(1)] public DateTime RegimeProducedAtUtc { get; init; }
    [Key(2)] public DateTime TriggerProducedAtUtc { get; init; }
}
