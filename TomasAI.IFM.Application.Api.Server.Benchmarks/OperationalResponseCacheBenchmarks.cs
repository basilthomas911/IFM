using System.Text.Json;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using TomasAI.IFM.Application.Api.Server;

namespace TomasAI.IFM.Application.Api.Server.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net10_0, warmupCount: 3, iterationCount: 8)]
public class OperationalResponseCacheBenchmarks
{
    readonly TomasAI.IFM.Shared.EventModelActor.SupervisorRuntimeSnapshot snapshot =
        ActorHealthJsonBenchmarks.CreateSnapshot();
    byte[] cached = null!;

    [GlobalSetup]
    public void Setup() => cached = JsonSerializer.SerializeToUtf8Bytes(
        snapshot,
        ApiServerJsonContext.Default.SupervisorRuntimeSnapshot);

    [Benchmark(Baseline = true)]
    public byte[] SerializeSnapshot() => JsonSerializer.SerializeToUtf8Bytes(
        snapshot,
        ApiServerJsonContext.Default.SupervisorRuntimeSnapshot);

    [Benchmark]
    public byte[] ReadCachedResponse() => cached;
}
