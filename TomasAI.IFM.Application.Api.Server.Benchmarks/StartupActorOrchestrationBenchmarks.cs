using BenchmarkDotNet.Attributes;

namespace TomasAI.IFM.Application.Api.Server.Benchmarks;

[MemoryDiagnoser]
[InProcess]
[WarmupCount(3)]
[IterationCount(8)]
[InvocationCount(1)]
public class ConsumerStartupOrchestrationBenchmarks
{
    const int ConsumerCount = 8;
    static readonly TimeSpan SimulatedStartupLatency = TimeSpan.FromMilliseconds(5);

    [Benchmark(Baseline = true, Description = "Before: serial consumer startup")]
    public async Task StartConsumersSerially()
    {
        for (var index = 0; index < ConsumerCount; index++)
            await StartIndependentComponentAsync().ConfigureAwait(false);
    }

    [Benchmark(Description = "After: concurrent consumer startup")]
    public Task StartConsumersConcurrently() =>
        Task.WhenAll(Enumerable.Range(0, ConsumerCount).Select(_ => StartIndependentComponentAsync()));

    static Task StartIndependentComponentAsync() => Task.Delay(SimulatedStartupLatency);
}

[MemoryDiagnoser]
[InProcess]
[WarmupCount(3)]
[IterationCount(8)]
[InvocationCount(1)]
public class SchemaStartupOrchestrationBenchmarks
{
    const int SchemaCount = 13;
    const int MaximumConcurrency = 4;
    static readonly TimeSpan SimulatedStartupLatency = TimeSpan.FromMilliseconds(5);
    static readonly int[] Schemas = Enumerable.Range(0, SchemaCount).ToArray();

    [Benchmark(Baseline = true, Description = "Before: serial schema initialization")]
    public async Task InitializeSchemasSerially()
    {
        foreach (var _ in Schemas)
            await InitializeIndependentSchemaAsync().ConfigureAwait(false);
    }

    [Benchmark(Description = "After: bounded schema initialization")]
    public Task InitializeSchemasWithBoundedConcurrency() =>
        Parallel.ForEachAsync(
            Schemas,
            new ParallelOptions { MaxDegreeOfParallelism = MaximumConcurrency },
            static async (_, _) => await InitializeIndependentSchemaAsync().ConfigureAwait(false));

    static Task InitializeIndependentSchemaAsync() => Task.Delay(SimulatedStartupLatency);
}
