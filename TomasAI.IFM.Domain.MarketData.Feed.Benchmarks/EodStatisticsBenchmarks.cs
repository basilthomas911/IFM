using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using MathNet.Numerics.Distributions;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Feed.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net10_0, warmupCount: 3, iterationCount: 8)]
public class EodStatisticsBenchmarks
{
    FuturesEodDataV2ReadModel[] _data = [];

    [Params(20, 50, 200, 1000)]
    public int WindowSize { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _data = new FuturesEodDataV2ReadModel[WindowSize];
        for (var index = 0; index < _data.Length; index++)
        {
            _data[index] = new FuturesEodDataV2ReadModel
            {
                ContractId = "ESU6",
                ValueDate = new DateOnly(2026, 8, 5).AddDays(-index),
                ClosePrice = 6300m + index % 17 - index * 0.05m
            };
        }
    }

    [Benchmark(Baseline = true)]
    public double BeforeMathNetEstimate()
        => Normal.Estimate(_data.Select(static value => (double)value.ClosePrice).Take(WindowSize)).StdDev;

    [Benchmark]
    public double AfterSinglePass()
        => new StdDevCalculator(WindowSize, _data, static value => (double)value.ClosePrice).StdDev;
}
