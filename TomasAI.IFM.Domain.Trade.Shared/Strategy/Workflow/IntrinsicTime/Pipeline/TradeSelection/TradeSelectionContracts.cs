using System.Security.Cryptography;
using System.Text;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Application.Storage.ConfigurationDb.StrategyCatalog;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using TomasAI.IFM.Domain.Portfolio.Workflow;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.TradeSelection;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using System.Text.Json;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery;
using MessagePack;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;

public static partial class TradeSelectionContracts
{
    public const int MaximumTransportBytes = 1048576;
    public static void Require(bool condition, string code, string message) { if (!condition) throw new TradeSelectionValidationException(code, message); }
    public static bool Utc(DateTime value) => value != default && value.Kind == DateTimeKind.Utc;
    public static string KeyText(CatalogKey key) 
    {
        ArgumentNullException.ThrowIfNull(key);
        return $"{(short)key.Kind:D2}.{key.Id:D}.{key.Version:D10}";
    }
    public static string CandidateIdentity(SelectionCandidateBinding c) 
    {
        ArgumentNullException.ThrowIfNull(c);
        return $"{c.DeploymentKey.Id:D}.{c.DeploymentKey.Version:D10}.{c.StrategyKey.Id:D}.{c.StrategyKey.Version:D10}.{c.StructureKey.Id:D}.{c.StructureKey.Version:D10}.{c.VariantKey.Id:D}.{c.VariantKey.Version:D10}.{c.Product.ProductId:D10}.{c.AssignmentVersion:D20}";
    }
    public static string WireHash<T>(T value) => Convert.ToHexStringLower(SHA256.HashData(MessagePackBinarySerializer.SerializeHistoricalContent(value)));
    // JSON event storage can normalize decimal scale (1m -> 1.0m). Hash numeric meaning
    // for typed evidence; envelope PayloadSha256 protects typed content or legacy payload bytes.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1308", Justification = "The lowercase hexadecimal evidence hash is an existing canonical persisted identity; its case must remain stable.")]
    public static string EvidenceHash<T>(T value) => MarketConditionAssessmentHash.Compute(value).ToLowerInvariant();
    public static string BindingHash(TradeSelectionBinding b) 
    {
        ArgumentNullException.ThrowIfNull(b);
        return EvidenceHash(b with { PayloadSha256 = "" });
    }
    public static string CandidateHash(SelectionCandidateBinding c, SelectionDeploymentSnapshot graph)
    {
        ArgumentNullException.ThrowIfNull(c);
        ArgumentNullException.ThrowIfNull(graph);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(Encoding.UTF8.GetBytes(MarketConditionAssessmentHash.Serialize(c with { CandidateHash = "" })));
        hash.AppendData(Encoding.UTF8.GetBytes(graph.ContentHash));
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }
    public static bool SamePolicy(SelectionPipelinePolicyReference a, SelectionPipelinePolicyReference b) 
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        return a.Kind == b.Kind && a.Id == b.Id && a.Version == b.Version && string.Equals(a.PayloadSha256,b.PayloadSha256,StringComparison.Ordinal);
    }
    public static SelectionPipelinePolicySnapshot Policy(TradeSelectionBinding binding, SelectionPipelinePolicyReference reference)
        
    {
        ArgumentNullException.ThrowIfNull(binding);
        return binding.PipelinePolicies.SingleOrDefault(x => x.Kind == reference.Kind && x.Id == reference.Id && x.Version == reference.Version && string.Equals(x.PayloadSha256,reference.PayloadSha256,StringComparison.Ordinal))
        ?? throw new TradeSelectionValidationException("TS.CONFIG.MISSING", "Exact pipeline policy is absent.");
    }
    public static TradeSelectionParameterSet CommonPolicy(TradeSelectionBinding b) 
    {
        ArgumentNullException.ThrowIfNull(b);
        return TradeSelectionPolicy.Read(Policy(b, b.CommonPolicy).PayloadJson);
    }
    public static TradeSelectionBinding Seal(TradeSelectionBinding source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var b = source with
        {
            CatalogDefinitions = source.CatalogDefinitions.OrderBy(x => KeyText(x.Key), StringComparer.Ordinal).ToArray(),
            DeploymentSnapshots = source.DeploymentSnapshots.OrderBy(x => KeyText(x.DeploymentKey), StringComparer.Ordinal).ToArray(),
            PipelinePolicies = source.PipelinePolicies.OrderBy(x => (short)x.Kind).ThenBy(x => x.Id.ToString("D"), StringComparer.Ordinal).ThenBy(x => x.Version).ToArray(),
            Candidates = source.Candidates.OrderBy(CandidateIdentity, StringComparer.Ordinal).ToArray(),
            ExcludedAssignments = source.ExcludedAssignments.OrderBy(x => x.AssignmentVersion).ToArray(),
            PayloadSha256 = ""
        };
        return b with { PayloadSha256 = BindingHash(b) };
    }
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051", Justification = "Keep the related validation rules together so the full contract is checked in the established order.")]
    public static TradeSelectionParameterSet ValidateBinding(TradeSelectionBinding b)
    {
        ArgumentNullException.ThrowIfNull(b);
        Require(b is not null && b.SchemaVersion is 1 or 2, "TS.CONTRACT.SCHEMA", "Explicit binding schema 1 or 2 is required.");
        Require(b.CommonPolicy is { Kind: CatalogPipelineParameterKind.TradeSelection }, "TS.CONFIG.PROFILE_MISMATCH", "Common selector policy is required.");
        var p = CommonPolicy(b);
        Require(p.ParameterSetId == b.CommonPolicy.Id && p.Version == b.CommonPolicy.Version && string.Equals(TradeSelectionPolicy.Hash(p),b.CommonPolicy.PayloadSha256,StringComparison.Ordinal), "TS.CONTRACT.HASH", "Common policy identity/hash mismatch.");
        Require(Utc(b.FrozenAtUtc) && Utc(b.ValidUntilUtc) && b.ValidUntilUtc > b.FrozenAtUtc && b.TradeDatePolicy is "UTC.TriggerCreatedDate.Test.v1" or "UTC.TriggerCreatedDate.v2", "TS.CONTRACT.VALUE_RANGE", "Invalid binding times/date policy.");
        Require(b.Candidates.Length <= p.MaximumCandidates && b.CatalogDefinitions.Length <= p.MaximumCatalogDefinitions && b.DeploymentSnapshots.Length <= p.MaximumAssignments
            && (b.SchemaVersion == 2 || b.PortfolioSnapshot.Assignments.Length <= p.MaximumAssignments), "TS.CONFIG.CANDIDATE_LIMIT", "Binding count limit exceeded.");
        Require(MessagePackBinarySerializer.MeasureContent(b) <= p.MaximumBindingPayloadBytes, "TS.CONTRACT.PAYLOAD_SIZE", "Binding byte limit exceeded.");
        Require(string.Equals(b.PayloadSha256,BindingHash(b),StringComparison.Ordinal), "TS.CONTRACT.HASH", "Binding hash mismatch.");
        if (b.VolatilityInput is { } volatility)
        {
            Require(volatility.SchemaVersion == 1 && volatility.Dependency is not null
                && volatility.Dependency.SchemaVersion == VolatilityWorkflowDependencyPolicy.CurrentSchemaVersion
                && !string.IsNullOrWhiteSpace(volatility.Dependency.DependencyPolicyId)
                && !string.IsNullOrWhiteSpace(volatility.Dependency.DependencyPolicyVersion)
                && !string.IsNullOrWhiteSpace(volatility.Dependency.Series.SeriesId)
                && !string.IsNullOrWhiteSpace(volatility.Dependency.Series.MethodologyVersion)
                && !string.IsNullOrWhiteSpace(volatility.Dependency.MetricPolicyVersion)
                && volatility.Dependency.Requirement is VolatilityDependencyRequirement.Required or VolatilityDependencyRequirement.Optional
                && volatility.EvaluatedAtUtc.Offset == TimeSpan.Zero
                && volatility.EvaluatedAtUtc.UtcDateTime >= b.FrozenAtUtc
                && volatility.EvaluatedAtUtc.UtcDateTime <= b.ValidUntilUtc,
                "TS.VOL.DEPENDENCY.INVALID", "Invalid versioned volatility dependency.");
            if (volatility.Snapshot is { } snapshot)
                Require(snapshot.Series == volatility.Dependency.Series
                    && string.Equals(snapshot.MetricPolicyVersion,volatility.Dependency.MetricPolicyVersion,StringComparison.Ordinal)
                    && snapshot.AvailableAtUtc <= volatility.EvaluatedAtUtc
                    && !string.IsNullOrWhiteSpace(snapshot.SnapshotId)
                    && !string.IsNullOrWhiteSpace(snapshot.SnapshotDigest),
                    "TS.VOL.EVIDENCE.MISMATCH", "Volatility snapshot does not match the configured dependency.");
            else Require(volatility.FreshnessStatus == VolatilityFreshnessStatus.Unavailable,
                "TS.VOL.EVIDENCE.MISMATCH", "Missing volatility snapshot must be explicitly unavailable.");
        }
        if (b.SchemaVersion == 1)
        {
            var portfolio = b.PortfolioSnapshot;
            ValidateAuthority(portfolio, b);
            Require(string.Equals(portfolio.PayloadSha256,PortfolioCanonicalHash.Compute(portfolio with { PayloadSha256 = "" }),StringComparison.Ordinal), "TS.CONTRACT.HASH", "Portfolio authority hash mismatch.");
            Require(portfolio.WorkflowId != Guid.Empty && portfolio.WorkflowRevision > 0 && portfolio.Fund.FundId > 0 && portfolio.Portfolio.PortfolioId > 0 && portfolio.Fund.PortfolioId == portfolio.Portfolio.PortfolioId, "TS.CONTRACT.IDENTITY", "Invalid Portfolio/Fund identity.");
            Require(portfolio.ResolvedAtUtc == b.FrozenAtUtc && b.ValidUntilUtc <= portfolio.ValidUntilUtc && string.Equals(portfolio.Fund.DecisionHorizon,p.TargetHorizon.ToString(),StringComparison.Ordinal), "TS.CONTRACT.IDENTITY", "Frozen authority horizon/time mismatch.");
        }
        else
        {
            var universe = b.StrategyUniverse;
            Require(universe is { SchemaVersion: 1 } && universe.WorkflowId != Guid.Empty && universe.WorkflowRevision > 0 && universe.CorrelationId != Guid.Empty
                && string.Equals(universe.InstrumentRoot,p.InstrumentRoot,StringComparison.Ordinal) && universe.TargetHorizon == p.TargetHorizon
                && universe.FrozenAtUtc == b.FrozenAtUtc && universe.ValidUntilUtc == b.ValidUntilUtc
                && universe.DeploymentKeys.SequenceEqual(b.DeploymentSnapshots.Select(x => x.DeploymentKey))
                && string.Equals(universe.PayloadSha256,EvidenceHash(universe with { PayloadSha256 = "" }),StringComparison.Ordinal), "TS.CONTRACT.IDENTITY", "Invalid portfolio-neutral strategy universe.");
        }
        Require(b.CatalogDefinitions.Select(x => x.Key).Distinct().Count() == b.CatalogDefinitions.Length && b.DeploymentSnapshots.Select(x => x.DeploymentKey).Distinct().Count() == b.DeploymentSnapshots.Length && b.Candidates.Select(CandidateIdentity).Distinct(StringComparer.Ordinal).Count() == b.Candidates.Length, "TS.CONFIG.INVALID", "Duplicate binding identity.");
        Require(b.PipelinePolicies.Select(x => (x.Kind, x.Id, x.Version)).Distinct().Count() == b.PipelinePolicies.Length, "TS.CONFIG.INVALID", "Duplicate pipeline policy identity.");
        var nodes = b.CatalogDefinitions.ToDictionary(x => x.Key, SelectionCatalogTransport.ToSource);
        foreach (var node in b.CatalogDefinitions)
        {
            Require(node.SchemaVersion == 1 && node.Status == CatalogLifecycleStatus.Published && node.EffectiveFromUtc is not null && node.EffectiveFromUtc <= b.FrozenAtUtc && (node.RetiredAtUtc is null || node.RetiredAtUtc > b.FrozenAtUtc), "TS.CONFIG.INVALID", "Catalog node is not published/effective at binding.");
            Require(string.Equals(StrategyCatalogValidation.ContentHash(nodes[node.Key].Definition),node.ContentHash,StringComparison.Ordinal), "TS.CONTRACT.HASH", "Catalog source hash mismatch.");
            StrategyCatalogValidation.ValidateForPublication(nodes[node.Key].Definition, nodes);
        }
        foreach (var graph in b.DeploymentSnapshots)
        {
            Require(graph.DefinitionKeys.Distinct().Count() == graph.DefinitionKeys.Length && graph.DefinitionKeys.All(nodes.ContainsKey) && graph.DefinitionKeys.Contains(graph.DeploymentKey) && graph.AsOfUtc == b.FrozenAtUtc, "TS.CONFIG.INVALID", "Invalid deployment graph membership.");
            var seen = new HashSet<CatalogKey>(); var visiting = new HashSet<CatalogKey>();
            void Visit(CatalogKey key, int depth)
            {
                Require(depth <= 32 && graph.DefinitionKeys.Contains(key), "TS.CONFIG.INVALID", "Missing/cyclic/deep graph dependency.");
                Require(!visiting.Contains(key), "TS.CONFIG.INVALID", "Cyclic graph dependency.");
                if (!seen.Add(key)) return;
                visiting.Add(key);
                foreach (var child in StrategyCatalogValidation.Dependencies(nodes[key].Definition)) Visit(child, depth + 1);
                visiting.Remove(key);
            }
            Visit(graph.DeploymentKey, 0);
            Require(seen.Count == graph.DefinitionKeys.Length && string.Equals(SelectionCatalogTransport.GraphHash(graph.DeploymentKey, graph.DefinitionKeys.Select(k => nodes[k])),graph.ContentHash,StringComparison.Ordinal), "TS.CONTRACT.HASH", "Deployment graph mismatch.");
        }
        foreach (var policy in b.PipelinePolicies)
        {
            Require(Enum.IsDefined(policy.Kind) && policy.Id != Guid.Empty && policy.Version > 0
                && (policy.SchemaVersion == 1 || policy.Kind == CatalogPipelineParameterKind.OrderComposition && policy.SchemaVersion is 2 or 3)
                && policy.Status == CatalogLifecycleStatus.Published && policy.EffectiveFromUtc is not null && policy.EffectiveFromUtc <= b.FrozenAtUtc && (policy.RetiredAtUtc is null || policy.RetiredAtUtc > b.FrozenAtUtc), "TS.CONFIG.INVALID", "Pipeline policy is not exact published evidence.");
            ValidatePipelinePolicy(policy);
        }
        foreach (var candidate in b.Candidates) ValidateCandidate(b, candidate, nodes, p);
        ValidateEnumeration(b);
        return p;
    }
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1859", Justification = "Preserve the established abstract dictionary contract at this validation boundary.")]
    static void ValidateCandidate(TradeSelectionBinding b, SelectionCandidateBinding c, IReadOnlyDictionary<CatalogKey, StoredStrategyCatalogDefinition> nodes, TradeSelectionParameterSet p)
    {
        Require(c.SchemaVersion == 1 && c.AssignmentVersion > 0 && c.AssignmentPriority >= 0, "TS.CONTRACT.IDENTITY", "Invalid candidate schema/assignment.");
        var graph = b.DeploymentSnapshots.Single(x => x.DeploymentKey == c.DeploymentKey);
        Require(string.Equals(c.CandidateHash,CandidateHash(c, graph),StringComparison.Ordinal), "TS.CONTRACT.HASH", "Candidate evidence hash mismatch.");
        var deployment = nodes[c.DeploymentKey].Definition; var strategy = nodes[c.StrategyKey].Definition; _ = nodes[c.StructureKey].Definition; var variant = nodes[c.VariantKey].Definition;
        Require(c.DeploymentKey.Kind == StrategyCatalogKind.Deployment && c.StrategyKey.Kind == StrategyCatalogKind.Strategy && c.StructureKey.Kind == StrategyCatalogKind.Structure && c.VariantKey.Kind == StrategyCatalogKind.Variant && deployment.Parent == c.StrategyKey && variant.Parent == c.StructureKey && strategy.Structures.Contains(c.StructureKey) && deployment.Variants.Contains(c.VariantKey), "TS.CONTRACT.IDENTITY", "Candidate graph relationships do not match.");
        Require(deployment.Horizon == p.TargetHorizon && c.Product.ProductId > 0 && string.Equals(c.Product.Symbol,p.InstrumentRoot,StringComparison.Ordinal) && string.Equals(c.Product.Currency,"USD",StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(c.Product.Exchange) && deployment.Products.Contains(SelectionCatalogTransport.ToSource(c.Product)), "TS.CONTRACT.IDENTITY", "Candidate product/horizon mismatch.");
        Require(c.FamilyKeys.SequenceEqual(strategy.Families) && c.SpecializedParameterBindings.Select(SelectionCatalogTransport.ToSource).SequenceEqual(deployment.Parameters), "TS.CONTRACT.IDENTITY", "Candidate family/parameter provenance mismatch.");
        foreach (var pair in new[] { (c.SelectionPolicyReference, CatalogPipelineParameterKind.TradeSelection), (c.CompositionPolicyReference, CatalogPipelineParameterKind.OrderComposition) })
        {
            var declared = deployment.PipelineParameters.Where(x => x.Kind == pair.Item2).ToArray();
            Require(declared.Length == 1 && pair.Item1.Kind == pair.Item2 && declared[0] == new CatalogPipelineParameter(pair.Item1.Role, pair.Item1.Kind, pair.Item1.Id, pair.Item1.Version, pair.Item1.PayloadSha256), "TS.CONFIG.PROFILE_MISMATCH", "Ambiguous/mismatched deployment pipeline role.");
            _ = Policy(b, pair.Item1);
        }
        foreach (var parameter in c.SpecializedParameterBindings)
        {
            var set = nodes[parameter.ParameterSet].Definition; var schema = nodes[set.Parent!].Definition;
            Require(schema.Key.Kind == StrategyCatalogKind.ParameterSchema && schema.Capabilities.Any(x => string.Equals(x.Role,"validator",StringComparison.Ordinal)), "TS.CONFIG.CAPABILITY_UNSUPPORTED", "Required specialized role has no owning semantic validator.");
            if (string.Equals(parameter.Role,"TradeSelectionVariants",StringComparison.Ordinal))
            {
                Require(schema.Capabilities.Any(x => string.Equals(x.Role,"validator",StringComparison.Ordinal) && string.Equals(x.Code,"TradeSelectionVariants",StringComparison.Ordinal) && x.Version == 1), "TS.CONFIG.CAPABILITY_UNSUPPORTED", "Selector specialized role must use its exact implemented validator.");
                _ = TradeSelectionPolicy.ReadSpecialized(set.Settings.GetRawText(), p);
            }
        }
        Require(SamePolicy(c.SelectionPolicyReference, b.CommonPolicy), "TS.CONFIG.PROFILE_MISMATCH", "Candidates must share the pinned common policy.");
        if (b.SchemaVersion == 1)
        {
            var assignment = b.PortfolioSnapshot.Assignments.SingleOrDefault(x => x.AssignmentVersion == c.AssignmentVersion && x.TradeStrategyFamily?.CatalogDeployment == c.DeploymentKey);
            Require(assignment is not null && assignment.SchemaVersion == 3 && assignment.TradeTemplateId == c.DeploymentKey.Id && assignment.TradeTemplateVersion == c.DeploymentKey.Version && assignment.Priority == c.AssignmentPriority && assignment.TradeSelectionHintProfileId == c.SelectionPolicyReference.Id && assignment.TradeSelectionHintProfileVersion == c.SelectionPolicyReference.Version && assignment.OrderCompositionProfileId == c.CompositionPolicyReference.Id && assignment.OrderCompositionProfileVersion == c.CompositionPolicyReference.Version, "TS.CONTRACT.IDENTITY", "Candidate assignment/profile identity mismatch.");
        }
    }
    public static (TradeSelectionParameterSet Policy, MarketConditionAssessmentResult Assessment, RegimeDiscoveryResult Regime) ValidateRequest(ExecuteTradeSelectionPipelineCommand c)
        
    {
        ArgumentNullException.ThrowIfNull(c);
        return ValidateRequestEvidence(c, null);
    }

    /// <summary>Appends cross-field evidence errors while sharing the same rules with non-actor contract consumers.</summary>
    internal static (TradeSelectionParameterSet Policy, MarketConditionAssessmentResult Assessment, RegimeDiscoveryResult Regime) ValidateRequestEvidence(
        ExecuteTradeSelectionPipelineCommand c, List<TomasAI.IFM.Shared.Validation.ValidationError>? errors)

    {
        void Check(bool valid, string code, string message)
        {
            if (errors is null) Require(valid, code, message);
            else if (!valid) errors.Add(new(code, code + ": " + message));
        }
        Check(c is not null && c.SchemaVersion == 1 && c.CommandId != Guid.Empty && !c.PostEvents, "TS.CONTRACT.SCHEMA", "A schema-1 Function request is required.");
        Check(c.Subject.ActorType == ActorType.Function && string.Equals(c.Subject.Name,ExecuteTradeSelectionPipelineCommand.Actor,StringComparison.Ordinal) && string.Equals(c.Subject.Verb,ExecuteTradeSelectionPipelineCommand.Verb,StringComparison.Ordinal) && string.Equals(c.Subject.EntityId,c.EntityId.Format(),StringComparison.Ordinal), "TS.CONTRACT.IDENTITY", "Function subject mismatch.");
        var v = c.WorkflowView; var p = ValidateBinding(c.SelectionBinding);
        var bindingWorkflowId = c.SelectionBinding.SchemaVersion == 2 ? c.SelectionBinding.StrategyUniverse!.WorkflowId : c.SelectionBinding.PortfolioSnapshot.WorkflowId;
        Check(c.EntityId.InputWorkflowRevision == c.InputWorkflowRevision && c.InputWorkflowRevision > 0 && c.WorkflowId == v.WorkflowId && c.WorkflowEntityId == v.EntityId && c.InputWorkflowRevision == v.WorkflowRevision && c.WorkflowId.Value == bindingWorkflowId && string.Equals(v.SelectionBinding?.PayloadSha256,c.SelectionBinding.PayloadSha256,StringComparison.Ordinal), "TS.CONTRACT.IDENTITY", "Frozen workflow identity mismatch.");
        Check(v.Status == WorkflowStrategyMachineStatus.Started && v.CurrentStage == StrategyWorkflowStage.TradeSelection && c.TriggerEvent.Id == v.TriggerEventId && c.TriggerEvent.EntityId == v.TriggerEvent.EntityId && c.TriggerEvent.EntityId.TimePeriod == p.TargetHorizon, "TS.UPSTREAM.INVALID", "Workflow stage/trigger/horizon mismatch.");
        Check(string.Equals(c.RegimeResultEnvelope.PayloadSha256,v.RegimeDiscovery.Result?.PayloadSha256,StringComparison.Ordinal) && string.Equals(c.AssessmentResultEnvelope.PayloadSha256,v.MarketCondition.Result?.PayloadSha256,StringComparison.Ordinal), "TS.UPSTREAM.INVALID", "Upstream envelopes differ from accepted workflow.");
        var bindingCorrelationId = c.SelectionBinding.SchemaVersion == 2 ? c.SelectionBinding.StrategyUniverse!.CorrelationId : c.SelectionBinding.PortfolioSnapshot.CorrelationId;
        Check(c.CorrelationId != Guid.Empty && c.CorrelationId == v.CorrelationId && c.CorrelationId == bindingCorrelationId && c.CausationId != Guid.Empty,
            "TS.CONTRACT.IDENTITY", "Workflow correlation/causation mismatch.");
        Check(v.RegimeDiscovery.ProcessingStatus == StrategyActorProcessingStatus.Completed && v.MarketCondition.ProcessingStatus == StrategyActorProcessingStatus.Completed
            && v.TradeSelection.ProcessingStatus == StrategyActorProcessingStatus.Processing && v.TradeSelection.InputWorkflowRevision == c.InputWorkflowRevision,
            "TS.UPSTREAM.INVALID", "Upstream results must have been accepted and selection must be processing.");
        Check(string.Equals(EvidenceHash(c.TriggerEvent),EvidenceHash(v.TriggerEvent),StringComparison.Ordinal) && c.RegimeResultEnvelope.HasSameContent(v.RegimeDiscovery.Result) && c.AssessmentResultEnvelope.HasSameContent(v.MarketCondition.Result), "TS.UPSTREAM.INVALID", "Accepted input metadata differs.");
        var assessment = MarketConditionAssessmentContracts.ReadResult(c.AssessmentResultEnvelope);
        MarketConditionAssessmentContracts.ValidateAcceptance(assessment, v, v.MarketCondition.InputWorkflowRevision);
        Check(assessment.Assessment.Availability == AssessmentAvailability.Available && !assessment.Assessment.InheritedRestrictions.Contains(RegimeRestriction.NoNewTrade), "TS.UPSTREAM.NOT_ELIGIBLE", "Assessment is unavailable or restricted.");
        Check(c.RegimeResultEnvelope.HasValidPayloadSha256() && string.Equals(c.RegimeResultEnvelope.ResultType,nameof(RegimeDiscoveryResult),StringComparison.Ordinal), "TS.UPSTREAM.INVALID", "Invalid regime envelope.");
        var regime = c.RegimeResultEnvelope.ReadRegimeResult();
        Check(Utc(c.RequestedAtUtc) && Utc(c.EvaluatedAtUtc) && Utc(c.ExpiresAtUtc) && c.EvaluatedAtUtc >= c.RequestedAtUtc && c.EvaluatedAtUtc < c.ExpiresAtUtc && c.ExpiresAtUtc <= v.ExpiresAtUtc && c.ExpiresAtUtc <= c.SelectionBinding.ValidUntilUtc && c.ExpiresAtUtc <= assessment.Assessment.ValidUntilUtc && c.ExpiresAtUtc <= c.RequestedAtUtc.AddMilliseconds(p.MaximumExecutionMilliseconds), "TS.TIME.EXPIRED", "Invalid execution times/deadline.");
        var future = c.EvaluatedAtUtc.AddSeconds(p.FutureClockSkewSeconds);
        Check(c.SelectionBinding.FrozenAtUtc <= c.EvaluatedAtUtc && v.UpdatedAtUtc <= future && v.StartedAtUtc <= future && c.TriggerEvent.CreatedOn <= future
            && c.RegimeResultEnvelope.ProducedAtUtc <= future && c.AssessmentResultEnvelope.ProducedAtUtc <= future, "TS.TIME.CLOCK_SKEW", "Frozen evidence is in the future.");
        foreach (var row in c.SelectionBinding.PipelinePolicies)
        {
            if (row.Kind == CatalogPipelineParameterKind.RegimeDiscovery)
                Check(row.Id == v.RegimeDiscoveryParameterSet.ParameterSetId && row.Version == v.RegimeDiscoveryParameterSet.Version && string.Equals(row.PayloadSha256,v.RegimeDiscoveryParameterPayloadSha256,StringComparison.Ordinal), "TS.CONFIG.PROFILE_MISMATCH", "Deployment regime profile differs from accepted upstream.");
            if (row.Kind == CatalogPipelineParameterKind.MarketConditionAssessment)
                Check(row.Id == assessment.ParameterSetId && row.Version == assessment.ParameterSetVersion && string.Equals(row.PayloadSha256,assessment.ParameterPayloadSha256,StringComparison.Ordinal), "TS.CONFIG.PROFILE_MISMATCH", "Deployment assessment profile differs from accepted upstream.");
        }
        Check(c.SelectionBinding.RequestedTradeDate == DateOnly.FromDateTime(c.TriggerEvent.CreatedOn) && Utc(c.TriggerEvent.CreatedOn), "TS.CONTRACT.IDENTITY", "Trade date must use the original UTC trigger.");
        Check(MessagePackBinarySerializer.MeasureContent(c) <= MaximumTransportBytes && MessagePackBinarySerializer.MeasureEncoded(c) <= MaximumTransportBytes, "TS.CONTRACT.PAYLOAD_SIZE", "Function transport limit exceeded.");
        return (p, assessment, regime);
    }
    /// <summary>Compares projected completion evidence across legacy and typed envelopes, ignoring only stream sequence.</summary>
    public static bool SameCompletion(Events.TradeSelectionFunctionCompletedEvent left, Events.TradeSelectionFunctionCompletedEvent right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        var leftResult = ReadResult(left.Result);
        var rightResult = ReadResult(right.Result);
        var a = left with { EventId = 0, Result = StrategyStageResultEnvelope.CreateSelection(leftResult, 524288) };
        var b = right with { EventId = 0, Result = StrategyStageResultEnvelope.CreateSelection(rightResult, 524288) };
        return string.Equals(EvidenceHash(a),EvidenceHash(b),StringComparison.Ordinal);
    }

    public static TradeSelectionResult ReadResult(StrategyStageResultEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        Require(envelope is not null && string.Equals(envelope.ResultType,nameof(TradeSelectionResult),StringComparison.Ordinal) && envelope.SchemaVersion == 1 && envelope.HasValidPayloadSha256() && envelope.ContentSize <= 524288, "TS.RESULT.INVALID", "Invalid selector result envelope.");
        var result = envelope.ReadSelectionResult();
        var p = ValidateBinding(result.DecisionContext.SelectionBinding);
        Require(result.SchemaVersion == 1 && result.ResultId == envelope.ResultId && result.ResultId == result.InvocationId && result.ProducedAtUtc == envelope.ProducedAtUtc && envelope.MarketDataAsOfUtc == result.DecisionContext.AssessmentResultEnvelope.MarketDataAsOfUtc && envelope.ContentSize <= p.MaximumResultPayloadBytes && result.Outcome is SelectionOutcome.Selected or SelectionOutcome.NoTrade && (result.SelectedCandidate is not null) == (result.Outcome == SelectionOutcome.Selected) && result.SummaryText.Length <= 2048 && result.CompatibilityScore is null, "TS.RESULT.INVALID", "Invalid selector result invariants.");
        ValidateResultEvidence(result, p);
        return result;
    }
}

public static partial class TradeSelectionContracts
{
    static void ValidateAuthority(PortfolioFundStrategySnapshot snapshot, TradeSelectionBinding binding)
    {
        var p = snapshot.Portfolio; var f = snapshot.Fund; var policy = snapshot.FinancialPolicy; var allocation = snapshot.Allocation; var envelope = snapshot.RiskEnvelope; var at = binding.FrozenAtUtc;
        Require(p.Validate().Count == 0 && policy.Validate(forActivation: true).Count == 0 && allocation.Validate().Count == 0 && envelope.Validate().Count == 0, "TS.CONFIG.AUTHORITY", "Invalid authority objects.");
        Require(Enum.IsDefined(p.OperatingState) && Enum.IsDefined(f.OperatingState) && f.OperatingState != FundOperatingState.Unknown && Enum.IsDefined(envelope.CapacityState)
            && f.FundId > 0 && f.FundMandateVersion > 0 && f.SchemaVersion == 3 && f.PortfolioId == p.PortfolioId && f.TradingYear == binding.RequestedTradeDate.Year
            && Utc(f.CreatedOnUtc) && !string.IsNullOrWhiteSpace(f.CreatedBy), "TS.CONFIG.AUTHORITY", "Invalid Fund scope/state/schema.");
        Require(policy.PortfolioId == p.PortfolioId && policy.PolicyId == p.ActivePolicyId && policy.PolicyVersion == p.ActivePolicyVersion && policy.OperatingState == PortfolioFinancialPolicyState.Active
            && allocation.PortfolioId == p.PortfolioId && allocation.PortfolioVersion == p.PortfolioVersion && allocation.FundId == f.FundId && allocation.FundMandateVersion == f.FundMandateVersion
            && allocation.SourcePolicyId == policy.PolicyId && allocation.SourcePolicyVersion == policy.PolicyVersion && envelope.PortfolioId == p.PortfolioId && envelope.PortfolioVersion == p.PortfolioVersion
            && envelope.FundId == f.FundId && envelope.FundMandateVersion == f.FundMandateVersion && envelope.SourcePolicyId == policy.PolicyId && envelope.SourcePolicyVersion == policy.PolicyVersion, "TS.CONFIG.AUTHORITY", "Authority versions do not match.");
        var limits = new[] { p.EffectiveUntilUtc, f.EffectiveUntilUtc, policy.EffectiveUntilUtc, allocation.EffectiveUntilUtc, envelope.ExpiresAtUtc }.Where(x => x.HasValue).Select(x => x!.Value).ToArray();
        Require(Utc(snapshot.ResolvedAtUtc) && Utc(snapshot.ValidUntilUtc) && snapshot.ValidUntilUtc > at && limits.All(x => Utc(x) && snapshot.ValidUntilUtc <= x)
            && p.EffectiveFromUtc <= at && f.EffectiveFromUtc <= at && policy.EffectiveFromUtc <= at && allocation.EffectiveFromUtc <= at && envelope.EffectiveFromUtc <= at, "TS.CONFIG.AUTHORITY", "Authority is expired or not effective.");
        foreach (var assignment in snapshot.Assignments)
            Require(assignment.Validate().Count == 0 && assignment.PortfolioId == p.PortfolioId && assignment.PortfolioVersion == p.PortfolioVersion && assignment.FundId == f.FundId
                && assignment.FundMandateVersion == f.FundMandateVersion && string.Equals(assignment.DecisionHorizon,f.DecisionHorizon,StringComparison.Ordinal) && assignment.UnderlyingUniverse.Contains(CommonPolicy(binding).InstrumentRoot, StringComparer.Ordinal)
                && assignment.EffectiveFromUtc <= at && (assignment.EffectiveUntilUtc is null || binding.ValidUntilUtc <= assignment.EffectiveUntilUtc), "TS.CONFIG.AUTHORITY", "Invalid effective assignment scope or validity.");
    }
}

public static partial class TradeSelectionContracts
{
    public static string[] AssignmentExclusionReasons(PortfolioFundStrategySnapshot authority, FundTradeTemplateAssignmentReadModel assignment)
    {
        ArgumentNullException.ThrowIfNull(assignment);
        ArgumentNullException.ThrowIfNull(authority);
        List<string> reasons = [];
        if (!assignment.Enabled) reasons.Add("TS.ASSIGNMENT.DISABLED");
        var key = assignment.TradeStrategyFamily?.CatalogDeployment;
        if (assignment.Enabled) Require(assignment.SchemaVersion == 3 && key is not null && key.Kind == StrategyCatalogKind.Deployment && key.Id != Guid.Empty && key.Version > 0, "TS.CONFIG.INVALID", "Enabled assignment must reference an exact deployment.");
        if (key is null || !authority.Fund.PermittedTradeStrategyFamilies.Any(x => x.CatalogDeployment == key)) reasons.Add("TS.PERMISSION.DEPLOYMENT");
        Require(assignment.AssetType is "Futures" or "FuturesOptions", "TS.CONFIG.PERMISSION", "Unknown assignment asset type.");
        if (!authority.Fund.EligibleAssetTypes.Contains(assignment.AssetType, StringComparer.Ordinal) || !authority.Fund.PermittedTradeFamilies.Contains(assignment.TradeFamily, StringComparer.Ordinal)) reasons.Add("TS.PERMISSION.PRODUCT");
        if (authority.Portfolio.OperatingState != PortfolioOperatingState.Active || authority.Fund.OperatingState != FundOperatingState.Active) reasons.Add("TS.PERMISSION.OPERATING_STATE");
        var caps = authority.FinancialPolicy.TradeFamilyLimits.Where(x => x.CatalogDeployment == key && key is not null).ToArray();
        Require(caps.Length <= 1, "TS.CONFIG.INVALID", "Duplicate financial deployment limits.");
        if (caps.Length == 0 || !authority.FinancialPolicy.ResolveEffectiveCaps(key!, authority.RiskEnvelope, authority.ResolvedAtUtc).PermitsNewExposure) reasons.Add("TS.PERMISSION.ENVELOPE");
        return [.. reasons];
    }
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3267", Justification = "Preserve the established ordered catalog provenance validation loop without restructuring trading validation behavior.")]
    static void ValidateEnumeration(TradeSelectionBinding b)
    {
        if (b.SchemaVersion == 2)
        {
            var neutralExpected = new HashSet<string>(StringComparer.Ordinal);
            foreach (var graph in b.DeploymentSnapshots)
            {
                var deployment = b.CatalogDefinitions.Single(x => x.Key == graph.DeploymentKey);
                var assignmentVersion = Array.IndexOf(b.StrategyUniverse!.DeploymentKeys, graph.DeploymentKey) + 1;
                foreach (var product in deployment.Products.Where(x => string.Equals(x.Symbol,CommonPolicy(b).InstrumentRoot,StringComparison.Ordinal)))
                    foreach (var variantKey in deployment.Variants)
                    {
                        var variant = b.CatalogDefinitions.Single(x => x.Key == variantKey);
                        neutralExpected.Add(CandidateIdentity(new SelectionCandidateBinding
                        {
                            AssignmentVersion = assignmentVersion,
                            DeploymentKey = graph.DeploymentKey,
                            StrategyKey = deployment.Parent!,
                            StructureKey = variant.Parent!,
                            VariantKey = variantKey,
                            Product = product
                        }));
                    }
            }
            Require(neutralExpected.SetEquals(b.Candidates.Select(CandidateIdentity)) && b.ExcludedAssignments.Length == 0,
                "TS.CONFIG.INVALID", "Portfolio-neutral candidate enumeration is incomplete.");
            return;
        }
        var authority = b.PortfolioSnapshot;
        var expected = new HashSet<string>(StringComparer.Ordinal);
        var excluded = new List<SelectionAssignmentExclusion>();
        var expectedGraphs = new HashSet<CatalogKey>();
        foreach (var assignment in authority.Assignments)
        {
            var reasons = AssignmentExclusionReasons(authority, assignment);
            var key = assignment.TradeStrategyFamily?.CatalogDeployment;
            if (reasons.Length != 0) { excluded.Add(new() { AssignmentVersion = assignment.AssignmentVersion, DeploymentKey = key, ReasonCodes = reasons }); continue; }
            Require(expectedGraphs.Add(key!), "TS.CONFIG.INVALID", "Ambiguous effective assignment.");
            var deployment = b.CatalogDefinitions.SingleOrDefault(x => x.Key == key)
                ?? throw new TradeSelectionValidationException("TS.CONFIG.MISSING", "Authorized graph omitted.");
            Require(deployment.Products.Any(x => string.Equals(x.Symbol,CommonPolicy(b).InstrumentRoot,StringComparison.Ordinal)), "TS.CONFIG.INVALID", "Authorized deployment has no matching product.");
            foreach (var product in deployment.Products.Where(x => string.Equals(x.Symbol,CommonPolicy(b).InstrumentRoot,StringComparison.Ordinal)))
                foreach (var variantKey in deployment.Variants)
                {
                    var variant = b.CatalogDefinitions.Single(x => x.Key == variantKey);
                    expected.Add(CandidateIdentity(new() { AssignmentVersion = assignment.AssignmentVersion, DeploymentKey = key!, StrategyKey = deployment.Parent!, StructureKey = variant.Parent!, VariantKey = variantKey, Product = product }));
                }
        }
        Require(expected.SetEquals(b.Candidates.Select(CandidateIdentity)) && expectedGraphs.SetEquals(b.DeploymentSnapshots.Select(x => x.DeploymentKey)), "TS.CONFIG.INVALID", "Candidate enumeration is incomplete or unauthorized.");
        Require(excluded.Count == b.ExcludedAssignments.Length && excluded.All(x => b.ExcludedAssignments.Count(y => y.AssignmentVersion == x.AssignmentVersion && y.DeploymentKey == x.DeploymentKey && y.ReasonCodes.SequenceEqual(x.ReasonCodes)) == 1), "TS.CONFIG.INVALID", "Assignment exclusion evidence mismatch.");
    }
}

public static partial class TradeSelectionContracts
{
    public static void ValidatePipelinePolicy(SelectionPipelinePolicySnapshot row)
    {
        ArgumentNullException.ThrowIfNull(row);
        TradeSelectionPolicy.CheckJson(row.PayloadJson);
        (Guid Id, int Version, string Hash) identity;
        switch (row.Kind)
        {
            case CatalogPipelineParameterKind.RiskManagement:
                var risk = TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement.RiskParameterSet.Read(row.PayloadJson);
                Require(risk.SchemaVersion == row.SchemaVersion, "TS.CONTRACT.SCHEMA", "Risk schema metadata differs from its payload.");
                identity = (risk.ParameterSetId, risk.Version, risk.Hash()); break;
            case CatalogPipelineParameterKind.TradeSelection:
                var selection = TradeSelectionPolicy.Read(row.PayloadJson); identity = (selection.ParameterSetId, selection.Version, TradeSelectionPolicy.Hash(selection)); break;
            case CatalogPipelineParameterKind.OrderComposition:
                var composition = SelectionConstructionPolicy.Read(row.PayloadJson);
                Require(composition.SchemaVersion == row.SchemaVersion, "TS.CONTRACT.SCHEMA", "Construction schema metadata differs from its payload.");
                identity = (composition.ParameterSetId, composition.Version, composition.Hash()); break;
            case CatalogPipelineParameterKind.IntrinsicTimeStrategyWorkflow:
                var activation = TradeSelectionActivation.Read(row.PayloadJson); identity = (activation.ParameterSetId, activation.Version, activation.Hash()); break;
            case CatalogPipelineParameterKind.RegimeDiscovery:
                var regime = JsonSerializer.Deserialize<RegimeDiscoveryParameterSet>(row.PayloadJson) ?? throw new ArgumentException("Missing regime policy.",nameof(row));
                Require(new RegimeDiscoveryParameterSetValidationRules().Execute(regime).Length == 0, "TS.CONFIG.INVALID", "Invalid regime policy.");
                identity = (regime.ParameterSetId, regime.Version, RegimeDiscoveryParameterPayload.ComputeSha256(regime)); break;
            case CatalogPipelineParameterKind.MarketConditionAssessment:
                var assessment = JsonSerializer.Deserialize<MarketConditionAssessmentParameterSet>(row.PayloadJson) ?? throw new ArgumentException("Missing assessment policy.",nameof(row));
                assessment.Validate(); identity = (assessment.ParameterSetId, assessment.Version, MarketConditionAssessmentHash.Parameters(assessment)); break;
            default: throw new TradeSelectionValidationException("TS.CONFIG.CAPABILITY_UNSUPPORTED", "No implemented owning schema for required pipeline policy " + row.Kind);
        }
        Require(identity.Id == row.Id && identity.Version == row.Version && identity.Hash.Equals(row.PayloadSha256, StringComparison.OrdinalIgnoreCase), "TS.CONTRACT.HASH", "Pipeline payload metadata/hash mismatch.");
    }
}

public static partial class TradeSelectionContracts
{
    static void ValidateResultEvidence(TradeSelectionResult r, TradeSelectionParameterSet p)
    {
        var b = r.DecisionContext.SelectionBinding; var authority = b.PortfolioSnapshot;
        var bindingWorkflowId = b.SchemaVersion == 2 ? b.StrategyUniverse!.WorkflowId : authority.WorkflowId;
        var bindingRevision = b.SchemaVersion == 2 ? b.StrategyUniverse!.WorkflowRevision : authority.WorkflowRevision;
        Require(r.DecisionContext.SchemaVersion == 1 && r.ResultId != Guid.Empty && r.WorkflowId.Value == bindingWorkflowId && r.InputWorkflowRevision >= bindingRevision
            && r.EntityId.ItiSignalEntityId.TimePeriod == p.TargetHorizon && r.DecisionHorizon == p.TargetHorizon && r.TriggerEventId != Guid.Empty
            && (b.SchemaVersion == 2 ? r.PortfolioId == 0 && r.FundId == 0 : r.PortfolioId == authority.Portfolio.PortfolioId && r.FundId == authority.Fund.FundId)
            && SamePolicy(r.CommonPolicyReference, b.CommonPolicy), "TS.RESULT.INVALID", "Result identity/context mismatch.");
        Require(Utc(r.EvaluatedAtUtc) && Utc(r.ProducedAtUtc) && Utc(r.ValidUntilUtc) && r.ProducedAtUtc == r.EvaluatedAtUtc && r.EvaluatedAtUtc >= b.FrozenAtUtc
            && r.ValidUntilUtc > r.EvaluatedAtUtc && r.ValidUntilUtc <= b.ValidUntilUtc && r.ValidUntilUtc <= r.EvaluatedAtUtc.AddSeconds(p.ResultLifetimeSeconds), "TS.RESULT.INVALID", "Result validity mismatch.");
        var assessment = MarketConditionAssessmentContracts.ReadResult(r.DecisionContext.AssessmentResultEnvelope);
        var regimeEnvelope = r.DecisionContext.RegimeResultEnvelope;
        Require(regimeEnvelope.HasValidPayloadSha256() && string.Equals(regimeEnvelope.ResultType,nameof(RegimeDiscoveryResult),StringComparison.Ordinal), "TS.RESULT.INVALID", "Invalid result regime context.");
        var regime = regimeEnvelope.ReadRegimeResult();
        Require(assessment.WorkflowId == r.WorkflowId && assessment.EntityId == r.EntityId && assessment.TargetHorizon == r.DecisionHorizon
            && string.Equals(assessment.RegimePayloadSha256,regimeEnvelope.PayloadSha256,StringComparison.Ordinal) && r.ValidUntilUtc <= assessment.Assessment.ValidUntilUtc
            && r.SelectionConfidence == Math.Round(Math.Min(regime.Decision.Confidence, assessment.Assessment.AssessmentConfidence ?? -1), 6, MidpointRounding.ToEven)
            && r.SelectionConfidence is >= 0 and <= 1, "TS.RESULT.INVALID", "Result upstream/confidence mismatch.");
        var expectedGlobalCount = b.VolatilityInput is null ? 21 : 22;
        Require(r.GlobalEvidence.Select(x => x.RuleId).SequenceEqual(Enumerable.Range(1, expectedGlobalCount).Select(x => $"G{x:D2}"), StringComparer.Ordinal)
            && r.CandidateDecisions.Select(x => x.CandidateHash).SequenceEqual(b.Candidates.OrderBy(CandidateIdentity, StringComparer.Ordinal).Select(x => x.CandidateHash), StringComparer.Ordinal), "TS.RESULT.INVALID", "Incomplete or unordered decision evidence.");
        var volatilityGate = b.VolatilityInput is null ? null : VolatilityWorkflowGate.Evaluate(b.VolatilityInput);
        var expectedVolatility = b.VolatilityInput?.AcceptedEvidence is { } accepted && volatilityGate?.AllowsNewEntry == true
            ? accepted with { RuleOutcomeCode = volatilityGate.ReasonCode }
            : null;
        Require(r.DecisionContext.VolatilityInput == b.VolatilityInput
            && r.AcceptedVolatilityEvidence == expectedVolatility,
            "TS.RESULT.INVALID", "Volatility evidence differs from the frozen selection input.");
        foreach (var row in r.GlobalEvidence.Concat(r.CandidateDecisions.SelectMany(x => x.RuleEvidence)))
            Require(row.Status is SelectionRuleStatus.Passed or SelectionRuleStatus.Rejected && !string.IsNullOrWhiteSpace(row.FieldPath)
                && row.ActualJson.Length <= 4096 && row.ExpectedJson.Length <= 4096 && (row.Status == SelectionRuleStatus.Passed ? row.ReasonCode.Length == 0 : row.ReasonCode.StartsWith("TS.", StringComparison.Ordinal)), "TS.RESULT.INVALID", "Invalid rule evidence.");
        foreach (var decision in r.CandidateDecisions)
        {
            var c = b.Candidates.Single(x => string.Equals(x.CandidateHash,decision.CandidateHash,StringComparison.Ordinal)); var comparison = decision.Comparison;
            Require(Enum.IsDefined(decision.Status) && comparison is not null && comparison.Priority == c.AssignmentPriority && comparison.Deployment == c.DeploymentKey
                && comparison.Strategy == c.StrategyKey && comparison.Structure == c.StructureKey && comparison.Variant == c.VariantKey && comparison.ProductId == c.Product.ProductId
                && comparison.AssignmentVersion == c.AssignmentVersion, "TS.RESULT.INVALID", "Comparison identity mismatch.");
            Require(decision.Status == SelectionCandidateStatus.NotEvaluated ? decision.RuleEvidence.Length == 0 && r.GlobalEvidence.Any(x => x.Status == SelectionRuleStatus.Rejected)
                : decision.RuleEvidence.Select(x => x.RuleId).SequenceEqual(Enumerable.Range(1, 9).Select(x => $"C{x:D2}"), StringComparer.Ordinal), "TS.RESULT.INVALID", "Incomplete candidate evidence.");
            if (decision.Status is SelectionCandidateStatus.Selected or SelectionCandidateStatus.EligibleNotSelected)
                Require(decision.RuleEvidence.All(x => x.Status == SelectionRuleStatus.Passed) && r.GlobalEvidence.All(x => x.Status == SelectionRuleStatus.Passed), "TS.RESULT.INVALID", "Rejected candidate marked eligible.");
        }
        Require(r.CandidateDecisions.Count(x => x.Status == SelectionCandidateStatus.Selected) == (r.Outcome == SelectionOutcome.Selected ? 1 : 0)
            && !string.IsNullOrWhiteSpace(r.PrimaryReasonCode), "TS.RESULT.INVALID", "Outcome evidence mismatch.");
        if (r.SelectedCandidate is { } selected)
        {
            var c = b.Candidates.SingleOrDefault(x => string.Equals(x.CandidateHash,selected.CandidateHash,StringComparison.Ordinal));
            Require(c is not null && string.Equals(r.PrimaryReasonCode,"TS.SELECTED",StringComparison.Ordinal) && string.Equals(r.CandidateDecisions.Single(x => x.Status == SelectionCandidateStatus.Selected).CandidateHash,selected.CandidateHash,StringComparison.Ordinal)
                && selected.AssignmentVersion == c.AssignmentVersion && selected.DeploymentKey == c.DeploymentKey && selected.StrategyKey == c.StrategyKey && selected.StructureKey == c.StructureKey
                && selected.VariantKey == c.VariantKey && selected.Product == c.Product && selected.FamilyKeys.SequenceEqual(c.FamilyKeys) && selected.SpecializedParameterBindings.SequenceEqual(c.SpecializedParameterBindings)
                && selected.SelectionPolicyReference == c.SelectionPolicyReference && selected.CompositionPolicyReference == c.CompositionPolicyReference, "TS.RESULT.INVALID", "Selected intent differs from candidate.");
            var variant = b.CatalogDefinitions.Single(x => x.Key == selected.VariantKey);
            Require(string.Equals(selected.Side,variant.Side,StringComparison.Ordinal) && string.Equals(selected.Bias,variant.Bias,StringComparison.Ordinal) && string.Equals(selected.PremiumMode,variant.PremiumMode,StringComparison.Ordinal), "TS.RESULT.INVALID", "Selected variant semantics mismatch.");
        }
        else Require(!r.CandidateDecisions.Any(x => x.Status == SelectionCandidateStatus.EligibleNotSelected), "TS.RESULT.INVALID", "NoTrade contains an eligible candidate.");
    }
}
