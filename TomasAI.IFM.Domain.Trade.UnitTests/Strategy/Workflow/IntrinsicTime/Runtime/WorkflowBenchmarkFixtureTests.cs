using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.TradeSelection;
using TomasAI.IFM.Framework.Serialization;

namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.Runtime;

public sealed class WorkflowBenchmarkFixtureTests
{
    [Fact]
    public async Task Export_qualified_prepared_selection_inputs_when_requested()
    {
        var directory = Environment.GetEnvironmentVariable("IFM_PREPARED_WORKFLOW_BENCHMARK_FIXTURES");
        if (string.IsNullOrWhiteSpace(directory)) return;
        Directory.CreateDirectory(directory);
        foreach (var (variant, horizon) in new[] { ("LongFuture", TimeFrameType.Daily),
            ("BearPutDebit", TimeFrameType.Weekly), ("ShortBalancedIronCondor", TimeFrameType.Monthly) })
        {
            var command = await TradeSelectionFixture.Command(variant, horizon);
            var compiled = new TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection.Model.PreparedTradeSelectionConfiguration(command.SelectionBinding);
            var original = TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection.Model.TradeSelectionEvaluator.Evaluate(command);
            var prepared = TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection.Model.TradeSelectionEvaluator.EvaluatePrepared(command, compiled);
            Assert.Equal(original.Outcome, prepared.Outcome);
            Assert.Equal(original.PrimaryReasonCode, prepared.PrimaryReasonCode);
            await File.WriteAllBytesAsync(Path.Combine(directory, variant + ".msgpack"), MessagePackBinarySerializer.Shared.Serialize(command));
            var composition = await OrderComposer.CompositionFixture.Command(variant, horizon);
            await File.WriteAllBytesAsync(Path.Combine(directory, variant + "-composition.msgpack"), MessagePackBinarySerializer.Shared.Serialize(composition));
        }
    }
}
