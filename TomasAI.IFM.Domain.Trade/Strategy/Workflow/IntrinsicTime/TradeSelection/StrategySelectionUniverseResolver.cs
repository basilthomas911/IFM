using TomasAI.IFM.Application.Storage.ConfigurationDb;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.TradeSelection;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
using static TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection.TradeSelectionContracts;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection;

/// <summary>Freezes the portfolio-neutral catalog universe selected by a workflow activation.</summary>
public sealed class StrategySelectionUniverseResolver(IConfigurationDbContext configuration)
{
    public async Task<TradeSelectionBinding> ResolveAsync(
        TradeSelectionActivation activation,
        Guid workflowId,
        long workflowRevision,
        Guid correlationId,
        DateTime frozenAtUtc,
        DateTime workflowExpiresAtUtc,
        DateOnly tradeDate,
        CancellationToken cancellationToken = default)
    {
        activation.Validate();
        Require(activation.SchemaVersion == 2, "TS.CONFIG.ACTIVATION", "A portfolio-neutral schema-2 activation is required.");
        Require(frozenAtUtc.Kind == DateTimeKind.Utc && workflowExpiresAtUtc.Kind == DateTimeKind.Utc &&
            frozenAtUtc < workflowExpiresAtUtc && workflowId != Guid.Empty && workflowRevision > 0 && correlationId != Guid.Empty,
            "TS.CONTRACT.IDENTITY", "Invalid strategy-universe execution identity or validity interval.");

        var common = activation.SelectionPolicyReference;
        var resolved = await configuration.GetEffectiveTradeSelectionVersionAsync(
            common.Id, common.Version, common.PayloadSha256, frozenAtUtc, cancellationToken).ConfigureAwait(false);
        var selectionPolicy = resolved.ParameterSet;
        var deploymentKeys = activation.DeploymentKeys.OrderBy(KeyText, StringComparer.Ordinal).ToArray();
        Require(deploymentKeys.Length <= selectionPolicy.MaximumAssignments, "TS.CONFIG.CANDIDATE_LIMIT", "Too many strategy deployments.");

        Dictionary<CatalogKey, SelectionCatalogDefinitionSnapshot> nodes = [];
        Dictionary<(CatalogPipelineParameterKind, Guid, int), SelectionPipelinePolicySnapshot> policies = [];
        List<SelectionDeploymentSnapshot> graphs = [];
        List<SelectionCandidateBinding> candidates = [];
        await AddPolicy(common).ConfigureAwait(false);

        for (var deploymentIndex = 0; deploymentIndex < deploymentKeys.Length; deploymentIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var key = deploymentKeys[deploymentIndex];
            var graph = await configuration.GetPublishedStrategyDeploymentAsync(key, frozenAtUtc, cancellationToken).ConfigureAwait(false);
            foreach (var source in graph.Definitions)
            {
                var node = SelectionCatalogTransport.From(source);
                if (nodes.TryGetValue(node.Key, out var prior))
                    Require(EvidenceHash(prior) == EvidenceHash(node), "TS.CONTRACT.HASH", "Conflicting shared catalog node.");
                else
                    nodes.Add(node.Key, node);
                Require(nodes.Count <= selectionPolicy.MaximumCatalogDefinitions, "TS.CONFIG.CANDIDATE_LIMIT", "Too many catalog nodes.");
                foreach (var parameter in node.PipelineParameters)
                    await AddPolicy(ToReference(parameter)).ConfigureAwait(false);
            }

            var deployment = nodes[key];
            var strategy = nodes[deployment.Parent!];
            var selected = UniquePolicy(deployment, CatalogPipelineParameterKind.TradeSelection);
            var composed = UniquePolicy(deployment, CatalogPipelineParameterKind.OrderComposition);
            Require(SamePolicy(selected, common), "TS.CONFIG.PROFILE_MISMATCH", "Deployment common policy differs from activation.");
            var frozenGraph = new SelectionDeploymentSnapshot
            {
                DeploymentKey = key,
                AsOfUtc = frozenAtUtc,
                ContentHash = graph.ContentHash,
                DefinitionKeys = graph.Definitions.Select(x => x.Definition.Key).OrderBy(KeyText, StringComparer.Ordinal).ToArray()
            };
            graphs.Add(frozenGraph);
            _ = SelectionConstructionProfileReference.FromFrozen(policies[(composed.Kind, composed.Id, composed.Version)], composed, key, nodes, frozenAtUtc);

            foreach (var product in deployment.Products.Where(x => x.Symbol == selectionPolicy.InstrumentRoot))
            foreach (var variantKey in deployment.Variants)
            {
                var variant = nodes[variantKey];
                var structure = nodes[variant.Parent!];
                var classes = structure.Legs.Select(x => x.InstrumentClass).Distinct().ToArray();
                Require(classes.Length == 1 && classes[0] is "Futures" or "FuturesOption",
                    "TS.CONFIG.CAPABILITY_UNSUPPORTED", "Unsupported traded instrument class.");
                var candidate = new SelectionCandidateBinding
                {
                    SchemaVersion = 1,
                    AssignmentVersion = deploymentIndex + 1,
                    AssignmentPriority = deploymentIndex,
                    DeploymentKey = key,
                    StrategyKey = strategy.Key,
                    StructureKey = structure.Key,
                    VariantKey = variantKey,
                    Product = product,
                    SelectionPolicyReference = selected,
                    CompositionPolicyReference = composed,
                    SpecializedParameterBindings = deployment.Parameters,
                    FamilyKeys = strategy.Families
                };
                candidates.Add(candidate with { CandidateHash = CandidateHash(candidate, frozenGraph) });
                Require(candidates.Count <= selectionPolicy.MaximumCandidates, "TS.CONFIG.CANDIDATE_LIMIT", "Too many candidates; no truncation is permitted.");
            }
        }

        var validUntilUtc = policies.Values.Select(value => value.RetiredAtUtc ?? workflowExpiresAtUtc)
            .Append(workflowExpiresAtUtc).Min();
        var universe = new StrategySelectionUniverse
        {
            WorkflowId = workflowId,
            WorkflowRevision = workflowRevision,
            CorrelationId = correlationId,
            InstrumentRoot = activation.InstrumentRoot,
            TargetHorizon = activation.TargetHorizon,
            DeploymentKeys = deploymentKeys,
            FrozenAtUtc = frozenAtUtc,
            ValidUntilUtc = validUntilUtc
        };
        universe = universe with { PayloadSha256 = EvidenceHash(universe) };
        return Seal(new TradeSelectionBinding
        {
            SchemaVersion = 2,
            StrategyUniverse = universe,
            CatalogDefinitions = [.. nodes.Values],
            DeploymentSnapshots = [.. graphs],
            PipelinePolicies = [.. policies.Values],
            Candidates = [.. candidates],
            ExcludedAssignments = [],
            CommonPolicy = common,
            FrozenAtUtc = frozenAtUtc,
            ValidUntilUtc = validUntilUtc,
            RequestedTradeDate = tradeDate,
            TradeDatePolicy = "UTC.TriggerCreatedDate.v2"
        });

        async Task AddPolicy(SelectionPipelinePolicyReference reference)
        {
            var policyKey = (reference.Kind, reference.Id, reference.Version);
            if (policies.TryGetValue(policyKey, out var prior))
            {
                Require(prior.PayloadSha256 == reference.PayloadSha256, "TS.CONTRACT.HASH", "Conflicting pipeline policy identity.");
                return;
            }
            var value = await configuration.GetSelectionPipelinePolicyAsync(reference.Kind, reference.Id, reference.Version, cancellationToken).ConfigureAwait(false)
                ?? throw new TradeSelectionValidationException("TS.CONFIG.MISSING", "Exact pipeline policy is missing.");
            Require(value.PayloadSha256 == reference.PayloadSha256, "TS.CONTRACT.HASH", "Exact pipeline policy hash mismatch.");
            policies.Add(policyKey, value);
        }
    }

    static SelectionPipelinePolicyReference UniquePolicy(SelectionCatalogDefinitionSnapshot deployment, CatalogPipelineParameterKind kind)
    {
        var found = deployment.PipelineParameters.Where(x => x.Kind == kind).ToArray();
        Require(found.Length == 1, "TS.CONFIG.PROFILE_MISMATCH", "Exactly one pipeline binding of each required kind is required.");
        return ToReference(found[0]);
    }

    static SelectionPipelinePolicyReference ToReference(SelectionPipelineParameter parameter) => new()
    {
        Kind = parameter.Kind,
        Id = parameter.Id,
        Version = parameter.Version,
        PayloadSha256 = parameter.Hash,
        Role = parameter.Role
    };
}
