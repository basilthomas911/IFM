using FluentValidation;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery;

/// <summary>Configures freshness validation for one immutable signal snapshot.</summary>
[MessagePackObject]
public sealed record RegimeDiscoveryFreshnessConfiguration
{
    /// <summary>Gets the maximum tolerated future clock skew in seconds.</summary>
    [Key(0)] public int FutureClockSkewSeconds { get; init; } = 5;
}
