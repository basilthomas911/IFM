using FluentAssertions;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Framework.OptionPricer.Black76;

namespace TomasAI.IFM.Domain.OptionPricer.UnitTests.Optimization;

public sealed class OptionCalculatorTests
{
    private static readonly DateOnly ValueDate = new(2026, 1, 1);
    private static readonly DateOnly MaturityDate = new(2027, 1, 1);

    [Theory]
    [InlineData(OptionTypeName.Call, 1)]
    [InlineData(OptionTypeName.Put, -1)]
    public void GetOptionGreeksRecoversBlack76VolatilityAndGreeks(string optionTypeName, int optionType)
    {
        const double forward = 100.0;
        const double strike = 105.0;
        const double rate = 0.04;
        const double volatility = 0.25;
        const double timeToExpiry = 1.0;
        var marketPrice = OptionModel.Price(
            forward, strike, rate, volatility, timeToExpiry, optionType);
        var expected = OptionModel.PriceWithGreeks(
            forward, strike, rate, volatility, timeToExpiry, optionType);

        var actual = new OptionCalculator(ValueDate, MaturityDate)
            .GetOptionGreeks(optionTypeName, forward, strike, marketPrice, rate);

        actual.Success.Should().BeTrue();
        actual.ImpliedVolatility.Should().BeApproximately(volatility, 1e-9);
        actual.Delta.Should().BeApproximately(expected.Delta, 1e-9);
        actual.Gamma.Should().BeApproximately(expected.Gamma, 1e-9);
        actual.Theta.Should().BeApproximately(expected.Theta, 1e-9);
        actual.Vega.Should().BeApproximately(expected.Vega, 1e-9);
        actual.Rho.Should().BeApproximately(expected.Rho, 1e-9);
    }

    [Fact]
    public void GetOptionGreeksRejectsInvalidAndUnsolvableInputs()
    {
        var calculator = new OptionCalculator(ValueDate, MaturityDate);

        calculator.GetOptionGreeks("UNKNOWN", 100, 100, 10, 0.04).Success.Should().BeFalse();
        calculator.GetOptionGreeks(OptionTypeName.Call, double.NaN, 100, 10, 0.04).Success.Should().BeFalse();
        calculator.GetOptionGreeks(OptionTypeName.Call, 100, 0, 10, 0.04).Success.Should().BeFalse();
        calculator.GetOptionGreeks(OptionTypeName.Call, 100, 100, 0, 0.04).Success.Should().BeFalse();
        calculator.GetOptionGreeks(OptionTypeName.Call, 100, 100, 100, 0.04).Success.Should().BeFalse();
        new OptionCalculator(MaturityDate, ValueDate)
            .GetOptionGreeks(OptionTypeName.Call, 100, 100, 10, 0.04)
            .Success.Should().BeFalse();
    }

    [Fact]
    public void GetOptionGreeksIsDeterministicUnderConcurrentUse()
    {
        const double forward = 5_200.0;
        const double strike = 5_250.0;
        const double rate = 0.045;
        const double volatility = 0.22;
        var calculator = new OptionCalculator(ValueDate, MaturityDate);
        var marketPrice = OptionModel.Price(forward, strike, rate, volatility, 1.0, 1);
        var results = new OptionGreeks[4_096];

        Parallel.For(0, results.Length, index =>
            results[index] = calculator.GetOptionGreeks(
                OptionTypeName.Call, forward, strike, marketPrice, rate));

        results.Should().OnlyContain(result => result.Success);
        results.Should().OnlyContain(result =>
            Math.Abs(result.ImpliedVolatility - volatility) <= 1e-9);
        results.Should().OnlyContain(result => result.Equals(results[0]));
    }
}
