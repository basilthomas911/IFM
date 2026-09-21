using TomasAI.IFM.Framework.OptionPricer.Pricing;
using Calculator = TomasAI.IFM.Framework.OptionPricer.Pricing.OptionCalculator;

namespace TomasAI.IFM.Framework.OptionPricer.UnitTests;

public class UnifiedOptionCalculatorTests
{
    [Fact]
    public void WarmedFuturesPricingDoesNotAllocatePerCalculation()
    {
        var c = new Calculator(new() { Steps = 101 });
        var r = Request(UnderlyingKind.Futures, ExerciseKind.American);
        for (int i = 0; i < 32; i++) c.Price(r, .2);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 100; i++) c.Price(r, .2);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0, allocated);
    }

    public static OptionPricingRequest Request(UnderlyingKind asset = UnderlyingKind.Equity,
        ExerciseKind style = ExerciseKind.European, OptionSide side = OptionSide.Call) =>
        new(asset, style, PremiumKind.PaidUpfront, side, 100, 100, 1, .05);

    [Fact]
    public void EuropeanEquityMatchesPublishedBlackScholesExample()
    {
        var result = new Calculator().Price(Request(), .2);
        Assert.True(result.Success);
        Assert.Equal("BlackScholesMerton.Managed/v1", result.EngineVersion);
        Assert.InRange(result.Value!.Value.Price, 10.45057, 10.45060);
        Assert.InRange(result.Value.Value.Delta, .63682, .63684);
        Assert.InRange(result.Value.Value.Gamma, .01875, .01877);
        Assert.InRange(result.Value.Value.Vega, 37.523, 37.525);
        Assert.InRange(result.Value.Value.Theta, -6.415, -6.413);
        Assert.InRange(result.Value.Value.Rho, 53.231, 53.233);
    }

    [Theory]
    [InlineData(UnderlyingKind.Futures, ExerciseKind.European)]
    [InlineData(UnderlyingKind.Futures, ExerciseKind.American)]
    [InlineData(UnderlyingKind.Equity, ExerciseKind.European)]
    [InlineData(UnderlyingKind.Equity, ExerciseKind.American)]
    public void BothRightsRoundTripThroughSameModel(UnderlyingKind asset, ExerciseKind style)
    {
        var calculator = new Calculator(new() { Steps = 201 });
        foreach (var side in new[] { OptionSide.Call, OptionSide.Put })
        {
            var request = Request(asset, style, side);
            var priced = calculator.Price(request, .25);
            Assert.True(priced.Success, priced.Failure.ToString());
            var implied = calculator.ImpliedVolatility(request, priced.Value!.Value.Price);
            Assert.True(implied.Success, implied.Failure.ToString());
            Assert.InRange(implied.Value!.Value.Volatility, .249999, .250001);
            Assert.Equal(priced.EngineVersion, implied.EngineVersion);
        }
    }

    [Fact]
    public void AmericanPutMatchesIndependentReferenceAndConverges()
    {
        // Longstaff-Schwartz (2001), standard S=36,K=40,r=.06,sigma=.2,T=1 benchmark.
        var request = Request(style: ExerciseKind.American, side: OptionSide.Put)
            with { UnderlyingPrice = 36, Strike = 40, Rate = .06 };
        var a = new Calculator(new() { Steps = 801 }).Price(request, .2);
        var b = new Calculator(new() { Steps = 1601 }).Price(request, .2);
        Assert.True(a.Success);
        Assert.True(b.Success);
        Assert.InRange(a.Value!.Value.Price, 4.47, 4.50);
        Assert.InRange(Math.Abs(a.Value.Value.Price - b.Value!.Value.Price), 0, .005);
    }

    [Fact]
    public void NoDividendAmericanCallConvergesToEuropeanForPositiveRates()
    {
        var calculator = new Calculator(new() { Steps = 1601 });
        var european = calculator.Price(Request(), .2);
        var american = calculator.Price(Request(style: ExerciseKind.American), .2);
        Assert.InRange(Math.Abs(american.Value!.Value.Price - european.Value!.Value.Price), 0, .003);
    }

    [Fact]
    public void FuturesStylePremiumRemovesRateSensitivity()
    {
        var calculator = new Calculator();
        var request = Request(UnderlyingKind.Futures, ExerciseKind.American)
            with { Premium = PremiumKind.FuturesStyle };
        var a = calculator.Price(request, .2);
        var b = calculator.Price(request with { Rate = -.05 }, .2);
        Assert.Equal(a.Value, b.Value);
        Assert.Equal(0, a.Value!.Value.Rho);
    }

    [Fact]
    public void InvalidInputsAndUnsupportedConventionsHaveNoNumericalPayload()
    {
        var calculator = new Calculator();
        foreach (var r in new[] {
            Request() with { UnderlyingPrice = double.NaN },
            Request() with { UnderlyingPrice = -1 },
            Request() with { Exercise = ExerciseKind.Unknown },
            Request() with { Dividends = DividendKind.DiscreteCash, CashDividends = default },
            Request() with { Premium = PremiumKind.FuturesStyle },
            Request() with { TimeToExpiry = -1 } })
        {
            var result = calculator.Price(r, .2);
            Assert.False(result.Success);
            Assert.Null(result.Value);
        }
        Assert.Equal(PricingFailure.UndefinedGreeks, calculator.Price(Request(), 0).Failure);
        Assert.Equal(PricingFailure.UndefinedGreeks, calculator.Price(Request() with { TimeToExpiry = 0 }, .2).Failure);
        Assert.Equal(PricingFailure.PriceOutOfBounds, calculator.ImpliedVolatility(Request(), 200).Failure);
        Assert.Equal(PricingFailure.ImpliedVolatilityNotIdentifiable,
            calculator.ImpliedVolatility(Request(), 100 - 100 * Math.Exp(-.05)).Failure);
    }

    [Fact]
    public void BatchConcurrencyAndCancellationAreDeterministic()
    {
        var calculator = new Calculator(new() { Steps = 101 });
        var requests = new[] { Request(), Request(UnderlyingKind.Futures, ExerciseKind.American) };
        var results = new PricingResult[2];
        calculator.PriceBatch(requests, new[] { .2, .3 }, results);
        Assert.Equal(calculator.Price(requests[0], .2), results[0]);
        Assert.Equal(calculator.Price(requests[1], .3), results[1]);
        Parallel.For(0, 16, _ => Assert.Equal(results[1], calculator.Price(requests[1], .3)));
        Assert.Throws<OperationCanceledException>(() => calculator.Price(requests[1], .3, new CancellationToken(true)));
        Assert.Throws<ArgumentException>(() => calculator.PriceBatch(requests, new[] { .2 }, results));
    }

    [Fact]
    public void DividendYieldAndNegativeRatesPreserveEuropeanParity()
    {
        var c = new Calculator();
        var r = Request() with { Rate = -.02, Dividends = DividendKind.ContinuousYield, DividendYield = .03 };
        var call = c.Price(r, .2).Value!.Value;
        var put = c.Price(r with { Side = OptionSide.Put }, .2).Value!.Value;
        Assert.InRange(Math.Abs(call.Price - put.Price - (100 * Math.Exp(-.03) - 100 * Math.Exp(.02))), 0, 1e-7);
    }

    [Fact]
    public void PriceOnlyHandlesExpiryAndZeroVolatilityWithoutInventingGreeks()
    {
        var c = new Calculator();
        var r = Request(side: OptionSide.Put) with { UnderlyingPrice = 80 };
        Assert.Equal(20, c.TheoreticalPrice(r with { TimeToExpiry = 0 }, .2).Price);
        Assert.InRange(Math.Abs(c.TheoreticalPrice(r, 0).Price!.Value -
            (100 * Math.Exp(-.05) - 80)), 0, 1e-10);
        Assert.Equal(20, c.TheoreticalPrice(r with { Exercise = ExerciseKind.American }, 0).Price);
    }

    [Fact]
    public void EuropeanAnalyticGreeksAgreeWithIndependentPriceBumps()
    {
        var c = new Calculator();
        foreach (var kind in new[] { UnderlyingKind.Futures, UnderlyingKind.Equity })
        {
            var r = Request(kind);
            var g = c.Price(r, .2).Value!.Value;
            double P(OptionPricingRequest x, double v = .2) => c.TheoreticalPrice(x, v).Price!.Value;
            const double ds = .01, bump = .0001;
            // The inherited Black76 CDF documents 1.5e-7 absolute approximation error.
            // Differencing price amplifies that error; keep the existing kernel unchanged.
            Assert.InRange(Math.Abs(g.Delta - (P(r with { UnderlyingPrice = 100 + ds }) -
                P(r with { UnderlyingPrice = 100 - ds })) / (2 * ds)), 0, 1e-5);
            Assert.InRange(Math.Abs(g.Gamma - (P(r with { UnderlyingPrice = 100 + ds }) -
                2 * g.Price + P(r with { UnderlyingPrice = 100 - ds })) / (ds * ds)), 0, 1e-5);
            // Per-unit Vega/Rho scale the inherited CDF derivative error by the underlying.
            Assert.InRange(Math.Abs(g.Vega - (P(r, .2 + bump) - P(r, .2 - bump)) / (2 * bump)), 0, 1e-3);
            Assert.InRange(Math.Abs(g.Rho - (P(r with { Rate = .05 + bump }) -
                P(r with { Rate = .05 - bump })) / (2 * bump)), 0, 1e-3);
        }
    }

    [Fact]
    public void AmericanFuturesAtZeroRateConvergesToBlack76ForBothRights()
    {
        var c = new Calculator(new() { Steps = 1601 });
        foreach (var right in new[] { OptionSide.Call, OptionSide.Put })
        {
            var r = Request(UnderlyingKind.Futures, side: right) with { Rate = 0 };
            var european = c.Price(r, .2).Value!.Value;
            var american = c.Price(r with { Exercise = ExerciseKind.American }, .2).Value!.Value;
            Assert.InRange(Math.Abs(american.Price - european.Price), 0, .003);
            Assert.InRange(Math.Abs(american.Delta - european.Delta), 0, .001);
            Assert.InRange(Math.Abs(american.Gamma - european.Gamma), 0, .001);
        }
    }

    [Fact]
    public void SolverLimitsAndBatchesFailExplicitly()
    {
        var r = Request();
        var full = new Calculator();
        var mark = full.Price(r, .237).Value!.Value.Price;
        Assert.Equal(PricingFailure.NonConvergence,
            new Calculator(new() { MaximumIterations = 1 }).ImpliedVolatility(r, mark).Failure);
        Assert.Equal(PricingFailure.VolatilityNotBracketed,
            new Calculator(new() { MaximumVolatility = .1 }).ImpliedVolatility(r, mark).Failure);
        var batch = new PricingResult[2];
        full.ImpliedVolatilityBatch(new[] { r, r }, new[] { mark, -1d }, batch);
        Assert.True(batch[0].Success);
        Assert.Equal(PricingFailure.InvalidInput, batch[1].Failure);
    }
}
