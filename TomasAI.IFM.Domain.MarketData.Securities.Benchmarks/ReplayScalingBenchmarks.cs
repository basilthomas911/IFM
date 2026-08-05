using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Newtonsoft.Json;

namespace TomasAI.IFM.Domain.MarketData.Securities.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net10_0, warmupCount: 3, iterationCount: 8)]
public class ReplayScalingBenchmarks
{
    string[] _serializedSnapshots = [];

    [Params(8, 128, 1024)]
    public int SnapshotCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _serializedSnapshots = new string[SnapshotCount];
        for (var snapshot = 0; snapshot < SnapshotCount; snapshot++)
        {
            var ids = Enumerable.Range(0, 32)
                .Select(index => $"ES{snapshot:D4}C{5000 + index}")
                .ToArray();
            _serializedSnapshots[snapshot] = JsonConvert.SerializeObject(new BulkSnapshot(snapshot, ids));
        }
    }

    [Benchmark(Baseline = true)]
    public int BeforeFullBulkHistoryReplay()
    {
        var state = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < _serializedSnapshots.Length; index++)
        {
            var snapshot = JsonConvert.DeserializeObject<BulkSnapshot>(_serializedSnapshots[index])!;
            foreach (var contractId in snapshot.ContractIds)
                state.Add(contractId);
        }
        return state.Count;
    }

    [Benchmark]
    public int AfterLatestBulkSnapshotReplay()
    {
        var state = new HashSet<string>(StringComparer.Ordinal);
        var snapshot = JsonConvert.DeserializeObject<BulkSnapshot>(_serializedSnapshots[^1])!;
        foreach (var contractId in snapshot.ContractIds)
            state.Add(contractId);
        return state.Count;
    }

    sealed record BulkSnapshot(int Version, string[] ContractIds);
}
