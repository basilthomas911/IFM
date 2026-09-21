using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Framework.MarketData.Contracts.LastPrice;
using TomasAI.IFM.Framework.MarketData.Contracts.Pricing;
using static TomasAI.IFM.Application.MarketData.UnitTests.OrderCompositionPricingPrerequisiteTests;

namespace TomasAI.IFM.Application.MarketData.UnitTests;

public sealed class OptionTradePricingTests
{
    static LastTradeTickSnapshot Trade(decimal price) => new("ES-option-call", new(2026, 9, 8), price, 1, 12, At, At);
    [Theory]
    [InlineData(OptionExerciseStyle.European)]
    [InlineData(OptionExerciseStyle.American)]
    public void Actual_trade_price_determines_IV_and_all_six_risk_values(OptionExerciseStyle style)
    {
        var c = ReviewedFuturesPricingRoutingTests.Reviewed(style);
        var result = OptionTradePricing.Calculate(c, Quote("ES-future", 5000), Trade(110), 5000, true, At);
        Assert.True(result.IsValid, result.PricingFailure?.Code);
        Assert.Equal(OptionGreeksPriceSource.Trade, result.PriceSource);
        Assert.Equal(110m, result.OptionMarkPrice);
        Assert.InRange(result.TheoreticalPrice!.Value, 109.99999, 110.00001);
        Assert.All(new[] { result.ImpliedVolatility, result.Delta, result.Gamma, result.Vega, result.Theta, result.Rho },
            number => Assert.True(number is { } v && double.IsFinite(v)));
        var quoteValue = Black76PricingModel.Calculate(c, Quote("ES-future", 5000), Quote("ES-option-call", 100), 5000, true, At).Value!;
        Assert.NotEqual(quoteValue.ImpliedVolatility, result.ImpliedVolatility);
        Assert.NotEqual(quoteValue.ContextDigest, result.PricingContextDigest);
    }

    [Fact]
    public void Missing_or_stale_inputs_leave_trade_identified_but_all_greeks_absent()
    {
        var c = ReviewedFuturesPricingRoutingTests.Reviewed(OptionExerciseStyle.European);
        foreach (var underlying in new[] { null, Quote("ES-future", 5000) with { EventAtUtc = At.AddSeconds(-2) } })
        {
            var result = OptionTradePricing.Calculate(c, underlying, Trade(110), 5000, true, At);
            Assert.False(result.IsValid);
            Assert.Equal(OptionGreeksPriceSource.Trade, result.PriceSource);
            Assert.Equal(12, result.OptionPriceSourceSequence);
            Assert.NotNull(result.PricingFailure);
            Assert.Null(result.ImpliedVolatility); Assert.Null(result.Delta); Assert.Null(result.Gamma);
        }
    }
}
