using BenchmarkDotNet.Attributes;
using TomasAI.IFM.Application.Storage.CommandDeduplication;

namespace TomasAI.IFM.Framework.Storage.Benchmarks;

/// <summary>
/// Process-local duplicate fast-path measurements. Database-backed cold and cross-process paths remain in
/// <see cref="CommandLogProviderBenchmarks"/>.
/// </summary>
[MemoryDiagnoser]
[InProcess]
[WarmupCount(2)]
[IterationCount(10)]
public class CommandDuplicateCoordinatorBenchmarks
{
    readonly CommandDuplicateCoordinator _coordinator = new(100_000);
    readonly Guid _completedCommandId = Guid.NewGuid();

    [Params(1, 16, 32)]
    public int ConcurrentRequests { get; set; }

    [GlobalSetup]
    public async Task GlobalSetup()
        => _ = await _coordinator.TryAcceptAsync(
            _completedCommandId,
            static _ => Task.FromResult(true));

    [Benchmark(Description = "L1 completed-id duplicate shortcut")]
    public async Task<int> CompletedIdShortcut()
    {
        var results = await Task.WhenAll(
            Enumerable.Range(0, ConcurrentRequests)
                .Select(_ => _coordinator.TryAcceptAsync(
                    _completedCommandId,
                    static _ => throw new InvalidOperationException("A hot duplicate must not reach storage."))
                    .AsTask()));
        return results.Count(accepted => accepted);
    }

    [Benchmark(Description = "L1 same-id reservation plus local duplicates")]
    public async Task<int> SameIdCoalescing()
    {
        var commandId = Guid.NewGuid();
        var results = await Task.WhenAll(
            Enumerable.Range(0, ConcurrentRequests)
                .Select(_ => _coordinator.TryAcceptAsync(
                    commandId,
                    static _ => Task.FromResult(true))
                    .AsTask()));
        return results.Count(accepted => accepted);
    }
}
