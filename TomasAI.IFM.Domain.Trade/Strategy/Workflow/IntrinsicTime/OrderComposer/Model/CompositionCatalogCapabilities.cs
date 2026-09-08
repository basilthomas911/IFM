using System.Text.Json;
using TomasAI.IFM.Application.Storage.ConfigurationDb.StrategyCatalog;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition;
using static TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition.CompositionRulesContract;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Model;

/// <summary>Registers implemented construction capabilities only. Risk capability publication remains owned by Risk Management.</summary>
public static class CompositionCatalogCapabilities
{
    public static IStrategyCatalogCapabilityValidator[] Create() =>
    [ new Validator(new("builder", "Future", 1)), new Validator(new("builder", "CallVertical", 1)),
      new Validator(new("builder", "PutVertical", 1)), new Validator(new("builder", "IronCondor", 1)),
      new Validator(new("validator", Role, 1)) ];

    /// <summary>Checks declared composer roles during deployment publication; selector-only historical graphs remain readable.</summary>
    public static void ValidateDeployment(StrategyCatalogDefinition deployment, IReadOnlyDictionary<CatalogKey, StoredStrategyCatalogDefinition> graph)
    {
        var roles = deployment.Parameters.Where(x => x.Role == Role).ToArray();
        if (roles.Length == 0) return;
        Require(roles.Length == 1, "OC.CONFIG.AMBIGUOUS");
        var definition = graph[roles[0].ParameterSet].Definition;
        var schema = graph[definition.Parent!].Definition;
        Require(schema.Capabilities.Contains(new CatalogCapability("validator", Role, 1)), "OC.CONFIG.CAPABILITY_UNSUPPORTED");
        var rules = Read(definition.Settings.GetRawText());
        Require(rules.SupportedHorizon == deployment.Horizon && rules.PricerVersion == new Black76ComposerPricer().Version
            && deployment.Variants.ToHashSet().SetEquals(rules.VariantRules.Select(x => x.VariantKey)), "OC.CONFIG.RULE_INVALID");
        foreach (var rule in rules.VariantRules)
        {
            var variant = graph[rule.VariantKey].Definition;
            Require(rule.StructureKey == variant.Parent, "OC.CONFIG.RULE_INVALID");
            var settings = variant.Settings;
            Require(rule.BaseParameters.TargetNetDelta == settings.GetProperty("TargetNetDelta").GetDecimal()
                && rule.BaseParameters.BalanceTolerance <= settings.GetProperty("BalanceTolerance").GetDecimal()
                && (!settings.GetProperty("SymmetricWings").GetBoolean() || rule.RequireSymmetricWings), "OC.CONFIG.RULE_INVALID");
            if (graph[rule.StructureKey].Definition.Capabilities.Single(x => x.Role == "builder").Code != "Future")
                Require(!rule.AllowedWidths.IsEmpty && rule.AllowedWidths.All(w => w >= settings.GetProperty("MinimumWingWidth").GetDecimal()
                    && w <= settings.GetProperty("MaximumWingWidth").GetDecimal()), "OC.CONFIG.RULE_INVALID");
            CompositionParameterResolver.ValidateBounds(rule);
        }
    }

    sealed class Validator(CatalogCapability capability) : IStrategyCatalogCapabilityValidator
    {
        public CatalogCapability Capability => capability;
        public void Validate(StrategyCatalogDefinition owner, IReadOnlyDictionary<CatalogKey, StoredStrategyCatalogDefinition> dependencies)
        {
            if (capability.Role == "builder")
            {
                var expected = capability.Code == "Future" ? 1 : capability.Code == "IronCondor" ? 4 : 2;
                Require(owner.Key.Kind == StrategyCatalogKind.Structure && owner.Legs.Length == expected
                    && owner.ExpiryGroups.Length == 1 && owner.Legs.All(x => x.Ratio == 1), "OC.CONFIG.UNSUPPORTED_STRUCTURE");
                return;
            }
            Require(owner.Key.Kind is StrategyCatalogKind.ParameterSchema or StrategyCatalogKind.ParameterSet, "OC.CONFIG.RULE_INVALID");
            if (owner.Key.Kind == StrategyCatalogKind.ParameterSchema) { _ = StrategyCatalogValidation.ReadShape(owner.Settings); return; }
            StrategyCatalogValidation.ValidateParameters(StrategyCatalogValidation.ReadShape(dependencies[owner.Parent!].Definition.Settings), owner.Settings);
            var rules = Read(owner.Settings.GetRawText());
            Require(rules.PricerVersion == new Black76ComposerPricer().Version, "OC.CONFIG.CAPABILITY_UNSUPPORTED");
            foreach (var rule in rules.VariantRules) CompositionParameterResolver.ValidateBounds(rule);
        }
    }
}
