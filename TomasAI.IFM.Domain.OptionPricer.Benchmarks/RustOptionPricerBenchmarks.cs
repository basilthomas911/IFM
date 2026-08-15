using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using TomasAI.IFM.Framework.OptionPricer.Black76;
using TomasAI.IFM.Framework.OptionPricer.Interop;

namespace TomasAI.IFM.Domain.OptionPricer.Benchmarks;

[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[SimpleJob(RuntimeMoniker.Net10_0, warmupCount: 1, iterationCount: 3)]
public class RustOptionPricerScalarBenchmarks
{
    private const double Forward = 5_300.0;
    private const double Strike = 5_250.0;
    private const double Rate = 0.045;
    private const double Volatility = 0.18;
    private const double Expiry = 0.25;
    private static readonly double MarketPrice = OptionModel.PriceManaged(
        Forward, Strike, Rate, Volatility, Expiry, 1);

    [Benchmark(Baseline = true), BenchmarkCategory("Price")]
    public double ManagedPrice() =>
        OptionModel.PriceManaged(Forward, Strike, Rate, Volatility, Expiry, 1);

    [Benchmark, BenchmarkCategory("Price")]
    public double RustPrice() =>
        RustOptionModel.Price(Forward, Strike, Rate, Volatility, Expiry, 1);

    [Benchmark(Baseline = true), BenchmarkCategory("Greeks")]
    public Black76Result ManagedGreeks() =>
        OptionModel.PriceWithGreeksManaged(Forward, Strike, Rate, Volatility, Expiry, 1);

    [Benchmark, BenchmarkCategory("Greeks")]
    public Black76Result RustGreeks() =>
        RustOptionModel.PriceWithGreeks(Forward, Strike, Rate, Volatility, Expiry, 1);

    [Benchmark(Baseline = true), BenchmarkCategory("ImpliedVolatility")]
    public double ManagedImpliedVolatility() =>
        OptionModel.ImpliedVolatilityManaged(
            Forward, Strike, Rate, MarketPrice, Expiry, 1, 1e-10, 100, null);

    [Benchmark, BenchmarkCategory("ImpliedVolatility")]
    public double RustImpliedVolatility() =>
        RustOptionModel.ImpliedVolatility(
            Forward, Strike, Rate, MarketPrice, Expiry, 1, 1e-10, 100, null);

    [Benchmark(Baseline = true), BenchmarkCategory("Fused")]
    public Black76Result ManagedFusedImpliedVolatilityAndGreeks()
    {
        var impliedVolatility = OptionModel.ImpliedVolatilityManaged(
            Forward, Strike, Rate, MarketPrice, Expiry, 1, 1e-10, 100, null);
        return OptionModel.PriceWithGreeksManaged(
            Forward, Strike, Rate, impliedVolatility, Expiry, 1);
    }

    [Benchmark, BenchmarkCategory("Fused")]
    public Black76Result RustFusedImpliedVolatilityAndGreeks()
    {
        RustOptionModel.TryImpliedVolatilityWithGreeks(
            Forward, Strike, Rate, MarketPrice, Expiry, 1, 1e-10, 100, null,
            out _, out var result);
        return result;
    }
}

[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[SimpleJob(RuntimeMoniker.Net10_0, warmupCount: 1, iterationCount: 3)]
public class RustOptionPricerBatchBenchmarks
{
    private double[] _forwards = null!;
    private double[] _strikes = null!;
    private double[] _rates = null!;
    private double[] _volatilities = null!;
    private double[] _expiries = null!;
    private int[] _optionTypes = null!;
    private double[] _prices = null!;
    private Black76Result[] _greeks = null!;

    [Params(1, 16, 256, 4_096, 16_384)]
    public int Count { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _forwards = new double[Count];
        _strikes = new double[Count];
        _rates = new double[Count];
        _volatilities = new double[Count];
        _expiries = new double[Count];
        _optionTypes = new int[Count];
        _prices = new double[Count];
        _greeks = new Black76Result[Count];

        for (var index = 0; index < Count; index++)
        {
            _forwards[index] = 4_500.0 + (index % 1_000);
            _strikes[index] = 4_400.0 + (index % 1_200);
            _rates[index] = 0.02 + ((index % 10) * 0.0025);
            _volatilities[index] = 0.10 + ((index % 20) * 0.01);
            _expiries[index] = 0.05 + ((index % 50) * 0.02);
            _optionTypes[index] = (index & 1) == 0 ? 1 : -1;
        }
    }

    [Benchmark(Baseline = true), BenchmarkCategory("PriceBatch")]
    public void ManagedPriceBatch()
    {
        for (var index = 0; index < Count; index++)
        {
            _prices[index] = OptionModel.PriceManaged(
                _forwards[index], _strikes[index], _rates[index], _volatilities[index],
                _expiries[index], _optionTypes[index]);
        }
    }

    [Benchmark, BenchmarkCategory("PriceBatch")]
    public void RustPriceBatch() => RustOptionModel.PriceBatch(
        _forwards, _strikes, _rates, _volatilities, _expiries, _optionTypes, _prices);

    [Benchmark(Baseline = true), BenchmarkCategory("GreeksBatch")]
    public void ManagedGreeksBatch()
    {
        for (var index = 0; index < Count; index++)
        {
            _greeks[index] = OptionModel.PriceWithGreeksManaged(
                _forwards[index], _strikes[index], _rates[index], _volatilities[index],
                _expiries[index], _optionTypes[index]);
        }
    }

    [Benchmark, BenchmarkCategory("GreeksBatch")]
    public void RustGreeksBatch() => RustOptionModel.PriceWithGreeksBatch(
        _forwards, _strikes, _rates, _volatilities, _expiries, _optionTypes, _greeks);
}
