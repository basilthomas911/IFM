using FluentValidation;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.MarketCondition;

[MessagePackObject]
public sealed record MarketConditionFuturesLiquidityConfiguration
{
    [Key(0)] public decimal TickSize { get; init; } = 0.25m;
    [Key(1)] public decimal HealthySpreadTicks { get; init; } = 1m;
    [Key(2)] public decimal MaximumTradeableSpreadTicks { get; init; } = 2m;
    [Key(3)] public decimal MinimumBidSize { get; init; } = 5m;
    [Key(4)] public decimal MinimumAskSize { get; init; } = 5m;
    [Key(5)] public decimal HealthyBestSideSize { get; init; } = 10m;
}
