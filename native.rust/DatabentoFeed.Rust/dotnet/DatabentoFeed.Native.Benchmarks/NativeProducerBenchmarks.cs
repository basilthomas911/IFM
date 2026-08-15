using BenchmarkDotNet.Attributes;
using DatabentoFeed.Native.Interop;

[MemoryDiagnoser]
[InProcess]
[WarmupCount(2)]
[IterationCount(10)]
[InvocationCount(1)]
public class NativeProducerBenchmarks
{
    private const int RecordCount = 4_000_000;
    private NativeApi _api = null!;
    private PreparedSyntheticFeed _feed = null!;

    [Params("Cpp", "Rust")]
    public string Implementation { get; set; } = null!;

    [GlobalSetup]
    public void GlobalSetup() => _api = BenchmarkNativeApi.Load(Implementation);

    [IterationSetup]
    public void IterationSetup() => _feed = new(_api, RecordCount, 4096);

    [Benchmark(OperationsPerInvoke = RecordCount)]
    public ulong PublishSyntheticRecords() => _feed.PublishAll();

    [IterationCleanup]
    public void IterationCleanup() => _feed.Dispose();

    [GlobalCleanup]
    public void GlobalCleanup() => _api.Dispose();
}
