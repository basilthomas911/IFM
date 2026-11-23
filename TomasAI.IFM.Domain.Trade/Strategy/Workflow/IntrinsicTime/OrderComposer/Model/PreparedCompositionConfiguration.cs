using System.Text.Json;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
using static TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition.CompositionRulesContract;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Model;

/// <summary>Producer-validated static constraints; binding a selected intent never parses configuration.</summary>
public sealed class PreparedCompositionConfiguration
{
    readonly CompositionBinding template;
    readonly SelectionConstructionPolicy outer;
    readonly decimal variantBalanceTolerance;

    public PreparedCompositionConfiguration(TradeSelectionBinding binding, SelectionCandidateIntent candidate)
    {
        template = CompositionBindingResolver.Resolve(new TradeSelectionResult { Outcome = SelectionOutcome.Selected,
            SelectedCandidate = candidate, DecisionHorizon = TradeSelectionContracts.CommonPolicy(binding).TargetHorizon,
            ValidUntilUtc = binding.ValidUntilUtc }, binding, binding.FrozenAtUtc);
        outer = SelectionConstructionPolicy.Read(TradeSelectionContracts.Policy(binding, candidate.CompositionPolicyReference).PayloadJson);
        using var json = JsonDocument.Parse(binding.CatalogDefinitions.Single(value => value.Key == candidate.VariantKey).SettingsJson);
        variantBalanceTolerance = json.RootElement.GetProperty("BalanceTolerance").GetDecimal();
    }

    public CompositionBinding Bind(TradeSelectionResult selected, TradeSelectionBinding binding, DateTime now)
    {
        Require(selected.Outcome == SelectionOutcome.Selected && selected.SelectedCandidate?.CandidateHash == template.Selected.CandidateHash,
            "OC.CONTRACT.UPSTREAM_INVALID");
        var value = template with { Selected = selected.SelectedCandidate!, FrozenAtUtc = now,
            ValidUntilUtc = new[] { binding.ValidUntilUtc, selected.ValidUntilUtc }.Min(), BindingSha256 = "" };
        return value with { BindingSha256 = CompositionHash.Binding(value) };
    }

    internal void ValidateResolved(Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands.ExecuteOrderCompositionPipelineCommand command,
        CompositionParameters parameters)
    {
        var intent = command.CompositionBinding.Selected;
        Require(intent.CandidateHash == template.Selected.CandidateHash, "OC.CONFIG.RULE_INVALID");
        Require(parameters.BalanceTolerance <= outer.MaximumDeltaTolerance && parameters.BalanceTolerance <= variantBalanceTolerance
            && (intent.Bias == "Balanced" ? parameters.TargetNetDelta == 0
                : intent.Bias == "Bullish" ? parameters.TargetNetDelta > 0 : parameters.TargetNetDelta < 0), "OC.CONFIG.RULE_INVALID");
        if (template.BuilderCode != "Future") Require(parameters.MinimumDaysToExpiry >= outer.MinimumDaysToExpiry
            && parameters.MaximumDaysToExpiry <= outer.MaximumDaysToExpiry, "OC.CONFIG.RULE_INVALID");
    }
}
