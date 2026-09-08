using System.Text.Json;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection;
using static TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition.CompositionRulesContract;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Model;

/// <summary>Resolves only exact rules already retained in the selected deployment; never queries latest configuration.</summary>
public static class CompositionBindingResolver
{
    public static CompositionBinding Resolve(TradeSelectionResult selected, TradeSelectionBinding binding, DateTime frozenAtUtc)
    {
        TradeSelectionContracts.ValidateBinding(binding);
        Require(selected.Outcome == SelectionOutcome.Selected && selected.SelectedCandidate is not null, "OC.CONTRACT.UPSTREAM_INVALID");
        var intent = selected.SelectedCandidate!;
        var roles = intent.SpecializedParameterBindings.Where(x => x.Role == Role).ToArray();
        Require(roles.Length == 1, "OC.CONFIG.MISSING");
        var definition = binding.CatalogDefinitions.SingleOrDefault(x => x.Key == roles[0].ParameterSet)
            ?? throw new CompositionException("OC.CONFIG.MISSING");
        var schema = binding.CatalogDefinitions.SingleOrDefault(x => x.Key == definition.Parent)
            ?? throw new CompositionException("OC.CONFIG.MISSING");
        Require(definition.Key.Kind == StrategyCatalogKind.ParameterSet && schema.Key.Kind == StrategyCatalogKind.ParameterSchema
            && schema.Capabilities.Any(x => x.Role == "validator" && x.Code == Role && x.Version == 1), "OC.CONFIG.CAPABILITY_UNSUPPORTED");
        var rules = Read(definition.SettingsJson);
        var structure = binding.CatalogDefinitions.Single(x => x.Key == intent.StructureKey);
        var variant = binding.CatalogDefinitions.Single(x => x.Key == intent.VariantKey);
        TradeSelectionCatalogCapabilities.ValidateVariant(variant, structure);
        var builder = structure.Capabilities.Single(x => x.Role == "builder");
        Require(builder.Version == 1 && builder.Code is "Future" or "CallVertical" or "PutVertical" or "IronCondor"
            && rules.SupportedHorizon == selected.DecisionHorizon && rules.InstrumentRoot == intent.Product.Symbol
            && rules.Currency == intent.Product.Currency, "OC.CONFIG.CAPABILITY_UNSUPPORTED");
        var deployment = binding.CatalogDefinitions.Single(x => x.Key == intent.DeploymentKey);
        Require(deployment.Variants.ToHashSet().SetEquals(rules.VariantRules.Select(x => x.VariantKey)), "OC.CONFIG.RULE_INVALID");
        var outer = SelectionConstructionPolicy.Read(TradeSelectionContracts.Policy(binding, intent.CompositionPolicyReference).PayloadJson);
        var rule = rules.VariantRules.Single(x => x.VariantKey == intent.VariantKey);
        Require(rule.StructureKey == intent.StructureKey, "OC.CONFIG.RULE_INVALID");
        using var json = JsonDocument.Parse(variant.SettingsJson);
        var v = json.RootElement; var p = rule.BaseParameters;
        Require(p.TargetNetDelta == v.GetProperty("TargetNetDelta").GetDecimal()
            && p.BalanceTolerance <= v.GetProperty("BalanceTolerance").GetDecimal()
            && p.BalanceTolerance <= outer.MaximumDeltaTolerance
            && (!v.GetProperty("SymmetricWings").GetBoolean() || rule.RequireSymmetricWings), "OC.CONFIG.RULE_INVALID");
        if (builder.Code != "Future")
            Require(p.MinimumDaysToExpiry >= outer.MinimumDaysToExpiry && p.MaximumDaysToExpiry <= outer.MaximumDaysToExpiry
                && !rule.AllowedWidths.IsEmpty && rule.AllowedWidths.All(w => w >= outer.MinimumWingWidth && w <= outer.MaximumWingWidth
                    && w >= v.GetProperty("MinimumWingWidth").GetDecimal() && w <= v.GetProperty("MaximumWingWidth").GetDecimal()), "OC.CONFIG.RULE_INVALID");
        Require(structure.Legs.Length <= outer.MaximumLegs, "OC.CONFIG.RULE_INVALID");
        var resolved = new CompositionBinding { SchemaVersion = 1, Selected = intent, RulesDefinition = definition, RulesSchema = schema,
            Rules = rules, BuilderCode = builder.Code, BuilderVersion = builder.Version, FrozenAtUtc = frozenAtUtc,
            ValidUntilUtc = new[] { binding.ValidUntilUtc, selected.ValidUntilUtc }.Min() };
        return resolved with { BindingSha256 = CompositionHash.Binding(resolved) };
    }
    /// <summary>Checks adjusted values against the original immutable outer constraints; adjustments cannot relax safeguards.</summary>
    public static void ValidateResolved(Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands.ExecuteOrderCompositionPipelineCommand c,
        CompositionParameters p, CompositionVariantRules rules)
    {
        var intent = c.CompositionBinding.Selected;
        var outer = SelectionConstructionPolicy.Read(TradeSelectionContracts.Policy(c.SelectionBinding, intent.CompositionPolicyReference).PayloadJson);
        var variant = c.SelectionBinding.CatalogDefinitions.Single(x => x.Key == intent.VariantKey);
        using var json = JsonDocument.Parse(variant.SettingsJson);
        Require(p.BalanceTolerance <= outer.MaximumDeltaTolerance && p.BalanceTolerance <= json.RootElement.GetProperty("BalanceTolerance").GetDecimal()
            && (intent.Bias == "Balanced" ? p.TargetNetDelta == 0 : intent.Bias == "Bullish" ? p.TargetNetDelta > 0 : p.TargetNetDelta < 0), "OC.CONFIG.RULE_INVALID");
        if (c.CompositionBinding.BuilderCode != "Future")
            Require(p.MinimumDaysToExpiry >= outer.MinimumDaysToExpiry && p.MaximumDaysToExpiry <= outer.MaximumDaysToExpiry, "OC.CONFIG.RULE_INVALID");
    }
}
