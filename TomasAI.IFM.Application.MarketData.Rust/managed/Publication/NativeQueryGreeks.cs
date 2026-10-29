using MessagePack;
using TomasAI.IFM.Shared.MarketData.Contracts.LastPrice;
using TomasAI.IFM.Shared.MarketData.Contracts.Pricing;
namespace TomasAI.IFM.Application.MarketData.Rust.Publication;

/// <summary>Copied native Greek projection. Conversion performs no pricing,
/// qualification, rate lookup, midpoint arithmetic or resource orchestration.</summary>
[MessagePackObject]
public sealed record NativeQueryGreeks(
    [property: Key(0)] bool IsValid,
    [property: Key(1)] bool IsStale,
    [property: Key(2)] int FailureReason,
    [property: Key(3)] int PriceSource,
    [property: Key(4)] string FuturesContractId,
    [property: Key(5)] decimal? FuturesPrice,
    [property: Key(6)] decimal? OptionMarkPrice,
    [property: Key(7)] double? RiskFreeRate,
    [property: Key(8)] double? TimeToExpiryYears,
    [property: Key(9)] double? ImpliedVolatility,
    [property: Key(10)] double? TheoreticalPrice,
    [property: Key(11)] double? Delta,
    [property: Key(12)] double? Gamma,
    [property: Key(13)] double? Vega,
    [property: Key(14)] double? Theta,
    [property: Key(15)] double? Rho,
    [property: Key(16)] int SolverIterations,
    [property: Key(17)] long FuturesPriceSourceSequence,
    [property: Key(18)] long OptionPriceSourceSequence,
    [property: Key(19)] DateTimeOffset FuturesPriceTimestamp,
    [property: Key(20)] DateTimeOffset OptionPriceTimestamp,
    [property: Key(21)] DateTimeOffset CalculatedAtUtc,
    [property: Key(22)] OptionPricingFailure? PricingFailure,
    [property: Key(23)] string? PricingContextDigest)
{
    /// <summary>Materializes the existing immutable public value type.</summary>
    public OptionGreeksSnapshot ToSnapshot() => new(
IsValid, IsStale, (OptionGreeksFailureReason)FailureReason, (OptionGreeksPriceSource)PriceSource, FuturesContractId, FuturesPrice, OptionMarkPrice, RiskFreeRate, TimeToExpiryYears, ImpliedVolatility, TheoreticalPrice, Delta, Gamma, Vega, Theta, Rho, SolverIterations, FuturesPriceSourceSequence, OptionPriceSourceSequence, FuturesPriceTimestamp, OptionPriceTimestamp, CalculatedAtUtc)
        { PricingFailure = PricingFailure, PricingContextDigest = PricingContextDigest };
    /// <summary>Qualification fixture projection from the current C# result.</summary>
    public static NativeQueryGreeks FromSnapshot(OptionGreeksSnapshot value) => new(
value.IsValid, value.IsStale, (int)value.FailureReason, (int)value.PriceSource, value.FuturesContractId, value.FuturesPrice, value.OptionMarkPrice, value.RiskFreeRate, value.TimeToExpiryYears, value.ImpliedVolatility, value.TheoreticalPrice, value.Delta, value.Gamma, value.Vega, value.Theta, value.Rho, value.SolverIterations, value.FuturesPriceSourceSequence, value.OptionPriceSourceSequence, value.FuturesPriceTimestamp, value.OptionPriceTimestamp, value.CalculatedAtUtc, value.PricingFailure, value.PricingContextDigest);
}

/// <summary>One quote and its original frozen native calculation identity.</summary>
[MessagePackObject]
public sealed record NativeQuoteGreeksResult(
    [property: Key(0)] OptionPricingQuote Quote,
    [property: Key(1)] NativeQueryGreeks Greeks,
    [property: Key(2)] Guid PhysicalGeneration);

/// <summary>One durably acknowledged native trade and its original calculation.</summary>
[MessagePackObject]
public sealed record NativeTradeGreeksResult(
    [property:Key(0)] NativeRawTrade Trade,
    [property:Key(1)] NativeQueryGreeks Greeks,
    [property:Key(2)] Guid PhysicalGeneration);

/// <summary>One raw provider quote with its exact native calculation. Rust
/// joins local qualification time separately from provider receipt time.</summary>
[MessagePackObject]
public sealed record NativeCanonicalQuoteRisk([property:Key(0)] NativeRawQuote Quote,
    [property:Key(1)] NativeQueryGreeks Greeks,[property:Key(2)] Guid PhysicalGeneration);
