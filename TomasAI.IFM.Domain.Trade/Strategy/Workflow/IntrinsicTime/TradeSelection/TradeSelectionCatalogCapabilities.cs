using System.Text.Json;
using TomasAI.IFM.Application.Storage.ConfigurationDb.StrategyCatalog;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.TradeSelection;
using static TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection.TradeSelectionContracts;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection;
/// <summary>Only implemented selector capabilities. Builder and risk capabilities must be supplied by their owners.</summary>
public static class TradeSelectionCatalogCapabilities
{
    public static IStrategyCatalogCapabilityValidator[] Create() => [new SelectorValidator(new("evaluator", "RegimeAligned", 1)), new SelectorValidator(new("data", "AcceptedMarketAssessment", 1)), new SelectorValidator(new("validator", "StructureVariant", 1)), new SelectorValidator(new("validator", "TradeSelectionVariants", 1))];
    sealed class SelectorValidator(CatalogCapability capability) : IStrategyCatalogCapabilityValidator
    {
        public CatalogCapability Capability => capability;

        public void Validate(StrategyCatalogDefinition owner, IReadOnlyDictionary<CatalogKey, StoredStrategyCatalogDefinition> dependencies)
        {
            if (string.Equals(capability.Code, "TradeSelectionVariants", StringComparison.Ordinal))
            {
                Require(owner.Key.Kind is StrategyCatalogKind.ParameterSchema or StrategyCatalogKind.ParameterSet, "TS.CONFIG.INVALID", "Specialized selector capability requires ParameterSchema/ParameterSet.");
                if (owner.Key.Kind == StrategyCatalogKind.ParameterSchema)
                {
                    _ = StrategyCatalogValidation.ReadShape(owner.Settings);
                    return;
                }

                StrategyCatalogValidation.ValidateParameters(StrategyCatalogValidation.ReadShape(dependencies[owner.Parent!].Definition.Settings), owner.Settings);
                // Publication validates a complete supported rule matrix. Binding additionally constrains it to the pinned common policy.
                _ = TradeSelectionPolicy.ReadSpecialized(owner.Settings.GetRawText(), TradeSelectionDefaultProfiles.Create(Guid.Parse("11111111-1111-1111-1111-111111111111"), Domain.MarketData.Analytics.Shared.TimeFrameType.Daily));
                return;
            }

            if (capability.Role is "evaluator" or "data")
            {
                Require(owner.Key.Kind == StrategyCatalogKind.Strategy && owner.Structures.Length > 0, "TS.CONFIG.CAPABILITY_UNSUPPORTED", "Selector capability requires a strategy with exact structures.");
                return;
            }

            if (owner.Key.Kind == StrategyCatalogKind.Deployment)
            {
                OrderComposer.Model.CompositionCatalogCapabilities.ValidateDeployment(owner, dependencies);
                foreach (var key in owner.Variants)
                {
                    var variant = dependencies[key];
                    ValidateVariant(SelectionCatalogTransport.From(variant), SelectionCatalogTransport.From(dependencies[variant.Definition.Parent!]));
                }

                return;
            }

            Require(owner.Key.Kind == StrategyCatalogKind.Variant, "TS.CONFIG.CAPABILITY_UNSUPPORTED", "StructureVariant validates variant definitions only.");
            ValidateVariant(SelectionCatalogTransport.From(new StoredStrategyCatalogDefinition(owner, StrategyCatalogValidation.ContentHash(owner), CatalogLifecycleStatus.Published, DateTime.UnixEpoch, "validation", DateTime.UnixEpoch, "validation", null, null)), SelectionCatalogTransport.From(dependencies[owner.Parent!]));
        }
    }

    public static void ValidateVariant(SelectionCatalogDefinitionSnapshot variant, SelectionCatalogDefinitionSnapshot structure)
    {
        ArgumentNullException.ThrowIfNull(structure);
        ArgumentNullException.ThrowIfNull(variant);
        var builder = structure.Capabilities.SingleOrDefault(x => string.Equals(x.Role, "builder", StringComparison.Ordinal)) ?? throw new TradeSelectionValidationException("TS.CONFIG.CAPABILITY_UNSUPPORTED", "Missing unique builder requirement.");
        var rule = TradeSelectionDefaultProfiles.Create(Guid.Parse("11111111-1111-1111-1111-111111111111"), Domain.MarketData.Analytics.Shared.TimeFrameType.Daily).VariantRules.SingleOrDefault(x => string.Equals(x.BuilderCapabilityCode, builder.Code, StringComparison.Ordinal) && x.BuilderCapabilityVersion == builder.Version && string.Equals(x.Side, variant.Side, StringComparison.Ordinal) && string.Equals(x.Bias, variant.Bias, StringComparison.Ordinal) && string.Equals(x.PremiumMode, variant.PremiumMode, StringComparison.Ordinal));
        Require(rule is not null, "TS.CONFIG.CAPABILITY_UNSUPPORTED", "Unsupported variant signature.");
        var legs = structure.Legs.ToDictionary(x => x.Key,StringComparer.Ordinal);
        Require(structure.ExpiryGroups.Length == 1 && structure.ExpiryGroups[0].AfterGroup is null && structure.Legs.All(x => x.Ratio == 1) && variant.VariantLegs.Length == legs.Count && variant.VariantLegs.All(x => x.Ratio == 1 && legs.ContainsKey(x.LegKey)), "TS.CONFIG.VARIANT", "V1 requires complete unit-ratio same-expiry legs.");
        Dictionary<string, (string Right, string Side)> required = builder.Code switch
        {
            "Future" => new(StringComparer.Ordinal)
            {
                ["Future"] = ("None", string.Equals(variant.Side, "Long", StringComparison.Ordinal) ? "Buy" : "Sell")
            },
            "CallVertical" => new(StringComparer.Ordinal)
            {
                ["Lower"] = ("Call", string.Equals(variant.Side, "Long", StringComparison.Ordinal) ? "Buy" : "Sell"),
                ["Upper"] = ("Call", string.Equals(variant.Side, "Long", StringComparison.Ordinal) ? "Sell" : "Buy")
            },
            "PutVertical" => new(StringComparer.Ordinal)
            {
                ["Lower"] = ("Put", string.Equals(variant.Side, "Long", StringComparison.Ordinal) ? "Sell" : "Buy"),
                ["Upper"] = ("Put", string.Equals(variant.Side, "Long", StringComparison.Ordinal) ? "Buy" : "Sell")
            },
            "IronCondor" => new(StringComparer.Ordinal)
            {
                ["LowerPut"] = ("Put", string.Equals(variant.Side, "Short", StringComparison.Ordinal) ? "Buy" : "Sell"),
                ["UpperPut"] = ("Put", string.Equals(variant.Side, "Short", StringComparison.Ordinal) ? "Sell" : "Buy"),
                ["LowerCall"] = ("Call", string.Equals(variant.Side, "Short", StringComparison.Ordinal) ? "Sell" : "Buy"),
                ["UpperCall"] = ("Call", string.Equals(variant.Side, "Short", StringComparison.Ordinal) ? "Buy" : "Sell")
            },
            _ => throw new TradeSelectionValidationException("TS.CONFIG.VARIANT", "Unsupported builder.")};
        Require(required.Count == legs.Count && required.All(x => legs.TryGetValue(x.Key, out var l) && string.Equals(l.OptionRight, x.Value.Right, StringComparison.Ordinal) && string.Equals(l.InstrumentClass, (string.Equals(builder.Code, "Future", StringComparison.Ordinal) ? "Futures" : "FuturesOption"), StringComparison.Ordinal) && string.Equals(variant.VariantLegs.Single(v => string.Equals(v.LegKey, x.Key, StringComparison.Ordinal)).Side, x.Value.Side, StringComparison.Ordinal)), "TS.CONFIG.VARIANT", "Variant topology does not match its side and premium mode.");
        TradeSelectionPolicy.CheckJson(variant.SettingsJson);
        using var json = JsonDocument.Parse(variant.SettingsJson);
        var settings = json.RootElement;
        string[] fields = ["TargetNetDelta", "BalanceTolerance", "SymmetricWings", "MinimumWingWidth", "MaximumWingWidth", "DeltaUnits"];
        Require(settings.EnumerateObject().Select(x => x.Name).ToHashSet(StringComparer.Ordinal).SetEquals(fields), "TS.CONFIG.VARIANT", "Complete explicit variant settings and delta units are required.");
        var delta = settings.GetProperty("TargetNetDelta").GetDecimal();
        var tolerance = settings.GetProperty("BalanceTolerance").GetDecimal();
        var min = settings.GetProperty("MinimumWingWidth").GetDecimal();
        var max = settings.GetProperty("MaximumWingWidth").GetDecimal();
        Require(string.Equals(settings.GetProperty("DeltaUnits").GetString(), "UnderlyingEquivalent", StringComparison.Ordinal) && tolerance is >= 0 and <= 1 && delta is >= -1 and <= 1 && ((string.Equals(variant.Bias, "Balanced", StringComparison.Ordinal)) switch
        {
            true => delta == 0,
            _ => string.Equals(variant.Bias, "Bullish", StringComparison.Ordinal) ? delta > 0 : delta < 0
        }), "TS.CONFIG.VARIANT", "Invalid delta intent or units.");
        Require(string.Equals(builder.Code, "Future", StringComparison.Ordinal) ? Math.Abs(delta) == 1 && min == 0 && max == 0 : min > 0 && max >= min, "TS.CONFIG.VARIANT", "Unfinished width/delta placeholders cannot execute.");
        _ = settings.GetProperty("SymmetricWings").GetBoolean();
    }
}
