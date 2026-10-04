using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.OptionPricer.Pricing;

namespace TomasAI.IFM.Application.Storage.MarketDataDb;

/// <summary>Calculates missing quote Greeks before persistence, using the quote's recorded pricing inputs.</summary>
internal static class OptionQuoteGreekEnrichment
{
    internal static FuturesOptionTickDataV2ReadModel Calculate(FuturesOptionContractReadModel definition,
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
            definition.OptionType.StartsWith("C", StringComparison.OrdinalIgnoreCase) ? OptionSide.Call : OptionSide.Put,
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
