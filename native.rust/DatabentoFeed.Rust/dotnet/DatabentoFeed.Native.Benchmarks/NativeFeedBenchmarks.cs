using BenchmarkDotNet.Attributes;
using DatabentoFeed.Native.Interop;

[MemoryDiagnoser]
[MarkdownExporterAttribute.GitHub]
[InProcess]
[WarmupCount(1)]
[IterationCount(5)]
[InvocationCount(1)]
public class NativeFeedBenchmarks
{
    private NativeApi _api = null!;

    [Params("Cpp", "Rust")]
    public string Implementation { get; set; } = null!;

    [Params(64u, 512u, 4096u)]
    public uint BatchSize { get; set; }

    [GlobalSetup]
    public void Setup()
        => _api = BenchmarkNativeApi.Load(Implementation);

    [GlobalCleanup]
    public void Cleanup() => _api.Dispose();

    [Benchmark(OperationsPerInvoke = 10_000)]
    public ulong ProduceAndConsumeSyntheticRecords()
    {
        SyntheticRun run = SyntheticFeedRunner.Run(_api, 10_000, BatchSize, captureRecords: false);
        return run.Stats.RecordsConsumed;
    }
}
