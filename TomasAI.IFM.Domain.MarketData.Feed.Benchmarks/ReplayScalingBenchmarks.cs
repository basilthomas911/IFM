using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Newtonsoft.Json;

namespace TomasAI.IFM.Domain.MarketData.Feed.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net10_0, warmupCount: 3, iterationCount: 8)]
public class ReplayScalingBenchmarks
{
    string[] _serializedEvents = [];

    [Params(256, 4096, 32768)]
    public int EventCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _serializedEvents = new string[EventCount + 1];
        _serializedEvents[0] = JsonConvert.SerializeObject(new ReplayEvent(0, "Snapshot"));
        for (var index = 1; index < _serializedEvents.Length; index++)
            _serializedEvents[index] = JsonConvert.SerializeObject(new ReplayEvent(index, "Insert"));
    }

    [Benchmark(Baseline = true)]
    public long BeforeSnapshotToEnd()
    {
        var total = 0L;
        for (var index = 0; index < _serializedEvents.Length; index++)
            total += JsonConvert.DeserializeObject<ReplayEvent>(_serializedEvents[index])!.Position;
        return total;
    }

    [Benchmark]
    public long AfterSnapshotOnly()
        => JsonConvert.DeserializeObject<ReplayEvent>(_serializedEvents[0])!.Position;

    sealed record ReplayEvent(long Position, string EventType);
}
