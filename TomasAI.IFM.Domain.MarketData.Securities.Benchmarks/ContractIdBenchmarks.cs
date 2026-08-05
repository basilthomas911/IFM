using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using TomasAI.IFM.Domain.MarketData.Securities.FuturesOptionContract.Command.Model;

namespace TomasAI.IFM.Domain.MarketData.Securities.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net10_0, warmupCount: 3, iterationCount: 8)]
public class ContractIdBenchmarks
{
    readonly BeforeContract _before = new("ES", new DateOnly(2026, 12, 18), "Call", 5000);
    readonly FuturesOptionSecuritiesContract _after = new(
        "E-mini option",
        "ES",
        "ES 26Dec18 C5000",
        "FOP",
        "USD",
        "CME",
        "50",
        new DateOnly(2026, 12, 18),
        5000,
        "Call");

    [Benchmark(Baseline = true)]
    public string BeforeComputedEveryAccess() => _before.ContractId;

    [Benchmark]
    public string AfterCachedAtConstruction() => _after.ContractId;

    sealed class BeforeContract(string symbol, DateOnly month, string optionType, double strike)
    {
        public string ContractId => $"{symbol}{month:yyyyMMdd}{optionType.Substring(0, 1)}{strike:####}";
    }
}
