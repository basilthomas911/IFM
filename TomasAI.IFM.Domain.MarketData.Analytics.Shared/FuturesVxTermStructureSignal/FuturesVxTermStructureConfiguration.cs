using FluentValidation;
using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesVxTermStructureSignal;

/// <summary>Defines versioned calculation and freshness rules for a VX curve.</summary>
[MessagePackObject]
public sealed record FuturesVxTermStructureConfiguration
{
    /// <summary>Gets the default configuration.</summary>
    public static FuturesVxTermStructureConfiguration Standard { get; } = new();
    /// <summary>Gets the stable calculation configuration identity.</summary>
    [Key(0)] public string ConfigurationId { get; init; } = "vx-front-back-v1";
    /// <summary>Gets the absolute term-structure percentage treated as flat.</summary>
    [Key(1)] public decimal FlatEpsilon { get; init; } = 0.0005m;
    /// <summary>Gets the maximum permitted source timestamp difference.</summary>
    [Key(2)] public TimeSpan MaximumSourceSkew { get; init; } = TimeSpan.FromSeconds(5);
}
