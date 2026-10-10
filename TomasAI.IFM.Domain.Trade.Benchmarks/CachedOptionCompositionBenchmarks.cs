using BenchmarkDotNet.Attributes;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Model;
namespace TomasAI.IFM.Domain.Trade.Benchmarks;

/// <summary>Complete pure prepared composition: freshness, global policy, valuation reuse, enumeration, risk checks, ranking and evidence hashing.</summary>
[MemoryDiagnoser, ShortRunJob]
public class CachedOptionCompositionBenchmarks
{
    [Params("ShortBalancedIronCondor", "ShortBullishIronCondor", "ShortBearishIronCondor", "BullCallDebit", "BearPutDebit")]
    public string Strategy { get; set; } = "";
    ExecuteOrderCompositionPipelineCommand command = null!;
    readonly OrderComposer composer = new(new Black76ComposerPricer());
    /// <summary>Loads a fixture exported only after successful cache-to-composer integration. Setup IO is excluded.</summary>
    [GlobalSetup]
    public void Setup()
    {
        var path = Environment.GetEnvironmentVariable("IFM_COMPOSITION_BENCHMARK_FIXTURES")
            ?? throw new InvalidOperationException("Run CachedCompositionIntegrationTests with IFM_COMPOSITION_BENCHMARK_FIXTURES set first.");
        command = System.Text.Json.JsonSerializer.Deserialize<ExecuteOrderCompositionPipelineCommand>(File.ReadAllText(Path.Combine(path, Strategy + ".json")))!;
        if (composer.Calculate(command).Candidate is null) throw new InvalidOperationException("A successful candidate is required for this benchmark.");
    }
    /// <summary>Excludes feed acquisition, accepted-input persistence and broker/financial actor latency.</summary>
    [Benchmark]
    public OrderCompositionResult CompletePreparedComposition() => composer.Calculate(command);
}
