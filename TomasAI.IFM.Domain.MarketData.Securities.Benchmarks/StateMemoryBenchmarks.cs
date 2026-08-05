using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

namespace TomasAI.IFM.Domain.MarketData.Securities.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net10_0, warmupCount: 3, iterationCount: 8)]
public class StateMemoryBenchmarks
{
    string[] _contractIds = [];

    [Params(32, 512, 4096)]
    public int Count { get; set; }

    [GlobalSetup]
    public void Setup()
        => _contractIds = Enumerable.Range(0, Count)
            .Select(static index => $"ES20261218C{5000 + index}")
            .ToArray();

    [Benchmark(Baseline = true)]
    public int BeforeFullContractDictionary()
    {
        var state = new Dictionary<string, BeforeContract>(StringComparer.Ordinal);
        foreach (var contractId in _contractIds)
        {
            if (state.ContainsKey(contractId))
                state.Remove(contractId);
            state.Add(contractId, new BeforeContract(
                contractId,
                "ES",
                "CME",
                "USD",
                "50",
                new DateOnly(2026, 12, 18),
                5000));
        }
        return state.Count;
    }

    [Benchmark]
    public int AfterIdentifierSet()
    {
        var state = new HashSet<string>(StringComparer.Ordinal);
        foreach (var contractId in _contractIds)
            state.Add(contractId);
        return state.Count;
    }

    sealed record BeforeContract(
        string ContractId,
        string Symbol,
        string Exchange,
        string Currency,
        string Multiplier,
        DateOnly ContractMonth,
        double StrikePrice);
}
