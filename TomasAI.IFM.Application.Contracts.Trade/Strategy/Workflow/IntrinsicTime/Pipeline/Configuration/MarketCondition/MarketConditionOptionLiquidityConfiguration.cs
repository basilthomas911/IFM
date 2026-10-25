using FluentValidation;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.MarketCondition;

[MessagePackObject]
public sealed record MarketConditionOptionLiquidityConfiguration
{
    [Key(0)] public int MinimumDte { get; init; } = 1;
    [Key(1)] public int MaximumDte { get; init; } = 14;
    [Key(2)] public decimal MaximumAbsoluteMoneyness { get; init; } = 0.05m;
    [Key(3)] public bool RequireCallsAndPuts { get; init; } = true;
    [Key(4)] public int MinimumEligibleExpirations { get; init; } = 1;
    [Key(5)] public int MinimumCandidateContracts { get; init; } = 12;
    [Key(6)] public decimal MinimumValidQuoteCoverage { get; init; } = 0.80m;
    [Key(7)] public decimal HealthyValidQuoteCoverage { get; init; } = 0.90m;
    [Key(8)] public decimal MaximumMedianRelativeSpread { get; init; } = 0.20m;
    [Key(9)] public decimal MaximumP90RelativeSpread { get; init; } = 0.35m;
    [Key(10)] public decimal MinimumMedianBidSize { get; init; } = 1m;
    [Key(11)] public decimal MinimumMedianAskSize { get; init; } = 1m;
    [Key(12)] public decimal MaximumUnderlyingMismatch { get; init; } = 0.0025m;
}
