using MessagePack;
using TomasAI.IFM.Shared.MarketData.Contracts.Pricing;
using TomasAI.IFM.Shared.MarketData.Pricing;
using TomasAI.IFM.Framework.MarketData.ReferenceData;
using Unified = TomasAI.IFM.Framework.OptionPricer.Pricing;

namespace TomasAI.IFM.Application.MarketData.Pricing;

/// <summary>Unscaled per-option values; composition applies signed ratios and multipliers exactly once.</summary>
[MessagePackObject]
public sealed record OptionPricingValue(
    [property: Key(0)] double ImpliedVolatility,
    [property: Key(1)] double Delta,
    [property: Key(2)] double Gamma,
    [property: Key(3)] double Theta,
    [property: Key(4)] double Vega,
    [property: Key(5)] double Rho,
    [property: Key(6)] double TheoreticalPrice,
    [property: Key(7)] double TimeToExpiry,
    [property: Key(8)] string ContextDigest);
