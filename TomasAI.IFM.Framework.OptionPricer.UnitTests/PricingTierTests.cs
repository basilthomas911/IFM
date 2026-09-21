using TomasAI.IFM.Framework.OptionPricer.Pricing;

namespace TomasAI.IFM.Framework.OptionPricer.UnitTests;

public class PricingTierTests
{
    public static IEnumerable<object[]> Cases()
    {
        foreach (var exercise in new[] { ExerciseKind.European, ExerciseKind.American })
        foreach (var side in new[] { OptionSide.Call, OptionSide.Put })
        {
            foreach (var premium in new[] { PremiumKind.PaidUpfront, PremiumKind.FuturesStyle })
                yield return [new OptionPricingRequest(UnderlyingKind.Futures, exercise, premium, side, 100, 103, .5, -.02)];
            foreach (var dividend in new[] { DividendKind.None, DividendKind.ContinuousYield, DividendKind.DiscreteCash })
                yield return [new OptionPricingRequest(UnderlyingKind.Equity, exercise, PremiumKind.PaidUpfront,
                    side, 100, 103, .5, .04, dividend, dividend == DividendKind.ContinuousYield ? .02 : 0)
                    { CashDividends = dividend == DividendKind.DiscreteCash ? [new(.2, 2)] : [] }];
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void FastTiersAgreeWithFullPricingAcrossModels(OptionPricingRequest r)
    {
        var policy = new PricingSettings { Steps = 101, SpatialSteps = 100 };
        var c = new OptionCalculator(policy);
        var full = c.Price(r, .24);
        var fast = c.PriceAndDelta(r, .24);
        Assert.True(full.Success && fast.Success, $"{full.Failure}/{fast.Failure}");
        Assert.Equal(full.Value!.Value.Price, fast.Value!.Value.Price, 10);
        Assert.Equal(full.Value.Value.Delta, fast.Value.Value.Delta, 10);
        Assert.Equal(.24, fast.Value.Value.Volatility);
        var iv = c.SolveImpliedVolatility(r, full.Value.Value.Price);
        var legacy = c.ImpliedVolatility(r, full.Value.Value.Price);
        Assert.True(iv.Success && legacy.Success, $"{iv.Failure}/{legacy.Failure}");
        Assert.Equal(legacy.Value!.Value.Volatility, iv.Value!.Value.Volatility);
        Assert.InRange(iv.Value.Value.Volatility, .239999, .240001);
        Assert.InRange(Math.Abs(iv.Value.Value.Residual), 0, policy.PriceTolerance);
        Assert.Equal(iv.Value.Value.RepricedPrice - full.Value.Value.Price, iv.Value.Value.Residual);
        Assert.InRange(iv.Value.Value.Iterations, 1, policy.MaximumIterations);
        Assert.Equal(r, fast.Request);
        Assert.Equal(r, iv.Request);
        Assert.Same(policy, fast.NumericalPolicy);
        Assert.Same(policy, iv.NumericalPolicy);
        Assert.Equal(full.EngineVersion, fast.EngineVersion);
        Assert.Equal(full.EngineVersion, iv.EngineVersion);
    }

    private static OptionPricingRequest Request => new(UnderlyingKind.Futures, ExerciseKind.American,
        PremiumKind.PaidUpfront, OptionSide.Call, 100, 100, .5, .04);

    [Fact]
    public void EuropeanDeltaIsAnalyticNotThePrivatePriceEvaluatorsPlaceholder()
    {
        var r = Request with { Underlying = UnderlyingKind.Equity, Exercise = ExerciseKind.European,
            TimeToExpiry = 1, Rate = .05 };
        var c = new OptionCalculator();
        var call = c.PriceAndDelta(r, .2);
        var put = c.PriceAndDelta(r with { Side = OptionSide.Put }, .2);
        Assert.InRange(call.Value!.Value.Delta, .63682, .63684);
        Assert.InRange(put.Value!.Value.Delta, -.36318, -.36316);
        Assert.Equal("BlackScholesMerton.Managed/v1", call.EngineVersion);
    }

    [Fact]
    public void FastPathsDoNotExecuteTheFullGreekPostpass()
    {
        var c = new OptionCalculator(new() { Steps = 100, SpatialSteps = 100 });
        var r = Request with { Underlying = UnderlyingKind.Equity, Dividends = DividendKind.DiscreteCash,
            TimeToExpiry = 1, CashDividends = [new(.5, 2)] };
        // At the FD total-volatility boundary the core price/Delta exists, but
        // the full calculation's upward Vega bump is outside the qualified domain.
        Assert.True(c.PriceAndDelta(r, .5).Success);
        Assert.Equal(PricingFailure.NumericalFailure, c.Price(r, .5).Failure);
        // At the carry boundary an IV solution exists, but its Rho bump is invalid.
        r = r with { Rate = .25 };
        var mark = c.TheoreticalPrice(r, .2);
        Assert.True(mark.Success);
        Assert.True(c.SolveImpliedVolatility(r, mark.Price!.Value).Success);
        Assert.Equal(PricingFailure.NumericalFailure, c.ImpliedVolatility(r, mark.Price.Value).Failure);
    }

    [Fact]
    public void FailuresHaveNoPayloadAndSolverLimitsRemainCompatible()
    {
        var c = new OptionCalculator(new() { Steps = 101 });
        foreach (var r in new[] { Request with { UnderlyingPrice = double.NaN },
            Request with { UnderlyingPrice = 0 }, Request with { TimeToExpiry = -1 },
            Request with { Exercise = ExerciseKind.Unknown },
            Request with { Dividends = DividendKind.DiscreteCash },
            Request with { CashDividends = [new(.1, 1)] } })
        {
            var fast = c.PriceAndDelta(r, .2);
            Assert.Equal(c.Price(r, .2).Failure, fast.Failure);
            Assert.Null(fast.Value);
            var iv = c.SolveImpliedVolatility(r, 5);
            Assert.Equal(c.ImpliedVolatility(r, 5).Failure, iv.Failure);
            Assert.Null(iv.Value);
        }
        foreach (double v in new[] { 0d, -.1, double.NaN, 5 })
        {
            var result = c.PriceAndDelta(Request, v);
            Assert.False(result.Success);
            Assert.Null(result.Value);
        }
        Assert.Equal(PricingFailure.UndefinedGreeks, c.PriceAndDelta(Request with { TimeToExpiry = 0 }, .2).Failure);
        foreach (double mark in new[] { -1d, double.NaN, 200, 0 })
        {
            var iv = c.SolveImpliedVolatility(Request, mark);
            Assert.Equal(c.ImpliedVolatility(Request, mark).Failure, iv.Failure);
            Assert.Null(iv.Value);
        }
        var target = c.PriceAndDelta(Request, .237).Value!.Value.Price;
        Assert.Equal(PricingFailure.NonConvergence,
            new OptionCalculator(new() { Steps = 101, MaximumIterations = 1 }).SolveImpliedVolatility(Request, target).Failure);
        Assert.Equal(PricingFailure.VolatilityNotBracketed,
            new OptionCalculator(new() { Steps = 101, MaximumVolatility = .1 }).SolveImpliedVolatility(Request, target).Failure);
        Assert.Equal(PricingFailure.ImpliedVolatilityNotIdentifiable,
            c.SolveImpliedVolatility(Request with { TimeToExpiry = 0 }, 0).Failure);
        Assert.Equal(PricingFailure.NumericalFailure,
            c.PriceAndDelta(Request with { UnderlyingPrice = double.MaxValue }, 4).Failure);
    }

    [Fact]
    public void BatchesPreserveOrderingAndEnforceBoundsAndCancellation()
    {
        var c = new OptionCalculator(new() { Steps = 101 });
        var r = new[] { Request, Request with { Side = OptionSide.Put }, Request with { Strike = 0 } };
        var v = new[] { .2, .3, .2 };
        var fast = new PriceDeltaResult[3];
        c.PriceAndDeltaBatch(r, v, fast);
        for (int i = 0; i < r.Length; i++) Assert.Equal(c.PriceAndDelta(r[i], v[i]), fast[i]);
        var marks = new[] { fast[0].Value!.Value.Price, fast[1].Value!.Value.Price, 5 };
        var iv = new ImpliedVolatilityResult[3];
        c.SolveImpliedVolatilityBatch(r, marks, iv);
        for (int i = 0; i < r.Length; i++) Assert.Equal(c.SolveImpliedVolatility(r[i], marks[i]), iv[i]);
        Assert.Throws<ArgumentException>(() => c.PriceAndDeltaBatch(r, new double[2], fast));
        Assert.Throws<ArgumentException>(() => c.SolveImpliedVolatilityBatch(r, marks, new ImpliedVolatilityResult[2]));
        Assert.Throws<ArgumentException>(() => c.PriceAndDeltaBatch(new OptionPricingRequest[2049], new double[2049], new PriceDeltaResult[2049]));
        Assert.Throws<ArgumentException>(() => c.SolveImpliedVolatilityBatch(new OptionPricingRequest[2049], new double[2049], new ImpliedVolatilityResult[2049]));
        var stopped = new CancellationToken(true);
        Assert.Throws<OperationCanceledException>(() => c.PriceAndDelta(Request, .2, stopped));
        Assert.Throws<OperationCanceledException>(() => c.SolveImpliedVolatility(Request, marks[0], stopped));
        Assert.Throws<OperationCanceledException>(() => c.PriceAndDeltaBatch(r, v, fast, stopped));
        Assert.Throws<OperationCanceledException>(() => c.SolveImpliedVolatilityBatch(r, marks, iv, stopped));
        c.PriceAndDeltaBatch([], [], []);
        c.SolveImpliedVolatilityBatch([], [], []);
        Assert.Throws<OperationCanceledException>(() => c.PriceAndDeltaBatch([], [], [], stopped));
        Assert.Throws<OperationCanceledException>(() => c.SolveImpliedVolatilityBatch([], [], [], stopped));
    }

    [Fact]
    public void FastTiersAreConcurrentAndAllocationFreeAfterWarmup()
    {
        var c = new OptionCalculator(new() { Steps = 101 });
        var price = c.PriceAndDelta(Request, .2);
        var iv = c.SolveImpliedVolatility(Request, price.Value!.Value.Price);
        Parallel.For(0, 16, _ =>
        {
            Assert.Equal(price, c.PriceAndDelta(Request, .2));
            Assert.Equal(iv, c.SolveImpliedVolatility(Request, price.Value.Value.Price));
        });
        for (int i = 0; i < 32; i++)
        {
            c.PriceAndDelta(Request, .2);
            c.SolveImpliedVolatility(Request, price.Value.Value.Price);
        }
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 20; i++)
        {
            c.PriceAndDelta(Request, .2);
            c.SolveImpliedVolatility(Request, price.Value.Value.Price);
        }
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }
}
