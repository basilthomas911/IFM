using BenchmarkDotNet.Attributes;
using TomasAI.IFM.Domain.OptionPricer.Shared;
using TomasAI.IFM.Domain.Trade.Shared;

namespace TomasAI.IFM.Domain.OptionPricer.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 8)]
public class SpreadCalculationBenchmarks
{
    OptionSpreadResult _result = default!;
    ProbabilityValueCollection _optimized = default!;

    [Params(256, 4096)]
    public int PathCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var random = new Random(42);
        _result = new OptionSpreadResult(0, 30, 5000, 0.04, 0.04, 4900, 0.2, 4850, 0.18);
        _result.ShortValues.Add(Enumerable.Range(0, PathCount).Select(_ => random.NextDouble() * 100).ToArray());
        _result.LongValues.Add(Enumerable.Range(0, PathCount).Select(_ => random.NextDouble() * 80).ToArray());
        _optimized = new ProbabilityValueCollection([_result]);
    }

    [Benchmark(Baseline = true)]
    public double LegacyRepeatedMaterialization()
    {
        var first = LegacySpreadValues(_result);
        var second = LegacySpreadValues(_result);
        return first.Average() + second[second.Count / 2];
    }

    [Benchmark]
    public double CachedMaterialization()
    {
        var first = _optimized.Values;
        var second = _optimized.Values;
        var total = 0.0;
        for (var index = 0; index < first.Count; index++)
            total += first[index];
        return (total / first.Count) + second[second.Count / 2];
    }

    static List<double> LegacySpreadValues(OptionSpreadResult result)
    {
        static IEnumerable<double> Normalize(List<double[]> values)
        {
            foreach (var chunk in values)
                foreach (var value in chunk)
                    yield return !double.IsFinite(value) || value < 0.000001 ? 0 : value;
        }

        var shortValues = Normalize(result.ShortValues).OrderBy(static value => value).ToList();
        var longValues = Normalize(result.LongValues).OrderBy(static value => value).ToList();
        var spreads = new List<double>(shortValues.Count);
        for (var index = 0; index < shortValues.Count; index++)
            spreads.Add(shortValues[index] - longValues[index]);
        return spreads;
    }
}
