using MessagePack;
using TomasAI.IFM.Framework.MarketData.Contracts.Pricing;
using TomasAI.IFM.Framework.MarketData.Pricing;

namespace TomasAI.IFM.Application.MarketData.Pricing;

/// <summary>Selection tier only. Absence of Gamma/Vega/Theta/Rho is deliberate, never represented by zeros.</summary>
[MessagePackObject]
public sealed record OptionSelectionValue(
    [property: Key(0)] double Price, [property: Key(1)] double Delta, [property: Key(2)] double ImpliedVolatility,
    [property: Key(3)] OptionPricingQuote IvUnderlying, [property: Key(4)] OptionPricingQuote IvOption,
    [property: Key(5)] DateTimeOffset IvCalculatedAtUtc, [property: Key(6)] DateTimeOffset CalculatedAtUtc,
    [property: Key(7)] DateTimeOffset ValidUntilUtc, [property: Key(8)] string PolicyVersion,
    [property: Key(9)] string ContextDigest)
{
    public static OptionSelectionValue From(ChainSelectionCalculation value, int ivAgeMilliseconds)
    {
        if (value.Failure is not null || value.TheoreticalPrice is null || value.Delta is null || value.Iv is null)
            throw new InvalidOperationException("Qualified selection calculation is required.");
        var c = value.Context;
        var until = new[] { c.ValidUntilUtc, value.Option.EventAtUtc.AddMilliseconds(c.MaximumQuoteAgeMilliseconds),
            value.Underlying.EventAtUtc.AddMilliseconds(c.MaximumQuoteAgeMilliseconds),
            value.Iv.Option.EventAtUtc.AddMilliseconds(ivAgeMilliseconds) }.Min();
        return new(value.TheoreticalPrice.Value, value.Delta.Value, value.Iv.Volatility,
            value.Iv.Underlying, value.Iv.Option, value.Iv.CalculatedAtUtc, value.CalculatedAtUtc,
            until, value.Iv.PolicyVersion, PricingSemanticHash.Compute(c));
    }
}
