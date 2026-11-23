using BenchmarkDotNet.Attributes;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection.Model;
using TomasAI.IFM.Framework.Serialization;

namespace TomasAI.IFM.Domain.Trade.Benchmarks;

/// <summary>Pure calculation comparison; excludes admission, persistence, cache production and financial commitment.</summary>
[MemoryDiagnoser]
public class PreparedWorkflowSelectionBenchmarks
{
    [Params("LongFuture", "BearPutDebit", "ShortBalancedIronCondor")]
    public string Strategy { get; set; } = "LongFuture";
    ExecuteTradeSelectionPipelineCommand command = null!;
    PreparedTradeSelectionConfiguration configuration = null!;

    [GlobalSetup]
    public void Setup()
    {
        var directory = Environment.GetEnvironmentVariable("IFM_PREPARED_WORKFLOW_BENCHMARK_FIXTURES")
            ?? throw new InvalidOperationException("Export qualified inputs using WorkflowBenchmarkFixtureTests first.");
        command = MessagePackBinarySerializer.Shared.Deserialize<ExecuteTradeSelectionPipelineCommand>(
            File.ReadAllBytes(Path.Combine(directory, Strategy + ".msgpack")))!;
        configuration = new(command.SelectionBinding);
        var before = TradeSelectionEvaluator.Evaluate(command);
        var after = TradeSelectionEvaluator.EvaluatePrepared(command, configuration);
        if (TradeSelectionContracts.DecisionEvidenceHash(before) != TradeSelectionContracts.DecisionEvidenceHash(after))
            throw new InvalidOperationException("Prepared calculation differs from the original decision.");
    }

    [Benchmark(Baseline = true)]
    public TradeSelectionResult ValidateAndCalculate() => TradeSelectionEvaluator.Evaluate(command);

    [Benchmark]
    public TradeSelectionResult CalculatePublishedInputs() => TradeSelectionEvaluator.EvaluatePrepared(command, configuration);
}
