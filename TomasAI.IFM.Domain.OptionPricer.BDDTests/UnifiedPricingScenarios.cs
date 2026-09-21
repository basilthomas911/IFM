using TomasAI.IFM.Framework.OptionPricer.Pricing;

namespace TomasAI.IFM.Domain.OptionPricer.BDDTests;

public sealed class UnifiedPricingScenarios
{
    [Theory]
    [InlineData(UnderlyingKind.Futures, ExerciseKind.European)]
    [InlineData(UnderlyingKind.Futures, ExerciseKind.American)]
    [InlineData(UnderlyingKind.Equity, ExerciseKind.European)]
    [InlineData(UnderlyingKind.Equity, ExerciseKind.American)]
    public void GivenQualifiedIv_WhenRefreshingSelection_ThenOnlyPriceAndDeltaAreReturned(
        UnderlyingKind underlying, ExerciseKind exercise)
    {
        var calculator = new OptionCalculator(new() { Steps = 101 });
        foreach (var side in new[] { OptionSide.Call, OptionSide.Put })
        {
            var given = new OptionPricingRequest(underlying, exercise, PremiumKind.PaidUpfront,
                side, 100, 103, .5, .04);
            var mark = calculator.TheoreticalPrice(given, .24).Price!.Value;
            var iv = calculator.SolveImpliedVolatility(given, mark);
            Assert.True(iv.Success);
            var refreshed = given with { UnderlyingPrice = 101 };
            var selected = calculator.PriceAndDelta(refreshed, iv.Value!.Value.Volatility);
            Assert.True(selected.Success);
            Assert.Equal(iv.Value.Value.Volatility, selected.Value!.Value.Volatility);
            Assert.Equal(calculator.Price(refreshed, iv.Value.Value.Volatility).Value!.Value.Delta,
                selected.Value.Value.Delta, 10);
            Assert.DoesNotContain(typeof(OptionPriceDeltaValues).GetProperties(),
                p => p.Name is "Gamma" or "Vega" or "Theta" or "Rho");
            Assert.DoesNotContain(typeof(ImpliedVolatilityValues).GetProperties(),
                p => p.Name is "Delta" or "Gamma" or "Vega" or "Theta" or "Rho");
        }
    }

    [Fact]
    public void GivenUnidentifiableIv_WhenRefreshing_ThenNoVolatilityOrSelectionNumbersAreInvented()
    {
        var request = new OptionPricingRequest(UnderlyingKind.Futures, ExerciseKind.American,
            PremiumKind.PaidUpfront, OptionSide.Put, 100, 100, .5, .04);
        var result = new OptionCalculator().SolveImpliedVolatility(request, 0);
        Assert.Equal(PricingFailure.ImpliedVolatilityNotIdentifiable, result.Failure);
        Assert.Null(result.Value);
    }

    [Theory]
    [InlineData(ExerciseKind.European)]
    [InlineData(ExerciseKind.American)]
    public void GivenCashDividendSchedule_WhenPriced_ThenDistinctModelAndBatchParity(ExerciseKind exercise)
    {
        var given = new OptionPricingRequest(UnderlyingKind.Equity, exercise, PremiumKind.PaidUpfront,
            OptionSide.Call, 100, 100, .5, .04, DividendKind.DiscreteCash)
            { CashDividends = [new(.2, 2), new(.4, 2)] };
        var calculator = new OptionCalculator(new() { Steps = 100, SpatialSteps = 200 });
        var requests = new[] { given, given with { Side = OptionSide.Put } };
        var batch = new PricingResult[2];
        calculator.PriceBatch(requests, new[] { .2, .3 }, batch);
        Assert.All(batch, x => Assert.True(x.Success, x.Failure.ToString()));
        Assert.Equal(calculator.Price(requests[0], .2), batch[0]);
        Assert.Equal(calculator.Price(requests[1], .3), batch[1]);
        Assert.Contains("CashDividend", batch[0].EngineVersion);
    }

    [Fact]
    public void GivenInvalidAndExpiredInputs_WhenBatchPriced_ThenFailuresStayInInputOrder()
    {
        var given = new OptionPricingRequest(UnderlyingKind.Futures, ExerciseKind.European,
            PremiumKind.PaidUpfront, OptionSide.Call, 100, 100, .5, .04);
        var results = new PricingResult[3];
        new OptionCalculator().PriceBatch(new[] { given, given with { UnderlyingPrice = 0 },
            given with { TimeToExpiry = -1 } }, new[] { .2, .2, .2 }, results);
        Assert.True(results[0].Success);
        Assert.Equal(PricingFailure.InvalidInput, results[1].Failure);
        Assert.Equal(PricingFailure.Expired, results[2].Failure);
        Assert.Null(results[1].Value);
        Assert.Null(results[2].Value);
    }

    [Theory]
    [InlineData(UnderlyingKind.Futures, ExerciseKind.European, "Black76")]
    [InlineData(UnderlyingKind.Futures, ExerciseKind.American, "AmericanFutures")]
    [InlineData(UnderlyingKind.Equity, ExerciseKind.European, "BlackScholesMerton")]
    [InlineData(UnderlyingKind.Equity, ExerciseKind.American, "AmericanEquity")]
    public void GivenQualifiedOption_WhenPriceThenInvert_ThenCorrectModelAndVolatility(
        UnderlyingKind underlying, ExerciseKind exercise, string engine)
    {
        var given = new OptionPricingRequest(underlying, exercise, PremiumKind.PaidUpfront,
            OptionSide.Put, 100, 105, .25, .04);
        var calculator = new OptionCalculator(new() { Steps = 201 });
        foreach (var right in new[] { OptionSide.Call, OptionSide.Put })
        {
            var request = given with { Side = right };
            var price = calculator.Price(request, .3);
            Assert.True(price.Success);
            var observed = calculator.ImpliedVolatility(request, price.Value!.Value.Price);
            Assert.True(observed.Success, observed.Failure.ToString());
            Assert.StartsWith(engine, observed.EngineVersion);
            Assert.InRange(observed.Value!.Value.Volatility, .299999, .300001);
        }
    }

    [Theory]
    [InlineData(ExerciseKind.Unknown)]
    [InlineData((ExerciseKind)99)]
    public void GivenUnknownExercise_WhenPriced_ThenNoUsableNumbers(ExerciseKind style)
    {
        var request = new OptionPricingRequest(UnderlyingKind.Futures, style,
            PremiumKind.PaidUpfront, OptionSide.Call, 5000, 5050, .1, .04);
        var result = new OptionCalculator().Price(request, .2);
        Assert.Equal(PricingFailure.UnsupportedConvention, result.Failure);
        Assert.Null(result.Value);
    }

    [Fact]
    public void GivenEarlyExercisePut_WhenIvIsNotIdentifiable_ThenNoArbitraryVolatility()
    {
        var request = new OptionPricingRequest(UnderlyingKind.Equity, ExerciseKind.American,
            PremiumKind.PaidUpfront, OptionSide.Put, 20, 100, .1, .2);
        var result = new OptionCalculator().ImpliedVolatility(request, 80);
        Assert.Equal(PricingFailure.ImpliedVolatilityNotIdentifiable, result.Failure);
        Assert.Null(result.Value);
    }
}
