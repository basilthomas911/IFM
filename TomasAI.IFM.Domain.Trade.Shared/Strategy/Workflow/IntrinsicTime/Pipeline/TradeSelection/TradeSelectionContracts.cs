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

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;

public sealed class TradeSelectionValidationException(string code,string message):ArgumentException(code+": "+message)
{
    public string ReasonCode { get; }=code;
}

public static partial class TradeSelectionContracts
{
    public const int MaximumTransportBytes=1048576;
    public static void Require(bool condition,string code,string message) { if(!condition) throw new TradeSelectionValidationException(code,message); }
    public static bool Utc(DateTime value)=>value!=default && value.Kind==DateTimeKind.Utc;
    public static string KeyText(CatalogKey key)=>$"{(short)key.Kind:D2}.{key.Id:D}.{key.Version:D10}";
    public static string CandidateIdentity(SelectionCandidateBinding c)=>$"{c.DeploymentKey.Id:D}.{c.DeploymentKey.Version:D10}.{c.StrategyKey.Id:D}.{c.StrategyKey.Version:D10}.{c.StructureKey.Id:D}.{c.StructureKey.Version:D10}.{c.VariantKey.Id:D}.{c.VariantKey.Version:D10}.{c.Product.ProductId:D10}.{c.AssignmentVersion:D20}";
    public static string WireHash<T>(T value)=>Convert.ToHexStringLower(SHA256.HashData(MessagePackBinarySerializer.SerializeHistoricalContent(value)));
    // JSON event storage can normalize decimal scale (1m -> 1.0m). Hash numeric meaning
    // for typed evidence; envelope PayloadSha256 protects typed content or legacy payload bytes.
    public static string EvidenceHash<T>(T value)=>MarketConditionAssessmentHash.Compute(value).ToLowerInvariant();
    public static string BindingHash(TradeSelectionBinding b)=>EvidenceHash(b with { PayloadSha256="" });
    public static string CandidateHash(SelectionCandidateBinding c,SelectionDeploymentSnapshot graph)
    {
        using var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(Encoding.UTF8.GetBytes(MarketConditionAssessmentHash.Serialize(c with {CandidateHash=""})));
        hash.AppendData(Encoding.UTF8.GetBytes(graph.ContentHash));
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }
    public static bool SamePolicy(SelectionPipelinePolicyReference a,SelectionPipelinePolicyReference b)=>a.Kind==b.Kind && a.Id==b.Id && a.Version==b.Version && a.PayloadSha256==b.PayloadSha256;
    public static SelectionPipelinePolicySnapshot Policy(TradeSelectionBinding binding,SelectionPipelinePolicyReference reference)
        => binding.PipelinePolicies.SingleOrDefault(x=>x.Kind==reference.Kind && x.Id==reference.Id && x.Version==reference.Version && x.PayloadSha256==reference.PayloadSha256)
        ?? throw new TradeSelectionValidationException("TS.CONFIG.MISSING","Exact pipeline policy is absent.");
    public static TradeSelectionParameterSet CommonPolicy(TradeSelectionBinding b)=>TradeSelectionPolicy.Read(Policy(b,b.CommonPolicy).PayloadJson);
    public static TradeSelectionBinding Seal(TradeSelectionBinding source)
    {
        var b=source with
        {
            CatalogDefinitions=source.CatalogDefinitions.OrderBy(x=>KeyText(x.Key),StringComparer.Ordinal).ToArray(),
            DeploymentSnapshots=source.DeploymentSnapshots.OrderBy(x=>KeyText(x.DeploymentKey),StringComparer.Ordinal).ToArray(),
            PipelinePolicies=source.PipelinePolicies.OrderBy(x=>(short)x.Kind).ThenBy(x=>x.Id.ToString("D"),StringComparer.Ordinal).ThenBy(x=>x.Version).ToArray(),
            Candidates=source.Candidates.OrderBy(CandidateIdentity,StringComparer.Ordinal).ToArray(),
            ExcludedAssignments=source.ExcludedAssignments.OrderBy(x=>x.AssignmentVersion).ToArray(),PayloadSha256=""
        };
        return b with {PayloadSha256=BindingHash(b)};
    }
    public static TradeSelectionParameterSet ValidateBinding(TradeSelectionBinding b)
    {
        Require(b is not null && b.SchemaVersion==1,"TS.CONTRACT.SCHEMA","Explicit binding schema 1 is required.");
        Require(b.CommonPolicy is {Kind:CatalogPipelineParameterKind.TradeSelection},"TS.CONFIG.PROFILE_MISMATCH","Common selector policy is required.");
        var p=CommonPolicy(b);
        Require(p.ParameterSetId==b.CommonPolicy.Id && p.Version==b.CommonPolicy.Version && TradeSelectionPolicy.Hash(p)==b.CommonPolicy.PayloadSha256,"TS.CONTRACT.HASH","Common policy identity/hash mismatch.");
        Require(Utc(b.FrozenAtUtc) && Utc(b.ValidUntilUtc) && b.ValidUntilUtc>b.FrozenAtUtc && b.TradeDatePolicy=="UTC.TriggerCreatedDate.Test.v1","TS.CONTRACT.VALUE_RANGE","Invalid binding times/date policy.");
        Require(b.Candidates.Length<=p.MaximumCandidates && b.CatalogDefinitions.Length<=p.MaximumCatalogDefinitions && b.DeploymentSnapshots.Length<=p.MaximumAssignments && b.PortfolioSnapshot.Assignments.Length<=p.MaximumAssignments,"TS.CONFIG.CANDIDATE_LIMIT","Binding count limit exceeded.");
        Require(MessagePackBinarySerializer.MeasureContent(b)<=p.MaximumBindingPayloadBytes,"TS.CONTRACT.PAYLOAD_SIZE","Binding byte limit exceeded.");
        Require(b.PayloadSha256==BindingHash(b),"TS.CONTRACT.HASH","Binding hash mismatch.");
        var portfolio=b.PortfolioSnapshot;
        ValidateAuthority(portfolio,b);
        Require(portfolio.PayloadSha256==PortfolioCanonicalHash.Compute(portfolio with {PayloadSha256=""}),"TS.CONTRACT.HASH","Portfolio authority hash mismatch.");
        Require(portfolio.WorkflowId!=Guid.Empty && portfolio.WorkflowRevision>0 && portfolio.Fund.FundId>0 && portfolio.Portfolio.PortfolioId>0 && portfolio.Fund.PortfolioId==portfolio.Portfolio.PortfolioId,"TS.CONTRACT.IDENTITY","Invalid Portfolio/Fund identity.");
        Require(portfolio.ResolvedAtUtc==b.FrozenAtUtc && b.ValidUntilUtc<=portfolio.ValidUntilUtc && portfolio.Fund.DecisionHorizon==p.TargetHorizon.ToString(),"TS.CONTRACT.IDENTITY","Frozen authority horizon/time mismatch.");
        Require(b.CatalogDefinitions.Select(x=>x.Key).Distinct().Count()==b.CatalogDefinitions.Length && b.DeploymentSnapshots.Select(x=>x.DeploymentKey).Distinct().Count()==b.DeploymentSnapshots.Length && b.Candidates.Select(CandidateIdentity).Distinct().Count()==b.Candidates.Length,"TS.CONFIG.INVALID","Duplicate binding identity.");
        Require(b.PipelinePolicies.Select(x=>(x.Kind,x.Id,x.Version)).Distinct().Count()==b.PipelinePolicies.Length,"TS.CONFIG.INVALID","Duplicate pipeline policy identity.");
        var nodes=b.CatalogDefinitions.ToDictionary(x=>x.Key,SelectionCatalogTransport.ToSource);
        foreach(var node in b.CatalogDefinitions)
        {
            Require(node.SchemaVersion==1 && node.Status==CatalogLifecycleStatus.Published && node.EffectiveFromUtc is not null && node.EffectiveFromUtc<=b.FrozenAtUtc && (node.RetiredAtUtc is null || node.RetiredAtUtc>b.FrozenAtUtc),"TS.CONFIG.INVALID","Catalog node is not published/effective at binding.");
            Require(StrategyCatalogValidation.ContentHash(nodes[node.Key].Definition)==node.ContentHash,"TS.CONTRACT.HASH","Catalog source hash mismatch.");
            StrategyCatalogValidation.ValidateForPublication(nodes[node.Key].Definition,nodes);
        }
        foreach(var graph in b.DeploymentSnapshots)
        {
            Require(graph.DefinitionKeys.Distinct().Count()==graph.DefinitionKeys.Length && graph.DefinitionKeys.All(nodes.ContainsKey) && graph.DefinitionKeys.Contains(graph.DeploymentKey) && graph.AsOfUtc==b.FrozenAtUtc,"TS.CONFIG.INVALID","Invalid deployment graph membership.");
            var seen=new HashSet<CatalogKey>(); var visiting=new HashSet<CatalogKey>();
            void Visit(CatalogKey key,int depth)
            {
                Require(depth<=32 && graph.DefinitionKeys.Contains(key),"TS.CONFIG.INVALID","Missing/cyclic/deep graph dependency.");
                Require(!visiting.Contains(key),"TS.CONFIG.INVALID","Cyclic graph dependency.");
                if(!seen.Add(key)) return;
                visiting.Add(key);
                foreach(var child in StrategyCatalogValidation.Dependencies(nodes[key].Definition)) Visit(child,depth+1);
                visiting.Remove(key);
            }
            Visit(graph.DeploymentKey,0);
            Require(seen.Count==graph.DefinitionKeys.Length && SelectionCatalogTransport.GraphHash(graph.DeploymentKey,graph.DefinitionKeys.Select(k=>nodes[k]))==graph.ContentHash,"TS.CONTRACT.HASH","Deployment graph mismatch.");
        }
        foreach(var policy in b.PipelinePolicies)
        {
            Require(Enum.IsDefined(policy.Kind) && policy.Id!=Guid.Empty && policy.Version>0
                && (policy.SchemaVersion==1 || policy.Kind==CatalogPipelineParameterKind.OrderComposition && policy.SchemaVersion==2)
                && policy.Status==CatalogLifecycleStatus.Published && policy.EffectiveFromUtc is not null && policy.EffectiveFromUtc<=b.FrozenAtUtc && (policy.RetiredAtUtc is null || policy.RetiredAtUtc>b.FrozenAtUtc),"TS.CONFIG.INVALID","Pipeline policy is not exact published evidence.");
            ValidatePipelinePolicy(policy);
        }
        foreach(var candidate in b.Candidates) ValidateCandidate(b,candidate,nodes,p);
        ValidateEnumeration(b);
        return p;
    }
    static void ValidateCandidate(TradeSelectionBinding b,SelectionCandidateBinding c,IReadOnlyDictionary<CatalogKey,StoredStrategyCatalogDefinition> nodes,TradeSelectionParameterSet p)
    {
        Require(c.SchemaVersion==1 && c.AssignmentVersion>0 && c.AssignmentPriority>=0,"TS.CONTRACT.IDENTITY","Invalid candidate schema/assignment.");
        var graph=b.DeploymentSnapshots.Single(x=>x.DeploymentKey==c.DeploymentKey);
        Require(c.CandidateHash==CandidateHash(c,graph),"TS.CONTRACT.HASH","Candidate evidence hash mismatch.");
        var deployment=nodes[c.DeploymentKey].Definition; var strategy=nodes[c.StrategyKey].Definition; var structure=nodes[c.StructureKey].Definition; var variant=nodes[c.VariantKey].Definition;
        Require(c.DeploymentKey.Kind==StrategyCatalogKind.Deployment && c.StrategyKey.Kind==StrategyCatalogKind.Strategy && c.StructureKey.Kind==StrategyCatalogKind.Structure && c.VariantKey.Kind==StrategyCatalogKind.Variant && deployment.Parent==c.StrategyKey && variant.Parent==c.StructureKey && strategy.Structures.Contains(c.StructureKey) && deployment.Variants.Contains(c.VariantKey),"TS.CONTRACT.IDENTITY","Candidate graph relationships do not match.");
        Require(deployment.Horizon==p.TargetHorizon && c.Product.ProductId>0 && c.Product.Symbol==p.InstrumentRoot && c.Product.Currency=="USD" && !string.IsNullOrWhiteSpace(c.Product.Exchange) && deployment.Products.Contains(SelectionCatalogTransport.ToSource(c.Product)),"TS.CONTRACT.IDENTITY","Candidate product/horizon mismatch.");
        Require(c.FamilyKeys.SequenceEqual(strategy.Families) && c.SpecializedParameterBindings.Select(SelectionCatalogTransport.ToSource).SequenceEqual(deployment.Parameters),"TS.CONTRACT.IDENTITY","Candidate family/parameter provenance mismatch.");
        foreach(var pair in new[]{(c.SelectionPolicyReference,CatalogPipelineParameterKind.TradeSelection),(c.CompositionPolicyReference,CatalogPipelineParameterKind.OrderComposition)})
        {
            var declared=deployment.PipelineParameters.Where(x=>x.Kind==pair.Item2).ToArray();
            Require(declared.Length==1 && pair.Item1.Kind==pair.Item2 && declared[0]==new CatalogPipelineParameter(pair.Item1.Role,pair.Item1.Kind,pair.Item1.Id,pair.Item1.Version,pair.Item1.PayloadSha256),"TS.CONFIG.PROFILE_MISMATCH","Ambiguous/mismatched deployment pipeline role.");
            _=Policy(b,pair.Item1);
        }
        foreach(var parameter in c.SpecializedParameterBindings)
        {
            var set=nodes[parameter.ParameterSet].Definition;var schema=nodes[set.Parent!].Definition;
            Require(schema.Key.Kind==StrategyCatalogKind.ParameterSchema && schema.Capabilities.Any(x=>x.Role=="validator"),"TS.CONFIG.CAPABILITY_UNSUPPORTED","Required specialized role has no owning semantic validator.");
            if(parameter.Role=="TradeSelectionVariants")
            {
                Require(schema.Capabilities.Any(x=>x.Role=="validator" && x.Code=="TradeSelectionVariants" && x.Version==1),"TS.CONFIG.CAPABILITY_UNSUPPORTED","Selector specialized role must use its exact implemented validator.");
                _=TradeSelectionPolicy.ReadSpecialized(set.Settings.GetRawText(),p);
            }
        }
        Require(SamePolicy(c.SelectionPolicyReference,b.CommonPolicy),"TS.CONFIG.PROFILE_MISMATCH","Candidates must share the pinned common policy.");
        var assignment=b.PortfolioSnapshot.Assignments.SingleOrDefault(x=>x.AssignmentVersion==c.AssignmentVersion && x.TradeStrategyFamily?.CatalogDeployment==c.DeploymentKey);
        Require(assignment is not null && assignment.SchemaVersion==3 && assignment.TradeTemplateId==c.DeploymentKey.Id && assignment.TradeTemplateVersion==c.DeploymentKey.Version && assignment.Priority==c.AssignmentPriority && assignment.TradeSelectionHintProfileId==c.SelectionPolicyReference.Id && assignment.TradeSelectionHintProfileVersion==c.SelectionPolicyReference.Version && assignment.OrderCompositionProfileId==c.CompositionPolicyReference.Id && assignment.OrderCompositionProfileVersion==c.CompositionPolicyReference.Version,"TS.CONTRACT.IDENTITY","Candidate assignment/profile identity mismatch.");
    }
    public static (TradeSelectionParameterSet Policy,MarketConditionAssessmentResult Assessment,RegimeDiscoveryResult Regime) ValidateRequest(ExecuteTradeSelectionPipelineCommand c)
        => ValidateRequestEvidence(c, null);

    /// <summary>Appends cross-field evidence errors while sharing the same rules with non-actor contract consumers.</summary>
    internal static (TradeSelectionParameterSet Policy,MarketConditionAssessmentResult Assessment,RegimeDiscoveryResult Regime) ValidateRequestEvidence(
        ExecuteTradeSelectionPipelineCommand c, List<TomasAI.IFM.Shared.Validation.ValidationError>? errors)

    {
        void Check(bool valid, string code, string message)
        {
            if (errors is null) Require(valid, code, message);
            else if (!valid) errors.Add(new(code, code + ": " + message));
        }
        Check(c is not null && c.SchemaVersion==1 && c.CommandId!=Guid.Empty && !c.PostEvents,"TS.CONTRACT.SCHEMA","A schema-1 Function request is required.");
        Check(c.Subject.ActorType==ActorType.Function && c.Subject.Name==ExecuteTradeSelectionPipelineCommand.Actor && c.Subject.Verb==ExecuteTradeSelectionPipelineCommand.Verb && c.Subject.EntityId==c.EntityId.Format(),"TS.CONTRACT.IDENTITY","Function subject mismatch.");
        var v=c.WorkflowView; var p=ValidateBinding(c.SelectionBinding);
        Check(c.EntityId.InputWorkflowRevision==c.InputWorkflowRevision && c.InputWorkflowRevision>0 && c.WorkflowId==v.WorkflowId && c.WorkflowEntityId==v.EntityId && c.InputWorkflowRevision==v.WorkflowRevision && c.WorkflowId.Value==c.SelectionBinding.PortfolioSnapshot.WorkflowId && v.SelectionBinding?.PayloadSha256==c.SelectionBinding.PayloadSha256,"TS.CONTRACT.IDENTITY","Frozen workflow identity mismatch.");
        Check(v.Status==WorkflowStrategyMachineStatus.Started && v.CurrentStage==StrategyWorkflowStage.TradeSelection && c.TriggerEvent.Id==v.TriggerEventId && c.TriggerEvent.EntityId==v.TriggerEvent.EntityId && c.TriggerEvent.EntityId.TimePeriod==p.TargetHorizon,"TS.UPSTREAM.INVALID","Workflow stage/trigger/horizon mismatch.");
        Check(c.RegimeResultEnvelope.PayloadSha256==v.RegimeDiscovery.Result?.PayloadSha256 && c.AssessmentResultEnvelope.PayloadSha256==v.MarketCondition.Result?.PayloadSha256,"TS.UPSTREAM.INVALID","Upstream envelopes differ from accepted workflow.");
        Check(c.CorrelationId!=Guid.Empty && c.CorrelationId==v.CorrelationId && c.CorrelationId==c.SelectionBinding.PortfolioSnapshot.CorrelationId && c.CausationId!=Guid.Empty,
            "TS.CONTRACT.IDENTITY","Workflow correlation/causation mismatch.");
        Check(v.RegimeDiscovery.ProcessingStatus==StrategyActorProcessingStatus.Completed && v.MarketCondition.ProcessingStatus==StrategyActorProcessingStatus.Completed
            && v.TradeSelection.ProcessingStatus==StrategyActorProcessingStatus.Processing && v.TradeSelection.InputWorkflowRevision==c.InputWorkflowRevision,
            "TS.UPSTREAM.INVALID","Upstream results must have been accepted and selection must be processing.");
        Check(EvidenceHash(c.TriggerEvent)==EvidenceHash(v.TriggerEvent) && c.RegimeResultEnvelope.HasSameContent(v.RegimeDiscovery.Result) && c.AssessmentResultEnvelope.HasSameContent(v.MarketCondition.Result),"TS.UPSTREAM.INVALID","Accepted input metadata differs.");
        var assessment=MarketConditionAssessmentContracts.ReadResult(c.AssessmentResultEnvelope);
        MarketConditionAssessmentContracts.ValidateAcceptance(assessment,v,v.MarketCondition.InputWorkflowRevision);
        Check(assessment.Assessment.Availability==AssessmentAvailability.Available && !assessment.Assessment.InheritedRestrictions.Contains(RegimeRestriction.NoNewTrade),"TS.UPSTREAM.NOT_ELIGIBLE","Assessment is unavailable or restricted.");
        Check(c.RegimeResultEnvelope.HasValidPayloadSha256() && c.RegimeResultEnvelope.ResultType==nameof(RegimeDiscoveryResult),"TS.UPSTREAM.INVALID","Invalid regime envelope.");
        var regime=c.RegimeResultEnvelope.ReadRegimeResult();
        Check(Utc(c.RequestedAtUtc) && Utc(c.EvaluatedAtUtc) && Utc(c.ExpiresAtUtc) && c.EvaluatedAtUtc>=c.RequestedAtUtc && c.EvaluatedAtUtc<c.ExpiresAtUtc && c.ExpiresAtUtc<=v.ExpiresAtUtc && c.ExpiresAtUtc<=c.SelectionBinding.ValidUntilUtc && c.ExpiresAtUtc<=assessment.Assessment.ValidUntilUtc && c.ExpiresAtUtc<=c.RequestedAtUtc.AddMilliseconds(p.MaximumExecutionMilliseconds),"TS.TIME.EXPIRED","Invalid execution times/deadline.");
        var future=c.EvaluatedAtUtc.AddSeconds(p.FutureClockSkewSeconds);
        Check(c.SelectionBinding.FrozenAtUtc<=c.EvaluatedAtUtc && v.UpdatedAtUtc<=future && v.StartedAtUtc<=future && c.TriggerEvent.CreatedOn<=future
            && c.RegimeResultEnvelope.ProducedAtUtc<=future && c.AssessmentResultEnvelope.ProducedAtUtc<=future,"TS.TIME.CLOCK_SKEW","Frozen evidence is in the future.");
        foreach(var row in c.SelectionBinding.PipelinePolicies)
        {
            if(row.Kind==CatalogPipelineParameterKind.RegimeDiscovery)
                Check(row.Id==v.RegimeDiscoveryParameterSet.ParameterSetId && row.Version==v.RegimeDiscoveryParameterSet.Version && row.PayloadSha256==v.RegimeDiscoveryParameterPayloadSha256,"TS.CONFIG.PROFILE_MISMATCH","Deployment regime profile differs from accepted upstream.");
            if(row.Kind==CatalogPipelineParameterKind.MarketConditionAssessment)
                Check(row.Id==assessment.ParameterSetId && row.Version==assessment.ParameterSetVersion && row.PayloadSha256==assessment.ParameterPayloadSha256,"TS.CONFIG.PROFILE_MISMATCH","Deployment assessment profile differs from accepted upstream.");
        }
        Check(c.SelectionBinding.RequestedTradeDate==DateOnly.FromDateTime(c.TriggerEvent.CreatedOn) && Utc(c.TriggerEvent.CreatedOn),"TS.CONTRACT.IDENTITY","Trade date must use the original UTC trigger.");
        Check(MessagePackBinarySerializer.MeasureContent(c)<=MaximumTransportBytes && MessagePackBinarySerializer.MeasureEncoded(c)<=MaximumTransportBytes,"TS.CONTRACT.PAYLOAD_SIZE","Function transport limit exceeded.");
        return (p,assessment,regime);
    }
    /// <summary>Compares projected completion evidence across legacy and typed envelopes, ignoring only stream sequence.</summary>
    public static bool SameCompletion(Events.TradeSelectionFunctionCompletedEvent left, Events.TradeSelectionFunctionCompletedEvent right)
    {
        var leftResult = ReadResult(left.Result);
        var rightResult = ReadResult(right.Result);
        var a = left with { EventId = 0, Result = StrategyStageResultEnvelope.CreateSelection(leftResult, 524288) };
        var b = right with { EventId = 0, Result = StrategyStageResultEnvelope.CreateSelection(rightResult, 524288) };
        return EvidenceHash(a) == EvidenceHash(b);
    }

    public static TradeSelectionResult ReadResult(StrategyStageResultEnvelope envelope)
    {
        Require(envelope is not null && envelope.ResultType==nameof(TradeSelectionResult) && envelope.SchemaVersion==1 && envelope.HasValidPayloadSha256() && envelope.ContentSize<=524288,"TS.RESULT.INVALID","Invalid selector result envelope.");
        var result=envelope.ReadSelectionResult();
        var p=ValidateBinding(result.DecisionContext.SelectionBinding);
        Require(result.SchemaVersion==1 && result.ResultId==envelope.ResultId && result.ResultId==result.InvocationId && result.ProducedAtUtc==envelope.ProducedAtUtc && envelope.MarketDataAsOfUtc==result.DecisionContext.AssessmentResultEnvelope.MarketDataAsOfUtc && envelope.ContentSize<=p.MaximumResultPayloadBytes && result.Outcome is SelectionOutcome.Selected or SelectionOutcome.NoTrade && (result.SelectedCandidate is not null)==(result.Outcome==SelectionOutcome.Selected) && result.SummaryText.Length<=2048 && result.CompatibilityScore is null,"TS.RESULT.INVALID","Invalid selector result invariants.");
        ValidateResultEvidence(result,p);
        return result;
    }
}
