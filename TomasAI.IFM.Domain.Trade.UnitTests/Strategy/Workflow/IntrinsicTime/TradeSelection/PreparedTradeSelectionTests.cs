using FluentAssertions;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection.Model;
using TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.OrderComposer;

namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.TradeSelection;

public sealed class PreparedTradeSelectionTests
{
    public static IEnumerable<object[]> Variants => CompositionFixture.Variants.Select(value => new object[] { value });

    [Theory]
    [MemberData(nameof(Variants))]
    public async Task Compiled_configuration_preserves_each_variants_decision_and_evidence(string variant)
    {
        var command = await TradeSelectionFixture.Command(variant);
        var prepared = new PreparedTradeSelectionConfiguration(command.SelectionBinding);
        var before = TradeSelectionEvaluator.Evaluate(command);
        var after = TradeSelectionEvaluator.EvaluatePrepared(command, prepared);
        TradeSelectionContracts.DecisionEvidenceHash(after).Should().Be(TradeSelectionContracts.DecisionEvidenceHash(before));
        after.Should().BeEquivalentTo(before);
    }
}
