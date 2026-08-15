using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using FluentAssertions;
using TomasAI.IFM.Framework.OptionPricer.Black76;

namespace TomasAI.IFM.Domain.OptionPricer.UnitTests.Optimization;

public sealed class Black76BehaviorContractTests
{
    private const double Forward = 100.0;
    private const double Strike = 105.0;
    private const double Rate = 0.04;
    private const double Volatility = 0.25;
    private const double TimeToExpiry = 1.0;

    [Fact]
    public void OptionTypeUsesPositiveForCallAndNonPositiveForPut()
    {
        OptionModel.Price(Forward, Strike, Rate, Volatility, TimeToExpiry, 2)
            .Should().Be(OptionModel.Price(Forward, Strike, Rate, Volatility, TimeToExpiry, 1));
        OptionModel.Price(Forward, Strike, Rate, Volatility, TimeToExpiry, 0)
            .Should().Be(OptionModel.Price(Forward, Strike, Rate, Volatility, TimeToExpiry, -1));
    }

    [Theory]
    [InlineData(110.0, 100.0, 1, 10.0, 1.0)]
    [InlineData(90.0, 100.0, 1, 0.0, 0.0)]
    [InlineData(90.0, 100.0, -1, 10.0, -1.0)]
    [InlineData(110.0, 100.0, -1, 0.0, 0.0)]
    [InlineData(100.0, 100.0, 1, 0.0, 0.0)]
    [InlineData(100.0, 100.0, -1, 0.0, 0.0)]
    public void ExpiredGreeksUseIntrinsicPriceAndDiscreteDelta(
        double forward,
        double strike,
        int optionType,
        double expectedPrice,
        double expectedDelta)
    {
        var actual = OptionModel.PriceWithGreeks(forward, strike, Rate, Volatility, 0.0, optionType);

        actual.Should().Be(new Black76Result(expectedPrice, expectedDelta, 0.0, 0.0, 0.0, 0.0));
    }

    [Fact]
    public void NonPositiveVolatilityUsesDiscountedIntrinsicAndPriceBasedRho()
    {
        const double forward = 110.0;
        const double strike = 100.0;
        const double time = 2.0;
        var expectedPrice = Math.Exp(-Rate * time) * (forward - strike);

        var actual = OptionModel.PriceWithGreeks(forward, strike, Rate, 0.0, time, 1);

        actual.Price.Should().Be(expectedPrice);
        actual.Delta.Should().Be(0.0);
        actual.Gamma.Should().Be(0.0);
        actual.Vega.Should().Be(0.0);
        actual.Theta.Should().Be(0.0);
        actual.Rho.Should().Be(-time * expectedPrice);
    }

    [Theory]
    [InlineData(0.0, 100.0)]
    [InlineData(-1.0, 100.0)]
    [InlineData(100.0, 0.0)]
    [InlineData(100.0, -1.0)]
    public void PriceWithGreeksRejectsNonPositiveForwardOrStrike(double forward, double strike)
    {
        var action = () => OptionModel.PriceWithGreeks(forward, strike, Rate, Volatility, TimeToExpiry, 1);

        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ImpliedVolatilityReturnsNaNWhenIterationLimitIsExhausted()
    {
        var marketPrice = OptionModel.Price(Forward, Strike, Rate, Volatility, TimeToExpiry, 1);

        var actual = OptionModel.ImpliedVolatility(
            Forward,
            Strike,
            Rate,
            marketPrice,
            TimeToExpiry,
            1,
            maxIterations: 0);

        actual.Should().BeNaN();
    }

    [Theory]
    [InlineData(0.0, 100.0, 10.0, 1.0)]
    [InlineData(100.0, 0.0, 10.0, 1.0)]
    [InlineData(100.0, 100.0, 0.0, 1.0)]
    [InlineData(100.0, 100.0, 10.0, 0.0)]
    public void ImpliedVolatilityRejectsNonPositiveRequiredInputs(
        double forward,
        double strike,
        double marketPrice,
        double timeToExpiry)
    {
        var action = () => OptionModel.ImpliedVolatility(
            forward,
            strike,
            Rate,
            marketPrice,
            timeToExpiry,
            1);

        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void BatchPricingMatchesScalarPricing()
    {
        double[] forwards = [100.0, 110.0, 90.0];
        double[] strikes = [100.0, 100.0, 100.0];
        double[] rates = [0.01, 0.02, 0.03];
        double[] volatilities = [0.2, 0.3, 0.4];
        double[] expiries = [0.25, 0.5, 1.0];
        int[] optionTypes = [1, -1, 0];
        var prices = new double[forwards.Length];
        var greeks = new Black76Result[forwards.Length];

        OptionModel.PriceBatch(forwards, strikes, rates, volatilities, expiries, optionTypes, prices);
        OptionModel.PriceWithGreeksBatch(forwards, strikes, rates, volatilities, expiries, optionTypes, greeks);

        for (var index = 0; index < forwards.Length; index++)
        {
            prices[index].Should().Be(OptionModel.Price(
                forwards[index], strikes[index], rates[index], volatilities[index], expiries[index], optionTypes[index]));
            greeks[index].Should().Be(OptionModel.PriceWithGreeks(
                forwards[index], strikes[index], rates[index], volatilities[index], expiries[index], optionTypes[index]));
        }
    }

    [Fact]
    public void BatchRejectsMismatchedLengthsBeforeWritingResults()
    {
        double[] forwards = [100.0];
        double[] empty = [];
        int[] optionTypes = [1];
        double[] results = [123.0];

        var action = () => OptionModel.PriceBatch(
            forwards,
            empty,
            forwards,
            forwards,
            forwards,
            optionTypes,
            results);

        action.Should().Throw<ArgumentException>();
        results.Should().Equal(123.0);
    }

    [Fact]
    public void GreeksBatchPreservesPartialWriteBehaviorAtFirstInvalidContract()
    {
        double[] forwards = [100.0, 0.0, 110.0];
        double[] strikes = [100.0, 100.0, 100.0];
        double[] rates = [Rate, Rate, Rate];
        double[] volatilities = [Volatility, Volatility, Volatility];
        double[] expiries = [TimeToExpiry, TimeToExpiry, TimeToExpiry];
        int[] optionTypes = [1, 1, 1];
        var sentinel = new Black76Result(9.0, 8.0, 7.0, 6.0, 5.0, 4.0);
        Black76Result[] results = [sentinel, sentinel, sentinel];

        var action = () => OptionModel.PriceWithGreeksBatch(
            forwards,
            strikes,
            rates,
            volatilities,
            expiries,
            optionTypes,
            results);

        action.Should().Throw<ArgumentOutOfRangeException>();
        results[0].Should().Be(OptionModel.PriceWithGreeks(
            forwards[0], strikes[0], rates[0], volatilities[0], expiries[0], optionTypes[0]));
        results[1].Should().Be(sentinel);
        results[2].Should().Be(sentinel);
    }

    [Fact]
    public void Black76ResultHasFrozenNativeLayout()
    {
        typeof(Black76Result).StructLayoutAttribute!.Value.Should().Be(LayoutKind.Sequential);
        Unsafe.SizeOf<Black76Result>().Should().Be(48);
        Marshal.SizeOf<Black76Result>().Should().Be(48);

        Span<Black76Result> result = [new Black76Result(1.0, 2.0, 3.0, 4.0, 5.0, 6.0)];
        var fields = MemoryMarshal.Cast<Black76Result, double>(result);

        fields.ToArray().Should().Equal(1.0, 2.0, 3.0, 4.0, 5.0, 6.0);
    }

    [Fact]
    public void ImpliedVolatilityDefaultsRemainFrozen()
    {
        var method = typeof(OptionModel).GetMethod(nameof(OptionModel.ImpliedVolatility));

        method.Should().NotBeNull();
        method!.ReturnType.Should().Be(typeof(double));
        var parameters = method.GetParameters();
        parameters.Should().HaveCount(9);
        parameters[6].IsOptional.Should().BeTrue();
        parameters[6].DefaultValue.Should().Be(1e-10);
        parameters[7].IsOptional.Should().BeTrue();
        parameters[7].DefaultValue.Should().Be(100);
        parameters[8].IsOptional.Should().BeTrue();
        parameters[8].DefaultValue.Should().BeNull();
    }
}
