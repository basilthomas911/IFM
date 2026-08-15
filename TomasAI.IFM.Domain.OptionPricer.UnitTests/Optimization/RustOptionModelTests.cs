using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using FluentAssertions;
using TomasAI.IFM.Framework.OptionPricer.Black76;
using TomasAI.IFM.Framework.OptionPricer.Interop;
using Xunit.Sdk;

namespace TomasAI.IFM.Domain.OptionPricer.UnitTests.Optimization;

public sealed class RustOptionModelTests
{
    [Theory]
    [InlineData(null, 0)]
    [InlineData("", 0)]
    [InlineData("Managed", 0)]
    [InlineData("managed", 0)]
    [InlineData("Rust", 1)]
    [InlineData("rust", 1)]
    public void ImplementationSelectionParsesOnlySupportedValues(
        string? value,
        int expected)
    {
        OptionPricerBackend.Parse(value).Should().Be((OptionPricerImplementation)expected);
    }

    [Fact]
    public void ImplementationSelectionRejectsUnknownValue()
    {
        var action = () => OptionPricerBackend.Parse("Automatic");

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Managed or Rust*");
    }

    [Fact]
    public void NativeStructuresMatchTheFrozenAbi()
    {
        Unsafe.SizeOf<Black76Result>().Should().Be(48);
        Marshal.SizeOf<Black76Result>().Should().Be(48);
        Unsafe.SizeOf<NativeImpliedGreeksResult>().Should().Be(56);
        Marshal.SizeOf<NativeImpliedGreeksResult>().Should().Be(56);
    }

    [Fact]
    public void ResolverUsesTheWindowsRidNativeLayout()
    {
        var expected = Path.GetFullPath(Path.Combine(
            "C:\\application",
            "runtimes",
            "win-x64",
            "native",
            "ifm_option_pricer_native.dll"));

        OptionPricerNativeLibraryResolver.GetExpectedPath("C:\\application")
            .Should().Be(expected);
    }

    [Fact]
    public void RustScalarPriceAndGreeksMatchManagedAcrossRepresentativeGrid()
    {
        double[] forwards = [75.0, 100.0, 125.0, 5_300.0];
        double[] strikeRatios = [0.75, 0.95, 1.0, 1.05, 1.25];
        double[] rates = [-0.02, 0.0, 0.045, 0.15];
        double[] volatilities = [0.0, 0.05, 0.20, 0.80, 2.0];
        double[] expiries = [0.0, 1.0 / 365.0, 0.25, 1.0, 5.0];
        int[] optionTypes = [-1, 0, 1, 2];

        foreach (var forward in forwards)
        foreach (var strikeRatio in strikeRatios)
        foreach (var rate in rates)
        foreach (var volatility in volatilities)
        foreach (var expiry in expiries)
        foreach (var optionType in optionTypes)
        {
            var strike = forward * strikeRatio;
            var managedPrice = OptionModel.PriceManaged(
                forward, strike, rate, volatility, expiry, optionType);
            var rustPrice = RustOptionModel.Price(
                forward, strike, rate, volatility, expiry, optionType);
            AssertClose(rustPrice, managedPrice, 5e-13);

            var managedGreeks = OptionModel.PriceWithGreeksManaged(
                forward, strike, rate, volatility, expiry, optionType);
            var rustGreeks = RustOptionModel.PriceWithGreeks(
                forward, strike, rate, volatility, expiry, optionType);
            AssertResultClose(rustGreeks, managedGreeks, 5e-13);
        }
    }

    [Theory]
    [InlineData(100.0, 105.0, 0.04, 0.25, 1.0, 1)]
    [InlineData(100.0, 95.0, -0.01, 0.35, 0.5, -1)]
    [InlineData(5_300.0, 5_250.0, 0.045, 0.18, 0.25, 1)]
    [InlineData(20.0, 30.0, 0.15, 0.80, 3.0, 0)]
    public void RustImpliedVolatilityAndFusedOperationMatchManaged(
        double forward,
        double strike,
        double rate,
        double volatility,
        double expiry,
        int optionType)
    {
        var marketPrice = OptionModel.PriceManaged(
            forward, strike, rate, volatility, expiry, optionType);
        var managedVolatility = OptionModel.ImpliedVolatilityManaged(
            forward, strike, rate, marketPrice, expiry, optionType, 1e-10, 100, null);
        var rustVolatility = RustOptionModel.ImpliedVolatility(
            forward, strike, rate, marketPrice, expiry, optionType, 1e-10, 100, null);

        AssertClose(rustVolatility, managedVolatility, 5e-10);
        RustOptionModel.TryImpliedVolatilityWithGreeks(
                forward,
                strike,
                rate,
                marketPrice,
                expiry,
                optionType,
                1e-10,
                100,
                null,
                out var fusedVolatility,
                out var fusedGreeks)
            .Should().BeTrue();
        AssertClose(fusedVolatility, managedVolatility, 5e-10);
        AssertResultClose(
            fusedGreeks,
            OptionModel.PriceWithGreeksManaged(
                forward, strike, rate, managedVolatility, expiry, optionType),
            5e-10);
    }

    [Fact]
    public void RustBatchOperationsMatchManagedScalarOperations()
    {
        double[] forwards = [100.0, 105.0, 110.0, 115.0];
        double[] strikes = [100.0, 100.0, 120.0, 90.0];
        double[] rates = [0.01, 0.02, 0.03, 0.04];
        double[] volatilities = [0.10, 0.20, 0.30, 0.40];
        double[] expiries = [0.25, 0.50, 0.75, 1.0];
        int[] optionTypes = [1, -1, 0, 2];
        var prices = new double[forwards.Length];
        var greeks = new Black76Result[forwards.Length];

        RustOptionModel.PriceBatch(
            forwards, strikes, rates, volatilities, expiries, optionTypes, prices);
        RustOptionModel.PriceWithGreeksBatch(
            forwards, strikes, rates, volatilities, expiries, optionTypes, greeks);

        for (var index = 0; index < forwards.Length; index++)
        {
            AssertClose(
                prices[index],
                OptionModel.PriceManaged(
                    forwards[index],
                    strikes[index],
                    rates[index],
                    volatilities[index],
                    expiries[index],
                    optionTypes[index]),
                5e-13);
            AssertResultClose(
                greeks[index],
                OptionModel.PriceWithGreeksManaged(
                    forwards[index],
                    strikes[index],
                    rates[index],
                    volatilities[index],
                    expiries[index],
                    optionTypes[index]),
                5e-13);
        }
    }

    [Fact]
    public void RustScalarPriceAndGreeksMatchManagedAcrossRandomizedDomain()
    {
        const int caseCount = 100_000;
        var random = new Random(0x4f5054);

        for (var index = 0; index < caseCount; index++)
        {
            var forward = Next(random, 10.0, 10_000.0);
            var strike = forward * Next(random, 0.50, 1.50);
            var rate = Next(random, -0.05, 0.25);
            var volatility = Next(random, 0.01, 2.0);
            var expiry = Next(random, 1.0 / 365.0, 10.0);
            var optionType = random.Next(2) == 0 ? -1 : 1;

            var managedPrice = OptionModel.PriceManaged(
                forward, strike, rate, volatility, expiry, optionType);
            var rustPrice = RustOptionModel.Price(
                forward, strike, rate, volatility, expiry, optionType);
            AssertCloseFast(rustPrice, managedPrice, 5e-12, index, "price");

            var managedGreeks = OptionModel.PriceWithGreeksManaged(
                forward, strike, rate, volatility, expiry, optionType);
            var rustGreeks = RustOptionModel.PriceWithGreeks(
                forward, strike, rate, volatility, expiry, optionType);
            AssertResultCloseFast(rustGreeks, managedGreeks, 5e-12, index);
        }
    }

    [Fact]
    public void RustImpliedVolatilityMatchesManagedAcrossRandomizedSolvableDomain()
    {
        const int caseCount = 5_000;
        var random = new Random(0x49564f4c);
        var convergedCount = 0;

        for (var index = 0; index < caseCount; index++)
        {
            var forward = Next(random, 50.0, 10_000.0);
            var strike = forward * Next(random, 0.80, 1.20);
            var rate = Next(random, -0.02, 0.12);
            var expectedVolatility = Next(random, 0.08, 0.80);
            var expiry = Next(random, 0.05, 3.0);
            var optionType = random.Next(2) == 0 ? -1 : 1;
            var marketPrice = OptionModel.PriceManaged(
                forward, strike, rate, expectedVolatility, expiry, optionType);

            var managed = OptionModel.ImpliedVolatilityManaged(
                forward, strike, rate, marketPrice, expiry, optionType, 1e-10, 100, null);
            var rust = RustOptionModel.ImpliedVolatility(
                forward, strike, rate, marketPrice, expiry, optionType, 1e-10, 100, null);

            AssertCloseFast(rust, managed, 5e-9, index, "implied volatility");
            if (!double.IsNaN(managed))
            {
                convergedCount++;
                var repriced = RustOptionModel.Price(
                    forward, strike, rate, rust, expiry, optionType);
                AssertCloseFast(repriced, marketPrice, 2e-9, index, "recovered market price");
            }
        }

        convergedCount.Should().BeGreaterThan(4_500);
    }

    [Fact]
    public void RustBatchOperationsMatchManagedAcrossRandomizedDomain()
    {
        const int caseCount = 4_096;
        var random = new Random(0x42415443);
        var forwards = new double[caseCount];
        var strikes = new double[caseCount];
        var rates = new double[caseCount];
        var volatilities = new double[caseCount];
        var expiries = new double[caseCount];
        var optionTypes = new int[caseCount];
        var prices = new double[caseCount];
        var greeks = new Black76Result[caseCount];

        for (var index = 0; index < caseCount; index++)
        {
            forwards[index] = Next(random, 10.0, 10_000.0);
            strikes[index] = forwards[index] * Next(random, 0.50, 1.50);
            rates[index] = Next(random, -0.05, 0.25);
            volatilities[index] = Next(random, 0.01, 2.0);
            expiries[index] = Next(random, 1.0 / 365.0, 10.0);
            optionTypes[index] = random.Next(2) == 0 ? -1 : 1;
        }

        RustOptionModel.PriceBatch(
            forwards, strikes, rates, volatilities, expiries, optionTypes, prices);
        RustOptionModel.PriceWithGreeksBatch(
            forwards, strikes, rates, volatilities, expiries, optionTypes, greeks);

        for (var index = 0; index < caseCount; index++)
        {
            AssertCloseFast(
                prices[index],
                OptionModel.PriceManaged(
                    forwards[index], strikes[index], rates[index], volatilities[index],
                    expiries[index], optionTypes[index]),
                5e-12,
                index,
                "batch price");
            AssertResultCloseFast(
                greeks[index],
                OptionModel.PriceWithGreeksManaged(
                    forwards[index], strikes[index], rates[index], volatilities[index],
                    expiries[index], optionTypes[index]),
                5e-12,
                index);
        }
    }

    [Fact]
    public void RustGreeksAgreeWithIndependentFiniteDifferences()
    {
        const int caseCount = 250;
        var random = new Random(0x47524545);

        for (var index = 0; index < caseCount; index++)
        {
            var forward = Next(random, 50.0, 5_000.0);
            var strike = forward * Next(random, 0.75, 1.25);
            var rate = Next(random, -0.02, 0.12);
            var volatility = Next(random, 0.08, 0.80);
            var expiry = Next(random, 0.10, 3.0);
            var optionType = random.Next(2) == 0 ? -1 : 1;
            var result = RustOptionModel.PriceWithGreeks(
                forward, strike, rate, volatility, expiry, optionType);

            var forwardStep = forward * 1e-4;
            var priceUp = RustOptionModel.Price(
                forward + forwardStep, strike, rate, volatility, expiry, optionType);
            var price = RustOptionModel.Price(
                forward, strike, rate, volatility, expiry, optionType);
            var priceDown = RustOptionModel.Price(
                forward - forwardStep, strike, rate, volatility, expiry, optionType);
            var numericalDelta = (priceUp - priceDown) / (2.0 * forwardStep);
            var numericalGamma = (priceUp - (2.0 * price) + priceDown)
                / (forwardStep * forwardStep);

            const double volatilityStep = 1e-5;
            var numericalVega = (
                RustOptionModel.Price(
                    forward, strike, rate, volatility + volatilityStep, expiry, optionType)
                - RustOptionModel.Price(
                    forward, strike, rate, volatility - volatilityStep, expiry, optionType))
                / (2.0 * volatilityStep);

            const double rateStep = 1e-6;
            var numericalRho = (
                RustOptionModel.Price(
                    forward, strike, rate + rateStep, volatility, expiry, optionType)
                - RustOptionModel.Price(
                    forward, strike, rate - rateStep, volatility, expiry, optionType))
                / (2.0 * rateStep);

            const double timeStep = 1e-5;
            var numericalTheta = -(
                RustOptionModel.Price(
                    forward, strike, rate, volatility, expiry + timeStep, optionType)
                - RustOptionModel.Price(
                    forward, strike, rate, volatility, expiry - timeStep, optionType))
                / (2.0 * timeStep);

            AssertCloseFast(result.Delta, numericalDelta, 2e-5, index, "finite delta");
            AssertCloseFast(result.Gamma, numericalGamma, 2e-3, index, "finite gamma");
            AssertCloseFast(result.Vega, numericalVega, 2e-5, index, "finite vega");
            AssertCloseFast(result.Theta, numericalTheta, 2e-5, index, "finite theta");
            AssertCloseFast(result.Rho, numericalRho, 2e-5, index, "finite rho");
        }
    }

    [Fact]
    public void RustGreeksBatchPreservesPartialWritesAtInvalidContract()
    {
        double[] forwards = [100.0, 0.0, 110.0];
        double[] values = [100.0, 100.0, 100.0];
        int[] optionTypes = [1, 1, 1];
        var sentinel = new Black76Result(1.0, 2.0, 3.0, 4.0, 5.0, 6.0);
        Black76Result[] results = [sentinel, sentinel, sentinel];

        var action = () => RustOptionModel.PriceWithGreeksBatch(
            forwards, values, values, values, values, optionTypes, results);

        action.Should().Throw<ArgumentOutOfRangeException>();
        results[0].Should().NotBe(sentinel);
        results[1].Should().Be(sentinel);
        results[2].Should().Be(sentinel);
    }

    [Fact]
    public void RustScalarAndBatchWrappersAllocateNoManagedMemoryAfterWarmup()
    {
        double[] values = [100.0, 105.0, 110.0, 115.0];
        int[] optionTypes = [1, -1, 1, -1];
        var prices = new double[values.Length];
        var greeks = new Black76Result[values.Length];

        _ = RustOptionModel.Price(100.0, 105.0, 0.04, 0.20, 1.0, 1);
        RustOptionModel.PriceBatch(values, values, values, values, values, optionTypes, prices);
        RustOptionModel.PriceWithGreeksBatch(
            values, values, values, values, values, optionTypes, greeks);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var iteration = 0; iteration < 1_000; iteration++)
        {
            _ = RustOptionModel.Price(100.0, 105.0, 0.04, 0.20, 1.0, 1);
            RustOptionModel.PriceBatch(values, values, values, values, values, optionTypes, prices);
            RustOptionModel.PriceWithGreeksBatch(
                values, values, values, values, values, optionTypes, greeks);
        }
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        allocated.Should().Be(0);
    }

    private static void AssertResultClose(
        in Black76Result actual,
        in Black76Result expected,
        double tolerance)
    {
        AssertClose(actual.Price, expected.Price, tolerance);
        AssertClose(actual.Delta, expected.Delta, tolerance);
        AssertClose(actual.Gamma, expected.Gamma, tolerance);
        AssertClose(actual.Vega, expected.Vega, tolerance);
        AssertClose(actual.Theta, expected.Theta, tolerance);
        AssertClose(actual.Rho, expected.Rho, tolerance);
    }

    private static void AssertClose(double actual, double expected, double tolerance)
    {
        if (double.IsNaN(expected))
        {
            actual.Should().BeNaN();
            return;
        }

        var scale = Math.Max(Math.Abs(expected), 1.0);
        Math.Abs(actual - expected).Should().BeLessThanOrEqualTo(tolerance * scale);
    }

    private static double Next(Random random, double minimum, double maximum) =>
        minimum + ((maximum - minimum) * random.NextDouble());

    private static void AssertResultCloseFast(
        in Black76Result actual,
        in Black76Result expected,
        double tolerance,
        int caseIndex)
    {
        AssertCloseFast(actual.Price, expected.Price, tolerance, caseIndex, "price");
        AssertCloseFast(actual.Delta, expected.Delta, tolerance, caseIndex, "delta");
        AssertCloseFast(actual.Gamma, expected.Gamma, tolerance, caseIndex, "gamma");
        AssertCloseFast(actual.Vega, expected.Vega, tolerance, caseIndex, "vega");
        AssertCloseFast(actual.Theta, expected.Theta, tolerance, caseIndex, "theta");
        AssertCloseFast(actual.Rho, expected.Rho, tolerance, caseIndex, "rho");
    }

    private static void AssertCloseFast(
        double actual,
        double expected,
        double tolerance,
        int caseIndex,
        string quantity)
    {
        if (double.IsNaN(expected) ? double.IsNaN(actual) :
            Math.Abs(actual - expected) <= tolerance * Math.Max(Math.Abs(expected), 1.0))
        {
            return;
        }

        throw new XunitException(
            $"Case {caseIndex} {quantity}: actual {actual:R}, expected {expected:R}, " +
            $"tolerance {tolerance:R}.");
    }
}
