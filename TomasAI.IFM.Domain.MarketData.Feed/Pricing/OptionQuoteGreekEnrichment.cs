using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.OptionPricer.Pricing;

namespace TomasAI.IFM.Domain.MarketData.Feed.Pricing;

/// <summary>Calculates missing quote Greeks before persistence, using the quote's recorded pricing inputs.</summary>
public static class OptionQuoteGreekEnrichment
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S1244", Justification = "Zero is the explicit missing-value sentinel in the persisted pricing contract; approximate comparison would classify valid nonzero values as missing.")]
    /// <summary>Enriches missing Greeks using the recorded contract and quote pricing inputs.</summary>
    /// <param name="definition">The option contract definition.</param>
    /// <param name="quote">The observed quote to enrich.</param>
    /// <returns>The enriched quote, or the original quote when pricing inputs are unavailable.</returns>
    public static FuturesOptionTickDataV2ReadModel Calculate(FuturesOptionContractReadModel definition,
        FuturesOptionTickDataV2ReadModel quote)
    {
        if (quote.Vega != 0 || quote.UnderlyingPrice <= 0 || quote.BidPrice <= 0 || quote.AskPrice < quote.BidPrice
            || definition.ExpirationUtc is not { } expiry || quote.TickId <= 100000000000000000) return quote;
        var observedAt = DateTimeOffset.UnixEpoch.AddTicks(quote.TickId / 100);
        var years = (expiry - observedAt).TotalDays / 365.25;
        if (years <= 0) return quote;
        var calculator = new OptionCalculator();
        var request = new OptionPricingRequest(UnderlyingKind.Futures,
            definition.ExerciseStyle == ReferenceExerciseStyle.American ? ExerciseKind.American : ExerciseKind.European,
            definition.PremiumStyle == ReferencePremiumStyle.FuturesStyleVariation ? PremiumKind.FuturesStyle : PremiumKind.PaidUpfront,
            definition.OptionType.Length > 0 && definition.OptionType[0] is 'C' or 'c' ? OptionSide.Call : OptionSide.Put,
            quote.UnderlyingPrice, (double)definition.GetExactStrikePrice(), years, 0);
        var result = quote.ImpliedVolatility is > 0 and < 4
            ? calculator.Price(request, quote.ImpliedVolatility)
            : calculator.ImpliedVolatility(request, (quote.BidPrice + quote.AskPrice) / 2);
        return result.Success && result.Value is { } value
            ? quote with { ImpliedVolatility = value.Volatility, Delta = value.Delta, Gamma = value.Gamma,
                Vega = value.Vega, Theta = value.Theta, Rho = value.Rho }
            : quote;
    }
}
