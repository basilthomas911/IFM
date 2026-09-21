using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Framework.OptionPricer.Pricing;
using static TomasAI.IFM.Application.MarketData.UnitTests.OrderCompositionPricingPrerequisiteTests;

namespace TomasAI.IFM.Application.MarketData.UnitTests;

public class UnifiedPricingConsumerCompatibilityTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void FrozenQualifiedConsumerAndUnifiedFacadeAgreeWithoutChangingQualification(bool call)
    {
        var context = Context();
        var old = Black76PricingModel.Calculate(context, Quote("ES-future", 5000),
            Quote("ES-option-call", 100), 5000, call, At);
        Assert.Null(old.Failure);
        var r = new OptionPricingRequest(UnderlyingKind.Futures, ExerciseKind.European,
            PremiumKind.PaidUpfront, call ? OptionSide.Call : OptionSide.Put,
            5000, 5000, old.Value!.TimeToExpiry, context.Rate.AnnualContinuousRate);
        var result = new OptionCalculator().ImpliedVolatility(r, 100);
        Assert.True(result.Success);
        var ivOnly = new OptionCalculator().SolveImpliedVolatility(r, 100);
        Assert.True(ivOnly.Success);
        Assert.Equal(result.Value!.Value.Volatility, ivOnly.Value!.Value.Volatility);
        var fast = new OptionCalculator().PriceAndDelta(r, ivOnly.Value.Value.Volatility);
        Assert.True(fast.Success);
        Assert.Equal(result.Value.Value.Delta, fast.Value!.Value.Delta, 10);
        Assert.Equal(result.Value.Value.Price, fast.Value.Value.Price, 10);
        var g = result.Value!.Value;
        Assert.InRange(Math.Abs(g.Price - old.Value.TheoreticalPrice), 0, 1e-7);
        Assert.InRange(Math.Abs(g.Volatility - old.Value.ImpliedVolatility), 0, 1e-7);
        Assert.InRange(Math.Abs(g.Delta - old.Value.Delta), 0, 1e-7);
        Assert.InRange(Math.Abs(g.Gamma - old.Value.Gamma), 0, 1e-7);
        Assert.InRange(Math.Abs(g.Vega - old.Value.Vega), 0, 1e-4);
        Assert.InRange(Math.Abs(g.Theta - old.Value.Theta), 0, 1e-4);
        Assert.InRange(Math.Abs(g.Rho - old.Value.Rho), 0, 1e-4);
        Assert.Equal(64, old.Value.ContextDigest.Length);
        var stale = Black76PricingModel.Calculate(context, Quote("ES-future", 5000),
            Quote("ES-option-call", 100) with { EventAtUtc = At.AddSeconds(-2) }, 5000, call, At);
        Assert.Null(stale.Value);
        Assert.Equal("StaleData", stale.Failure!.Code);
    }
}
