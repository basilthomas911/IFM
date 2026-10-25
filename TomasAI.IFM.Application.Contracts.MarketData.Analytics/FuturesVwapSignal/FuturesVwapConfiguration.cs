using FluentValidation;
using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesVwapSignal;

/// <summary>Defines versioned eligibility and current-contract routing for session VWAP.</summary>
[MessagePackObject]
public sealed record FuturesVwapConfiguration
{
    /// <summary>Gets the standard ES futures-session configuration.</summary>
    public static FuturesVwapConfiguration Standard { get; } = new();
    /// <summary>Gets the stable configuration identity.</summary>
    [Key(0)] public string ConfigurationId { get; init; } = "futures-vwap-exact-v1";
    /// <summary>Gets the futures root whose current contract is observed.</summary>
    [Key(1)] public string RootSymbol { get; init; } = "ES";
}
