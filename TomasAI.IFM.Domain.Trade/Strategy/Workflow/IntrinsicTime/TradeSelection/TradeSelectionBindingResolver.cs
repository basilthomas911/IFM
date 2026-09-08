using TomasAI.IFM.Application.Storage.ConfigurationDb;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
using static TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection.TradeSelectionContracts;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection;

public sealed class TradeSelectionBindingResolver(IConfigurationDbContext configuration)
{
    public async Task<TradeSelectionBinding> ResolveAsync(PortfolioFundStrategySnapshot authority,SelectionPipelinePolicyReference common,
        DateOnly tradeDate,CancellationToken cancellationToken=default)
    {
        Require(common is {Kind:CatalogPipelineParameterKind.TradeSelection,Role:""},"TS.CONFIG.PROFILE_MISMATCH","Activation must pin an exact common selection policy.");
        var at=authority.ResolvedAtUtc;
        var resolved=await configuration.ResolveTradeSelectionVersionAsync(common.Id,common.Version,common.PayloadSha256,at,cancellationToken).ConfigureAwait(false);
        var policy=resolved.ParameterSet;
        Require(authority.Assignments.Length<=policy.MaximumAssignments,"TS.CONFIG.CANDIDATE_LIMIT","Too many assignments.");
        Dictionary<CatalogKey,SelectionCatalogDefinitionSnapshot> nodes=[];
        Dictionary<(CatalogPipelineParameterKind,Guid,int),SelectionPipelinePolicySnapshot> policies=[];
        List<SelectionDeploymentSnapshot> graphs=[];List<SelectionCandidateBinding> candidates=[];List<SelectionAssignmentExclusion> exclusions=[];
        await AddPolicy(common).ConfigureAwait(false);
        foreach(var assignment in authority.Assignments)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var key=assignment.TradeStrategyFamily?.CatalogDeployment;
            var excluded=AssignmentExclusionReasons(authority,assignment);
            if(excluded.Length!=0){exclusions.Add(new(){AssignmentVersion=assignment.AssignmentVersion,DeploymentKey=key,ReasonCodes=excluded});continue;}
            var graph=await configuration.GetPublishedStrategyDeploymentAsync(key!,at,cancellationToken).ConfigureAwait(false);
            foreach(var source in graph.Definitions)
            {
                var node=SelectionCatalogTransport.From(source);
                if(nodes.TryGetValue(node.Key,out var prior))Require(EvidenceHash(prior)==EvidenceHash(node),"TS.CONTRACT.HASH","Conflicting shared catalog node.");
                else nodes.Add(node.Key,node);
                Require(nodes.Count<=policy.MaximumCatalogDefinitions,"TS.CONFIG.CANDIDATE_LIMIT","Too many catalog nodes.");
                foreach(var parameter in node.PipelineParameters)await AddPolicy(Reference(parameter)).ConfigureAwait(false);
            }
            var deployment=nodes[key!];var strategy=nodes[deployment.Parent!];
            var selected=UniquePolicy(deployment,CatalogPipelineParameterKind.TradeSelection);
            var composed=UniquePolicy(deployment,CatalogPipelineParameterKind.OrderComposition);
            Require(SamePolicy(selected,common),"TS.CONFIG.PROFILE_MISMATCH","Deployment common policy differs from activation.");
            var frozenGraph=new SelectionDeploymentSnapshot{DeploymentKey=key!,AsOfUtc=at,ContentHash=graph.ContentHash,DefinitionKeys=graph.Definitions.Select(x=>x.Definition.Key).OrderBy(KeyText,StringComparer.Ordinal).ToArray()};
            graphs.Add(frozenGraph);
            _=SelectionConstructionProfileReference.FromFrozen(policies[(composed.Kind,composed.Id,composed.Version)],composed,key!,nodes,at);
            foreach(var product in deployment.Products.Where(x=>x.Symbol==policy.InstrumentRoot))
            foreach(var variantKey in deployment.Variants)
            {
                var variant=nodes[variantKey];var structure=nodes[variant.Parent!];
                var classes=structure.Legs.Select(x=>x.InstrumentClass).Distinct().ToArray();
                Require(classes.Length==1 && classes[0] is "Futures" or "FuturesOption","TS.CONFIG.CAPABILITY_UNSUPPORTED","Unsupported traded instrument class.");
                Require(assignment.AssetType==(classes[0]=="FuturesOption"?"FuturesOptions":"Futures"),"TS.CONFIG.PROFILE_MISMATCH","Assignment traded asset differs from its structure.");
                var candidate=new SelectionCandidateBinding{SchemaVersion=1,AssignmentVersion=assignment.AssignmentVersion,AssignmentPriority=assignment.Priority,
                    DeploymentKey=key!,StrategyKey=strategy.Key,StructureKey=structure.Key,VariantKey=variantKey,Product=product,
                    SelectionPolicyReference=selected,CompositionPolicyReference=composed,SpecializedParameterBindings=deployment.Parameters,FamilyKeys=strategy.Families};
                candidates.Add(candidate with{CandidateHash=CandidateHash(candidate,frozenGraph)});
                Require(candidates.Count<=policy.MaximumCandidates,"TS.CONFIG.CANDIDATE_LIMIT","Too many candidates; no truncation is permitted.");
            }
        }
        var until=policies.Values.Select(x=>x.RetiredAtUtc??authority.ValidUntilUtc).Append(authority.ValidUntilUtc).Min();
        var binding=Seal(new(){SchemaVersion=1,PortfolioSnapshot=authority,CatalogDefinitions=[..nodes.Values],DeploymentSnapshots=[..graphs],PipelinePolicies=[..policies.Values],Candidates=[..candidates],ExcludedAssignments=[..exclusions],CommonPolicy=common,
            FrozenAtUtc=at,ValidUntilUtc=until,RequestedTradeDate=tradeDate,TradeDatePolicy="UTC.TriggerCreatedDate.Test.v1"});
        ValidateBinding(binding);
        return binding;
        async Task AddPolicy(SelectionPipelinePolicyReference reference)
        {
            var key=(reference.Kind,reference.Id,reference.Version);
            if(policies.TryGetValue(key,out var prior)){Require(prior.PayloadSha256==reference.PayloadSha256,"TS.CONTRACT.HASH","Conflicting pipeline policy identity.");return;}
            var value=await configuration.GetSelectionPipelinePolicyAsync(reference.Kind,reference.Id,reference.Version,cancellationToken).ConfigureAwait(false)
                ??throw new TradeSelectionValidationException("TS.CONFIG.MISSING","Exact pipeline policy is missing.");
            Require(value.PayloadSha256==reference.PayloadSha256,"TS.CONTRACT.HASH","Exact pipeline policy hash mismatch.");
            policies.Add(key,value);
        }
    }
    static SelectionPipelinePolicyReference UniquePolicy(SelectionCatalogDefinitionSnapshot deployment,CatalogPipelineParameterKind kind)
    {
        var found=deployment.PipelineParameters.Where(x=>x.Kind==kind).ToArray();
        Require(found.Length==1,"TS.CONFIG.PROFILE_MISMATCH","Exactly one pipeline binding of each required kind is required.");return Reference(found[0]);
    }
    static SelectionPipelinePolicyReference Reference(SelectionPipelineParameter p)=>new(){Kind=p.Kind,Id=p.Id,Version=p.Version,PayloadSha256=p.Hash,Role=p.Role};
}
