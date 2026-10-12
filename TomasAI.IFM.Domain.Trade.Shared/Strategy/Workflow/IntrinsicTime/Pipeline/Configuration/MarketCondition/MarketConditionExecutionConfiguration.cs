using FluentValidation;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.MarketCondition;

[MessagePackObject]
public sealed record MarketConditionExecutionConfiguration
{
    [Key(0)] public int MaximumExecutionMilliseconds { get; init; } = 5000;
    [Key(1)] public int TransportReplyGraceMilliseconds { get; init; } = 5000;
    [Key(2)] public int ResultLifetimeSeconds { get; init; } = 30;
}
