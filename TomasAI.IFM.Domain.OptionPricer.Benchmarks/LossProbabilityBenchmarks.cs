using BenchmarkDotNet.Attributes;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Framework.OptionPricer.Black76;

namespace TomasAI.IFM.Domain.OptionPricer.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 8)]
public class LossProbabilityBenchmarks
{
    List<double> _put = default!;
    List<double> _call = default!;
    LossProbability _calculator = default!;

    [Params(256, 4096)]
    public int PathCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var random = new Random(84);
        _put = Enumerable.Range(0, PathCount).Select(_ => 25 + random.NextDouble() * 20).ToList();
        _call = Enumerable.Range(0, PathCount).Select(_ => 20 + random.NextDouble() * 25).ToList();
        _calculator = new LossProbability(_put, _call, -100_000);
    }

    [Benchmark(Baseline = true)]
    public double LegacyThreeListPipeline()
    {
        var putPnl = _calculator.GetExpectedPnlValues(OptionType.Put, 2, 50, 10).ToList();
        var callPnl = _calculator.GetExpectedPnlValues(OptionType.Call, 2, 50, 11).ToList();
        return _calculator.ToViewModel(putPnl, callPnl).Value;
    }

    [Benchmark]
    public double FusedPooledPipeline()
        => _calculator.Calculate(2, 50, 10, 11).Value;
}
