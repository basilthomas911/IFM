using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using MathNet.Numerics.Distributions;
using TomasAI.IFM.Domain.Fund.Query;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Fund.Benchmarks;

[MemoryDiagnoser]
[ThreadingDiagnoser]
[SimpleJob(RuntimeMoniker.Net10_0, warmupCount: 3, iterationCount: 8)]
public class FundSharpeRatioBenchmarks
{
    List<FundDailyBalanceReadModel> _balances = null!;

    [Params(32, 256, 2048)]
    public int Count { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _balances = new List<FundDailyBalanceReadModel>(Count);
        var balance = 100_000m;
        for (var index = 0; index < Count; index++)
        {
            balance += index % 3 == 0 ? 13m : -5m;
            _balances.Add(new FundDailyBalanceReadModel(1, new DateOnly(2020, 1, 1).AddDays(index), balance));
        }
    }

    [Benchmark(Baseline = true)]
    public double Before_ListAndMathNet()
    {
        List<double> dailyReturns = [];
        for (var index = 0; index < _balances.Count - 1; index++)
        {
            var currentBalance = Convert.ToDouble(_balances.ElementAt(index).Balance);
            var previousBalance = Convert.ToDouble(_balances.ElementAt(index + 1).Balance);
            dailyReturns.Add((currentBalance - previousBalance) / previousBalance);
        }

        var distribution = Normal.Estimate(dailyReturns);
        return distribution.StdDev > 0
            ? distribution.Mean / distribution.StdDev * Math.Sqrt(252)
            : 0;
    }

    [Benchmark]
    public double After_SinglePassMoments()
        => FundQueryCalculations.CalculateSharpeRatio(_balances);
}

[MemoryDiagnoser]
[ThreadingDiagnoser]
[SimpleJob(RuntimeMoniker.Net10_0, warmupCount: 3, iterationCount: 8)]
public class FundQueryFanOutBenchmarks
{
    [Benchmark(Baseline = true)]
    public async Task<int> Before_SevenSequentialReads()
    {
        var result = 0;
        for (var index = 0; index < 7; index++)
            result += await SimulatedReadAsync().ConfigureAwait(false);
        return result;
    }

    [Benchmark]
    public async Task<int> After_SixConcurrentReads()
    {
        var reads = new Task<int>[6];
        for (var index = 0; index < reads.Length; index++)
            reads[index] = SimulatedReadAsync();
        var results = await Task.WhenAll(reads).ConfigureAwait(false);

        var result = 0;
        for (var index = 0; index < results.Length; index++)
            result += results[index];
        return result;
    }

    static async Task<int> SimulatedReadAsync()
    {
        await Task.Delay(1).ConfigureAwait(false);
        return 1;
    }
}

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net10_0, warmupCount: 3, iterationCount: 8)]
public class FundStateProbeBenchmarks
{
    Dictionary<int, int> _values = null!;

    [Params(32, 256, 2048)]
    public int Count { get; set; }

    [GlobalSetup]
    public void Setup()
        => _values = Enumerable.Range(1, Count).ToDictionary(static value => value);

    [Benchmark(Baseline = true)]
    public int Before_ContainsThenIndexer()
        => _values.ContainsKey(Count) ? _values[Count] : 0;

    [Benchmark]
    public int After_TryGetValue()
        => _values.TryGetValue(Count, out var value) ? value : 0;
}

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net10_0, warmupCount: 3, iterationCount: 8)]
public class FundBatchMaterializationBenchmarks
{
    decimal[] _amounts = null!;

    [Params(32, 256)]
    public int Count { get; set; }

    [GlobalSetup]
    public void Setup() => _amounts = Enumerable.Repeat(1m, Count).ToArray();

    [Benchmark(Baseline = true)]
    public decimal[] Before_IteratorMaterialization()
        => [.. EnumerateBalances(_amounts, 100m)];

    [Benchmark]
    public decimal[] After_ExactArrayLoop()
    {
        var result = new decimal[_amounts.Length];
        var balance = 100m;
        for (var index = 0; index < _amounts.Length; index++)
        {
            balance += _amounts[index];
            result[index] = balance;
        }
        return result;
    }

    static IEnumerable<decimal> EnumerateBalances(decimal[] amounts, decimal balance)
    {
        for (var index = 0; index < amounts.Length; index++)
        {
            balance += amounts[index];
            yield return balance;
        }
    }
}
