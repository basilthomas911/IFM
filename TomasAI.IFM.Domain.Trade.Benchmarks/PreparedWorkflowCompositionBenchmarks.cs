using BenchmarkDotNet.Attributes;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Model;
using TomasAI.IFM.Framework.Serialization;

namespace TomasAI.IFM.Domain.Trade.Benchmarks;

[MemoryDiagnoser]
public class PreparedWorkflowCompositionBenchmarks
{
    [Params("LongFuture", "BearPutDebit", "ShortBalancedIronCondor")]
    public string Strategy { get; set; } = "LongFuture";
    ExecuteOrderCompositionPipelineCommand command = null!;
    TradeSelectionResult selected = null!;
    PreparedCompositionConfiguration configuration = null!;
    readonly OrderComposer model = new(new Black76ComposerPricer());

    [GlobalSetup]
    public void Setup()
    {
        var directory = Environment.GetEnvironmentVariable("IFM_PREPARED_WORKFLOW_BENCHMARK_FIXTURES")
            ?? throw new InvalidOperationException("Export workflow fixtures first.");
        command = MessagePackBinarySerializer.Shared.Deserialize<ExecuteOrderCompositionPipelineCommand>(
            File.ReadAllBytes(Path.Combine(directory, Strategy + "-composition.msgpack")))!;
        selected = TradeSelectionContracts.ReadResult(command.AcceptedSelectionEnvelope);
        configuration = new(command.SelectionBinding, selected.SelectedCandidate!);
        if (CompositionHash.Compute(ResolveAndCalculate()) != CompositionHash.Compute(BindPublishedAndCalculate()))
            throw new InvalidOperationException("Prepared composition differs from the original result.");
    }

    [Benchmark(Baseline = true)]
    public OrderCompositionResult ResolveAndCalculate() => model.Calculate(command with { CompositionBinding =
        CompositionBindingResolver.Resolve(selected, command.SelectionBinding, command.CompositionBinding.FrozenAtUtc) });

    [Benchmark]
    public OrderCompositionResult BindPublishedAndCalculate() => model.CalculatePrepared(command with { CompositionBinding =
        configuration.Bind(selected, command.SelectionBinding, command.CompositionBinding.FrozenAtUtc) }, configuration);
}
