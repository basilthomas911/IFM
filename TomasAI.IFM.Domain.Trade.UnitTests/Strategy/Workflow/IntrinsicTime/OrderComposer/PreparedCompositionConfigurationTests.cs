using FluentAssertions;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Model;

namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.OrderComposer;

public sealed class PreparedCompositionConfigurationTests
{
    public static IEnumerable<object[]> Variants => CompositionFixture.Variants.Select(value => new object[] { value });

    [Theory, MemberData(nameof(Variants))]
    public async Task Published_configuration_preserves_binding_hash_and_the_complete_composition_result(string variant)
    {
        var command = await CompositionFixture.Command(variant);
        var selected = TradeSelectionContracts.ReadResult(command.AcceptedSelectionEnvelope);
        var prepared = new PreparedCompositionConfiguration(command.SelectionBinding, selected.SelectedCandidate!);
        var binding = prepared.Bind(selected, command.SelectionBinding, command.CompositionBinding.FrozenAtUtc);
        binding.Should().BeEquivalentTo(command.CompositionBinding);
        var model = new TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Model.OrderComposer(new Black76ComposerPricer());

        model.CalculatePrepared(command, prepared).Should().BeEquivalentTo(model.Calculate(command));

        var parameters = binding.Rules.VariantRules.Single(value => value.VariantKey == selected.SelectedCandidate!.VariantKey).BaseParameters;
        Action relax = () => prepared.ValidateResolved(command, parameters with { BalanceTolerance = 999 });
        relax.Should().Throw<Exception>().WithMessage("*OC.CONFIG.RULE_INVALID*");
    }
}
