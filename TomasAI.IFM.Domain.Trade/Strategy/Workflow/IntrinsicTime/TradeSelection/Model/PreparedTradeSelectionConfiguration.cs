using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.TradeSelection;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection.Model;

/// <summary>Compiled once by configuration publication, without workflow-specific identities or market values.</summary>
public sealed class PreparedTradeSelectionConfiguration
{
    public TradeSelectionParameterSet Policy { get; }
    internal IReadOnlyDictionary<CatalogKey, SelectionCatalogDefinitionSnapshot> Nodes { get; }
    internal IReadOnlyDictionary<string, SelectionVariantRule> Rules { get; }

    public PreparedTradeSelectionConfiguration(TradeSelectionBinding binding)
    {
        Policy = TradeSelectionContracts.ValidateBinding(binding);
        Nodes = binding.CatalogDefinitions.ToDictionary(value => value.Key);
        var rules = new Dictionary<string, SelectionVariantRule>(StringComparer.Ordinal);
        foreach (var candidate in binding.Candidates)
        {
            var variant = Nodes[candidate.VariantKey];
            var structure = Nodes[candidate.StructureKey];
            TradeSelectionCatalogCapabilities.ValidateVariant(variant, structure);
            SelectionConstructionPolicy.Read(TradeSelectionContracts.Policy(binding, candidate.CompositionPolicyReference).PayloadJson).ValidateCandidate(structure, variant);
            var builder = structure.Capabilities.Single(value => value.Role == "builder");
            var specialized = candidate.SpecializedParameterBindings.Where(value => value.Role == "TradeSelectionVariants").ToArray();
            TradeSelectionContracts.Require(specialized.Length <= 1, "TS.CONFIG.INVALID", "Multiple specialized selector policies.");
            var available = specialized.Length == 0 ? Policy.VariantRules
                : TradeSelectionPolicy.ReadSpecialized(Nodes[specialized[0].ParameterSet].SettingsJson, Policy).Rules;
            rules.Add(candidate.CandidateHash, available.Single(value => value.BuilderCapabilityCode == builder.Code
                && value.BuilderCapabilityVersion == builder.Version && value.Side == variant.Side
                && value.Bias == variant.Bias && value.PremiumMode == variant.PremiumMode));
        }
        Rules = rules;
    }

    internal static string Key(TradeSelectionBinding binding) => string.Join('/',
        binding.CommonPolicy.PayloadSha256, string.Join(',', binding.PipelinePolicies.Select(value => value.PayloadSha256).Order(StringComparer.Ordinal)),
        string.Join(',', binding.Candidates.Select(value => value.CandidateHash).Order(StringComparer.Ordinal)));
}
