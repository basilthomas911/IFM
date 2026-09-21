using TomasAI.IFM.Framework.OptionPricer.Pricing;
using Calculator = TomasAI.IFM.Framework.OptionPricer.Pricing.OptionCalculator;

namespace TomasAI.IFM.Framework.OptionPricer.UnitTests;

public class CashDividendPricingTests(Xunit.Abstractions.ITestOutputHelper output)
{
    [Fact]
    public void ThetaIsStableAcrossSegmentCountThresholdsAndDefaultIvBrackets()
    {
        var r = Request();
        var a = new Calculator(new() { Steps = 800 }).Price(r, .2);
        var b = new Calculator(new() { Steps = 801 }).Price(r, .2);
        Assert.True(a.Success && b.Success);
        Assert.InRange(Math.Abs(a.Value!.Value.Theta - b.Value!.Value.Theta), 0, .02);
        Assert.InRange(a.Value.Value.Theta, -8, -2);
        var c = new Calculator(new() { Steps = 100, SpatialSteps = 200 });
        var mark = c.Price(r, .3).Value!.Value.Price;
        var iv = c.ImpliedVolatility(r, mark);
        Assert.True(iv.Success, iv.Failure.ToString());
        Assert.InRange(iv.Value!.Value.Volatility, .299999, .300001);
        Assert.Equal(PricingFailure.NumericalFailure, c.Price(r, .6).Failure);
        Assert.Equal(PricingFailure.NumericalFailure,
            new Calculator(new() { SpatialSteps = 64 }).Price(r with { UnderlyingPrice = 5 }, .2).Failure);
    }

    private static OptionPricingRequest Request(ExerciseKind style = ExerciseKind.European,
        OptionSide side = OptionSide.Call) => new(UnderlyingKind.Equity, style,
            PremiumKind.PaidUpfront, side, 100, 100, 1, .05, DividendKind.DiscreteCash)
        { CashDividends = [new(.5, 3)] };

    [Theory]
    [InlineData(OptionSide.Call)]
    [InlineData(OptionSide.Put)]
    public void EuropeanCashJumpMatchesIndependentConditionalIntegration(OptionSide side)
    {
        var r = Request(side: side);
        var c = new Calculator(new() { Steps = 1600, SpatialSteps = 1600 });
        // Integrate the lognormal stock immediately before the dividend, then the
        // independent closed-form continuation after its limited-liability cash jump.
        var plain = r with { Dividends = DividendKind.None, CashDividends = [], TimeToExpiry = .5 };
        const int n = 4000;
        const double dz = 16d / n;
        double sum = 0;
        for (int i = 0; i <= n; i++)
        {
            double z = -8 + i * dz;
            double stock = Math.Max(100 * Math.Exp((.05 - .5 * .2 * .2) * .5 + .2 * Math.Sqrt(.5) * z) - 3, 0);
            double continuation = stock == 0 ? (side == OptionSide.Put ? 100 * Math.Exp(-.025) : 0) :
                c.TheoreticalPrice(plain with { UnderlyingPrice = stock }, .2).Price!.Value;
            sum += (i == 0 || i == n ? 1 : i % 2 == 0 ? 2 : 4) *
                continuation * Math.Exp(-z * z / 2) / Math.Sqrt(2 * Math.PI);
        }
        var reference = Math.Exp(-.025) * sum * dz / 3;
        var result = c.TheoreticalPrice(r, .2);
        Assert.True(result.Success, result.Failure.ToString());
        output.WriteLine($"Conditional integration reference: {reference:G17}; FD price: {result.Price:G17}; absolute error: {Math.Abs(result.Price!.Value - reference):G17}");
        Assert.InRange(Math.Abs(result.Price!.Value - reference), 0, .006);
    }

    [Theory]
    [InlineData(ExerciseKind.European, OptionSide.Call)]
    [InlineData(ExerciseKind.European, OptionSide.Put)]
    [InlineData(ExerciseKind.American, OptionSide.Call)]
    [InlineData(ExerciseKind.American, OptionSide.Put)]
    public void CashDividendPricesGreeksAndIvRoundTrip(ExerciseKind style, OptionSide side)
    {
        var c = new Calculator(new() { Steps = 200, SpatialSteps = 200, MaximumVolatility = .5 });
        var r = Request(style, side);
        var p = c.Price(r, .24);
        Assert.True(p.Success, p.Failure.ToString());
        Assert.True(p.Value!.Value.Gamma >= -1e-8);
        Assert.True(p.Value.Value.Vega > 0);
        var iv = c.ImpliedVolatility(r, p.Value.Value.Price);
        Assert.True(iv.Success, iv.Failure.ToString());
        Assert.InRange(iv.Value!.Value.Volatility, .239999, .240001);
        Assert.Equal(r, iv.Request);
    }

    [Fact]
    public void AmericanCallExercisesBeforeLargeCashDividend()
    {
        var c = new Calculator(new() { Steps = 800, SpatialSteps = 800 });
        var r = Request(ExerciseKind.American) with { UnderlyingPrice = 120, CashDividends = [new(.25, 20)] };
        var a = c.TheoreticalPrice(r, .15);
        var e = c.TheoreticalPrice(r with { Exercise = ExerciseKind.European }, .15);
        Assert.True(a.Success && e.Success);
        Assert.True(a.Price >= 20);
        Assert.True(a.Price > e.Price + 10);
        var fine = new Calculator(new() { Steps = 1600, SpatialSteps = 1600 }).TheoreticalPrice(r, .15);
        Assert.InRange(Math.Abs(a.Price!.Value - fine.Price!.Value), 0, .025);
    }

    [Fact]
    public void EmptyScheduleConvergesToIndependentCrrAmericanPut()
    {
        var r = Request(ExerciseKind.American, OptionSide.Put) with
            { UnderlyingPrice = 36, Strike = 40, Rate = .06, CashDividends = [] };
        var result = new Calculator(new() { Steps = 1600, SpatialSteps = 1600 }).Price(r, .2);
        Assert.True(result.Success);
        Assert.InRange(result.Value!.Value.Price, 4.47, 4.50);
    }

    [Fact]
    public void ZeroVolatilityIncludesPreDividendExerciseAndLimitedLiability()
    {
        var c = new Calculator();
        var r = Request(ExerciseKind.American) with { Rate = 0, UnderlyingPrice = 120, CashDividends = [new(.5, 50)] };
        Assert.Equal(20, c.TheoreticalPrice(r, 0).Price);
        Assert.Equal(0, c.TheoreticalPrice(r with { Exercise = ExerciseKind.European }, 0).Price);
        Assert.Equal(100, c.TheoreticalPrice(r with { Side = OptionSide.Put, CashDividends = [new(.5, 200)] }, 0).Price);
    }

    [Fact]
    public void InvalidSchedulesAndIterationExhaustionFailExplicitly()
    {
        var c = new Calculator();
        foreach (var schedule in new[] {
            new[] { new CashDividend(0, 1) }, new[] { new CashDividend(1, 1) },
            new[] { new CashDividend(.5, -1) }, new[] { new CashDividend(.5, double.NaN) },
            new[] { new CashDividend(.6, 1), new CashDividend(.5, 1) } })
        {
            var result = c.Price(Request() with { CashDividends = [.. schedule] }, .2);
            Assert.Equal(PricingFailure.InvalidInput, result.Failure);
            Assert.Null(result.Value);
        }
        var limited = new Calculator(new() { MaximumSorIterations = 1 });
        Assert.Equal(PricingFailure.NumericalFailure, limited.Price(Request(ExerciseKind.American), .2).Failure);
        Assert.Throws<OperationCanceledException>(() => c.Price(Request(), .2, new CancellationToken(true)));
    }
}
