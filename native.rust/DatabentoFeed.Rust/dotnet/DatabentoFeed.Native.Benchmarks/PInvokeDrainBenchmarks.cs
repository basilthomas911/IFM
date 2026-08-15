using BenchmarkDotNet.Attributes;
using DatabentoFeed.Native.Interop;

[MemoryDiagnoser]
[InProcess]
[WarmupCount(2)]
[IterationCount(10)]
[InvocationCount(1)]
public class PInvokeDrainBenchmarks
{
    private const int RecordCount = 1_000_000;
    private NativeApi _api = null!;
    private PreparedSyntheticFeed _feed = null!;

    [Params("Cpp", "Rust")]
    public string Implementation { get; set; } = null!;

    [Params(64u, 512u, 4096u)]
    public uint BatchSize { get; set; }

    [GlobalSetup]
    public void GlobalSetup() => _api = BenchmarkNativeApi.Load(Implementation);

    [IterationSetup]
    public void IterationSetup()
    {
        _feed = new(_api, RecordCount, BatchSize);
        _feed.Prefill();
    }

    [Benchmark(OperationsPerInvoke = RecordCount)]
    public ulong WaitAndReadSyntheticRecords() => _feed.DrainAll();

    [IterationCleanup]
    public void IterationCleanup() => _feed.Dispose();

    [GlobalCleanup]
    public void GlobalCleanup() => _api.Dispose();
}
