using BenchmarkDotNet.Attributes;
using TomasAI.IFM.Framework.OptionPricer.Pricing;

namespace TomasAI.IFM.Domain.OptionPricer.Benchmarks;

[MemoryDiagnoser]
public class UnifiedPricingBenchmarks
{
    [Params(1, 2, 4, 64)] public int Count { get; set; }
    [Params(ExerciseKind.European, ExerciseKind.American)] public ExerciseKind Exercise { get; set; }
    private OptionCalculator calculator = null!;
    private OptionPricingRequest[] requests = null!;
    private double[] volatilities = null!;
    private PricingResult[] results = null!;
    private double[] marks = null!;
    private PriceDeltaResult[] deltas = null!;
    private ImpliedVolatilityResult[] implied = null!;
    [GlobalSetup]
    public void Setup()
    {
        calculator = new();
        requests = Enumerable.Range(0, Count).Select(i => new OptionPricingRequest(
            UnderlyingKind.Futures, Exercise, PremiumKind.PaidUpfront,
            i % 2 == 0 ? OptionSide.Call : OptionSide.Put, 5200, 5000 + i * 10, .1, .04)).ToArray();
        volatilities = Enumerable.Repeat(.2, Count).ToArray();
        results = new PricingResult[Count];
        calculator.PriceBatch(requests, volatilities, results);
        if (results.Any(r => !r.Success)) throw new InvalidOperationException("Benchmark inputs did not price.");
        marks = results.Select(r => r.Value!.Value.Price).ToArray();
        calculator.ImpliedVolatilityBatch(requests, marks, results);
        if (results.Any(r => !r.Success)) throw new InvalidOperationException("Benchmark inputs did not invert.");
        deltas = new PriceDeltaResult[Count];
        implied = new ImpliedVolatilityResult[Count];
        calculator.PriceAndDeltaBatch(requests, volatilities, deltas);
        calculator.SolveImpliedVolatilityBatch(requests, marks, implied);
        if (deltas.Any(r => !r.Success) || implied.Any(r => !r.Success))
            throw new InvalidOperationException("Fast-tier benchmark inputs failed.");
    }
    [Benchmark]
    public void BatchPriceAndGreeks() => calculator.PriceBatch(requests, volatilities, results);
    [Benchmark]
    public void BatchIvAndGreeks() => calculator.ImpliedVolatilityBatch(requests, marks, results);
    [Benchmark]
    public void BatchPriceAndDelta() => calculator.PriceAndDeltaBatch(requests, volatilities, deltas);
    [Benchmark]
    public void BatchIvOnly() => calculator.SolveImpliedVolatilityBatch(requests, marks, implied);
}
