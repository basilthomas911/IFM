using System.Text.Json;
using TomasAI.IFM.Framework.OptionPricer.Pricing;
using LegacyModel = TomasAI.IFM.Framework.OptionPricer.Black76.OptionModel;

namespace TomasAI.IFM.Domain.OptionPricer.IntegrationTests;

/// <summary>Framework integration over frozen inputs; does not claim actor/database qualification.</summary>
public sealed class UnifiedPricingCompatibilityTests
{
    [Theory]
    [InlineData(UnderlyingKind.Futures, ExerciseKind.European)]
    [InlineData(UnderlyingKind.Futures, ExerciseKind.American)]
    [InlineData(UnderlyingKind.Equity, ExerciseKind.European)]
    [InlineData(UnderlyingKind.Equity, ExerciseKind.American)]
    public void FastTierEvidenceRoundTripsAndReplays(UnderlyingKind underlying, ExerciseKind exercise)
    {
        var r = new OptionPricingRequest(underlying, exercise, PremiumKind.PaidUpfront,
            OptionSide.Put, 100, 103, .5, .04,
            underlying == UnderlyingKind.Equity ? DividendKind.DiscreteCash : DividendKind.None)
            { CashDividends = underlying == UnderlyingKind.Equity ? [new(.2, 2)] : [] };
        var calculator = new OptionCalculator(new() { Steps = 100, SpatialSteps = 100 });
        var fast = calculator.PriceAndDelta(r, .24);
        Assert.True(fast.Success);
        var restored = JsonSerializer.Deserialize<PriceDeltaResult>(JsonSerializer.Serialize(fast));
        Assert.Equal(fast.Value, restored.Value);
        Assert.Equal(fast.NumericalPolicy, restored.NumericalPolicy);
        Assert.Equal(fast.EngineVersion, restored.EngineVersion);
        Assert.True(r.CashDividends.SequenceEqual(restored.Request.CashDividends));
        Assert.Equal(fast.Value, calculator.PriceAndDelta(restored.Request, .24).Value);
        var iv = calculator.SolveImpliedVolatility(r, fast.Value!.Value.Price);
        Assert.True(iv.Success);
        var restoredIv = JsonSerializer.Deserialize<ImpliedVolatilityResult>(JsonSerializer.Serialize(iv));
        Assert.Equal(iv.Value, restoredIv.Value);
        Assert.Equal(iv.NumericalPolicy, restoredIv.NumericalPolicy);
        Assert.Equal(iv.EngineVersion, restoredIv.EngineVersion);
        Assert.True(r.CashDividends.SequenceEqual(restoredIv.Request.CashDividends));
        Assert.Equal(iv.Value, calculator.SolveImpliedVolatility(restoredIv.Request, fast.Value.Value.Price).Value);
        var failure = calculator.SolveImpliedVolatility(r, -1);
        var restoredFailure = JsonSerializer.Deserialize<ImpliedVolatilityResult>(JsonSerializer.Serialize(failure));
        Assert.Equal(PricingFailure.InvalidInput, restoredFailure.Failure);
        Assert.Null(restoredFailure.Value);
    }

    [Theory]
    [InlineData(ExerciseKind.European)]
    [InlineData(ExerciseKind.American)]
    public void CashScheduleAndNumericalEvidenceSurviveJsonBoundary(ExerciseKind exercise)
    {
        var original = new OptionPricingRequest(UnderlyingKind.Equity, exercise, PremiumKind.PaidUpfront,
            OptionSide.Put, 100, 100, .5, .04, DividendKind.DiscreteCash)
            { CashDividends = [new(.2, 2)] };
        var request = JsonSerializer.Deserialize<OptionPricingRequest>(JsonSerializer.Serialize(original));
        Assert.True(original.CashDividends.SequenceEqual(request.CashDividends));
        var calculator = new OptionCalculator(new() { Steps = 100, SpatialSteps = 200 });
        var result = calculator.Price(request, .25);
        Assert.True(result.Success);
        var restored = JsonSerializer.Deserialize<PricingResult>(JsonSerializer.Serialize(result));
        Assert.Equal(result.Value, restored.Value);
        Assert.Equal(result.NumericalPolicy, restored.NumericalPolicy);
        Assert.Equal(result.EngineVersion, restored.EngineVersion);
        Assert.True(result.Request.CashDividends.SequenceEqual(restored.Request.CashDividends));
        Assert.Equal(result.Value, calculator.Price(restored.Request, .25).Value);
    }

    [Theory]
    [InlineData(OptionSide.Call)]
    [InlineData(OptionSide.Put)]
    public void FrozenRequestRoundTripUsesExistingBlack76KernelAndCompatibleGreeks(OptionSide side)
    {
        var original = new OptionPricingRequest(UnderlyingKind.Futures, ExerciseKind.European,
            PremiumKind.PaidUpfront, side, 5200, 5250, .25, .04);
        var request = JsonSerializer.Deserialize<OptionPricingRequest>(JsonSerializer.Serialize(original));
        Assert.Equal(original, request);
        var mark = LegacyModel.Price(5200, 5250, .04, .22, .25, side == OptionSide.Call ? 1 : -1);
        var expected = LegacyModel.PriceWithGreeks(5200, 5250, .04, .22, .25,
            side == OptionSide.Call ? 1 : -1);
        var current = new OptionCalculator().ImpliedVolatility(request, mark);
        Assert.True(current.Success);
        Assert.Equal(OptionCalculator.EngineVersionFor(request), current.EngineVersion);
        var g = current.Value!.Value;
        Assert.InRange(Math.Abs(g.Volatility - .22), 0, 1e-7);
        Assert.InRange(Math.Abs(g.Delta - expected.Delta), 0, 1e-7);
        Assert.InRange(Math.Abs(g.Gamma - expected.Gamma), 0, 1e-7);
        Assert.InRange(Math.Abs(g.Vega - expected.Vega), 0, 1e-4);
        Assert.InRange(Math.Abs(g.Theta - expected.Theta), 0, 1e-4);
        Assert.InRange(Math.Abs(g.Rho - expected.Rho), 0, 1e-4);
    }
}
