using FluentValidation;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.MarketCondition;

[MessagePackObject]
public sealed record MarketConditionClassificationConfiguration
{
    [Key(0)] public decimal WeakeningReversalLevel { get; init; } = 0.40m;
    [Key(1)] public decimal ExhaustingReversalLevel { get; init; } = 0.70m;
    [Key(2)] public decimal ConfirmedBandLevel { get; init; } = 1m;
    [Key(3)] public decimal HealthyLiquidityScore { get; init; } = 0.75m;
    [Key(4)] public decimal HealthyDataQualityScore { get; init; } = 0.75m;
}
