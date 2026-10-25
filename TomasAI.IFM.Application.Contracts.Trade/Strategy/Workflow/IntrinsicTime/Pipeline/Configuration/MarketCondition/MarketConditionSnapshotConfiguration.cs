using FluentValidation;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.MarketCondition;

[MessagePackObject]
public sealed record MarketConditionSnapshotConfiguration
{
    [Key(0)] public int FutureClockSkewSeconds { get; init; } = 2;
    [Key(1)] public int SnapshotCaptureAttempts { get; init; } = 3;
    [Key(2)] public int FuturesQuoteMaximumAgeSeconds { get; init; } = 2;
    [Key(3)] public int FuturesTradeMaximumAgeSeconds { get; init; } = 5;
    [Key(4)] public int OptionQuoteMaximumAgeSeconds { get; init; } = 5;
    [Key(5)] public int OptionChainMaximumAgeSeconds { get; init; } = 10;
    [Key(6)] public int VolatilityMaximumAgeSeconds { get; init; } = 15;
    [Key(7)] public int SessionMaximumAgeSeconds { get; init; } = 60;
    [Key(8)] public int HealthMaximumAgeSeconds { get; init; } = 15;
    [Key(9)] public int EventRiskMaximumAgeSeconds { get; init; } = 900;
}
