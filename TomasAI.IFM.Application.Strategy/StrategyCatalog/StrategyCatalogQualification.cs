using TomasAI.IFM.Application.Storage.ConfigurationDb;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using TomasAI.IFM.Domain.Strategy.Contracts.Shared.Canonical;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;

namespace TomasAI.IFM.Application.Strategy.StrategyCatalog;

/// <summary>Qualifies strategy graph facts after the persistence snapshot has closed.</summary>
public sealed class StrategyCatalogQualification(IStrategyCatalogCapabilities? capabilities = null,
    IStrategyCatalogReferences? references = null) : IStrategyDeploymentReader
{
    /// <summary>Checks a Draft and its immutable dependencies before the publication write begins.</summary>
    public async Task<StrategyCatalogPublicationProof> PreparePublicationAsync(IConfigurationDbReadContext database,
        CatalogKey key, string expectedContentHash, DateTime effectiveFromUtc, CancellationToken token = default)
    {
        var facts = await database.ReadStrategyCatalogQualificationAsync(key, effectiveFromUtc, true, token)
            .ConfigureAwait(false);
        var root = facts.Definitions.Single(row => row.Definition.Key == key);
        if (root.Status != CatalogLifecycleStatus.Draft || !string.Equals(root.ContentHash, expectedContentHash, StringComparison.Ordinal))
            throw new InvalidOperationException("Publish requires the exact Draft content hash.");
        var fingerprint = facts.Fingerprint();
        await ValidateAsync(facts, token).ConfigureAwait(false);
        return new(key, effectiveFromUtc, fingerprint);
    }

    /// <summary>Publishes a qualified draft through the fenced persistence capability.</summary>
    public async Task PublishAsync(IConfigurationDbContext database, CatalogKey key, string expectedContentHash,
        DateTime effectiveFromUtc, string publishedBy, CancellationToken token = default)
    {
        var proof = await PreparePublicationAsync(database, key, expectedContentHash, effectiveFromUtc, token)
            .ConfigureAwait(false);
        await database.PublishPreparedStrategyCatalogAsync(proof, expectedContentHash, publishedBy, token)
            .ConfigureAwait(false);
    }

    /// <summary>Reads and qualifies a published deployment without running application callbacks in a transaction.</summary>
    public async Task<StrategyCatalogSnapshot> ReadPublishedAsync(IConfigurationDbReadContext database, CatalogKey key,
        DateTime asOfUtc, CancellationToken token = default)
    {
        var facts = await database.ReadStrategyCatalogQualificationAsync(key, asOfUtc, false, token)
            .ConfigureAwait(false);
        await ValidateAsync(facts, token).ConfigureAwait(false);
        return StrategyCatalogValidation.CreateDeploymentSnapshot(key, asOfUtc, facts.Definitions);
    }

    async Task ValidateAsync(StrategyCatalogQualificationFacts facts, CancellationToken token)
    {
        var graph = facts.Definitions.ToDictionary(row => row.Definition.Key);
        var policies = facts.Policies.ToDictionary(policy => (policy.Kind, policy.Id, policy.Version));
        foreach (var stored in facts.Definitions)
        {
            token.ThrowIfCancellationRequested();
            var definition = stored.Definition;
            StrategyCatalogValidation.ValidateForPublication(definition, graph);
            foreach (var requirement in definition.Capabilities)
                ValidateCapability(requirement, definition);
            if (definition.Key.Kind == StrategyCatalogKind.ParameterSet)
                foreach (var validator in graph[definition.Parent!].Definition.Capabilities.Where(c => c.Role == "validator"))
                    ValidateCapability(validator, definition);
            if (definition.Products.Length > 0 || definition.LegacyFamilies.Length > 0)
            {
                var validator = references ?? throw new InvalidOperationException("Catalog external reference validator is not registered.");
                foreach (var product in definition.Products)
                    await validator.ValidateProductAsync(product, token).ConfigureAwait(false);
                foreach (var family in definition.LegacyFamilies)
                    await validator.ValidateLegacyFamilyAsync(family, StrategyCatalogValidation.Freeze(definition), token)
                        .ConfigureAwait(false);
            }

            foreach (var parameter in definition.PipelineParameters)
            {
                if (!policies.TryGetValue((parameter.Kind, parameter.Id, parameter.Version), out var policy)
                    || !string.Equals(policy.PayloadHash, parameter.Hash, StringComparison.Ordinal) || policy.Status != 1
                    || policy.EffectiveFromUtc is null || policy.EffectiveFromUtc > facts.AsOfUtc)
                    throw new InvalidOperationException("Pipeline parameter reference is missing, mismatched or not effective and Published.");
                if (parameter.Kind is CatalogPipelineParameterKind.TradeSelection or CatalogPipelineParameterKind.OrderComposition
                    or CatalogPipelineParameterKind.IntrinsicTimeStrategyWorkflow or CatalogPipelineParameterKind.MarketConditionAssessment
                    or CatalogPipelineParameterKind.RegimeDiscovery or CatalogPipelineParameterKind.RiskManagement)
                    TradeSelectionContracts.ValidatePipelinePolicy(new SelectionPipelinePolicySnapshot
                    {
                        Kind = parameter.Kind, Id = parameter.Id, Version = parameter.Version,
                        PayloadSha256 = policy.PayloadHash, PayloadJson = policy.PayloadJson, SchemaVersion = policy.SchemaVersion
                    });
                if (parameter.Kind == CatalogPipelineParameterKind.RiskManagement && definition.Key.Kind == StrategyCatalogKind.Deployment)
                {
                    var risk = RiskParameterSet.Read(policy.PayloadJson);
                    if (risk.TargetHorizon != definition.Horizon || definition.Products.Any(product =>
                        !string.Equals(product.Symbol, risk.Root, StringComparison.Ordinal) || !string.Equals(product.Currency, risk.Currency, StringComparison.Ordinal)))
                        throw new InvalidOperationException("Risk policy horizon/product scope does not match the deployment.");
                }
            }
        }

        void ValidateCapability(CatalogCapability capability, StrategyCatalogDefinition owner)
        {
            var registry = capabilities ?? throw new InvalidOperationException($"Catalog capability registry is not registered: {capability.Code}.");
            var copy = graph.ToDictionary(x => x.Key, x => x.Value with { Definition = StrategyCatalogValidation.Freeze(x.Value.Definition) });
            registry.Validate(capability, StrategyCatalogValidation.Freeze(owner), copy);
        }
    }
}
