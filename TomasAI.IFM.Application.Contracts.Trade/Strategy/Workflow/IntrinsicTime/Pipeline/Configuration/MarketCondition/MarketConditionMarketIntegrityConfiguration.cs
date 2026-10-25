using FluentValidation;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.MarketCondition;

[MessagePackObject]
public sealed record MarketConditionMarketIntegrityConfiguration
{
    [Key(0)] public decimal MaximumOneMinuteMoveAtr { get; init; } = 1.50m;
    [Key(1)] public decimal MaximumFiveMinuteVolatilityIncrease { get; init; } = 0.15m;
    [Key(2)] public bool PermitCrossedMarket { get; init; }
    [Key(3)] public bool RequirePositiveTwoSidedQuote { get; init; } = true;
}
