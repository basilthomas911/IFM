using MessagePack;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
[MessagePackObject]
public sealed record SelectionConstructionVariant([property:Key(0)] CatalogKey Structure,[property:Key(1)] CatalogKey Variant);
/// <summary>Derived constraints/provenance; never a claim that a builder is registered.</summary>
[MessagePackObject]
public sealed record SelectionConstructionProfileReference
{
    [Key(0)] public short SchemaVersion {get;init;}
    [Key(1)] public SelectionPipelinePolicyReference CompositionPolicyReference {get;init;}
    [Key(2)] public CatalogKey DeploymentKey {get;init;}
    SelectionConstructionVariant[] _variants=[];
    [Key(3)] public SelectionConstructionVariant[] AllowedVariants {get=>[.._variants];init=>_variants=[..value];}
    SelectionCapability[] _capabilities=[];
    [Key(4)] public SelectionCapability[] RequiredCapabilities {get=>[.._capabilities];init=>_capabilities=[..value];}
    [Key(5)] public string SourcePayloadJson {get;init;}="";
    [Key(6)] public DateTime EffectiveFromUtc {get;init;}
    public static SelectionConstructionProfileReference FromFrozen(SelectionPipelinePolicySnapshot row,SelectionPipelinePolicyReference reference,CatalogKey deploymentKey,IReadOnlyDictionary<CatalogKey,SelectionCatalogDefinitionSnapshot> nodes,DateTime at)
    {
        TradeSelectionContracts.ValidatePipelinePolicy(row);
        TradeSelectionContracts.Require(row.Kind==CatalogPipelineParameterKind.OrderComposition && reference.Kind==row.Kind && reference.Id==row.Id && reference.Version==row.Version && reference.PayloadSha256==row.PayloadSha256
            && row.Status==CatalogLifecycleStatus.Published && row.EffectiveFromUtc is not null && row.EffectiveFromUtc<=at && !(row.RetiredAtUtc<=at),"TS.CONFIG.COMPOSITION_SCHEMA","Exact effective construction policy required.");
        var deployment=nodes[deploymentKey];var policy=SelectionConstructionPolicy.Read(row.PayloadJson);
        var variants=deployment.Variants.Select(key=>new SelectionConstructionVariant(nodes[key].Parent!,key)).OrderBy(x=>TradeSelectionContracts.KeyText(x.Variant),StringComparer.Ordinal).ToArray();
        foreach(var variant in variants)policy.ValidateCandidate(nodes[variant.Structure],nodes[variant.Variant]);
        return new(){SchemaVersion=1,CompositionPolicyReference=reference,DeploymentKey=deploymentKey,AllowedVariants=variants,
            RequiredCapabilities=variants.SelectMany(x=>nodes[x.Structure].Capabilities).Concat(deployment.Capabilities).Distinct().OrderBy(x=>x.Role,StringComparer.Ordinal).ThenBy(x=>x.Code,StringComparer.Ordinal).ThenBy(x=>x.Version).ToArray(),
            SourcePayloadJson=policy.Serialize(),EffectiveFromUtc=row.EffectiveFromUtc.Value};
    }
}
