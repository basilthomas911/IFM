using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
public static partial class TradeSelectionContracts
{
    public static string[] AssignmentExclusionReasons(PortfolioFundStrategySnapshot authority,FundTradeTemplateAssignmentReadModel assignment)
    {
        List<string> reasons=[];
        if(!assignment.Enabled) reasons.Add("TS.ASSIGNMENT.DISABLED");
        var key=assignment.TradeStrategyFamily?.CatalogDeployment;
        if(assignment.Enabled) Require(assignment.SchemaVersion==3 && key is not null && key.Kind==StrategyCatalogKind.Deployment && key.Id!=Guid.Empty && key.Version>0,"TS.CONFIG.INVALID","Enabled assignment must reference an exact deployment.");
        if(key is null || !authority.Fund.PermittedTradeStrategyFamilies.Any(x=>x.CatalogDeployment==key)) reasons.Add("TS.PERMISSION.DEPLOYMENT");
        Require(assignment.AssetType is "Futures" or "FuturesOptions","TS.CONFIG.PERMISSION","Unknown assignment asset type.");
        if(!authority.Fund.EligibleAssetTypes.Contains(assignment.AssetType,StringComparer.Ordinal) || !authority.Fund.PermittedTradeFamilies.Contains(assignment.TradeFamily,StringComparer.Ordinal)) reasons.Add("TS.PERMISSION.PRODUCT");
        if(authority.Portfolio.OperatingState!=PortfolioOperatingState.Active || authority.Fund.OperatingState!=FundOperatingState.Active) reasons.Add("TS.PERMISSION.OPERATING_STATE");
        var caps=authority.FinancialPolicy.TradeFamilyLimits.Where(x=>x.CatalogDeployment==key && key is not null).ToArray();
        Require(caps.Length<=1,"TS.CONFIG.INVALID","Duplicate financial deployment limits.");
        if(caps.Length==0 || !authority.FinancialPolicy.ResolveEffectiveCaps(key!,authority.RiskEnvelope,authority.ResolvedAtUtc).PermitsNewExposure) reasons.Add("TS.PERMISSION.ENVELOPE");
        return [..reasons];
    }
    static void ValidateEnumeration(TradeSelectionBinding b)
    {
        if (b.SchemaVersion == 2)
        {
            var neutralExpected = new HashSet<string>(StringComparer.Ordinal);
            foreach (var graph in b.DeploymentSnapshots)
            {
                var deployment = b.CatalogDefinitions.Single(x => x.Key == graph.DeploymentKey);
                var assignmentVersion = Array.IndexOf(b.StrategyUniverse!.DeploymentKeys, graph.DeploymentKey) + 1;
                foreach (var product in deployment.Products.Where(x => x.Symbol == CommonPolicy(b).InstrumentRoot))
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
        var authority=b.PortfolioSnapshot;
        var expected=new HashSet<string>(StringComparer.Ordinal);
        var excluded=new List<SelectionAssignmentExclusion>();
        var expectedGraphs=new HashSet<CatalogKey>();
        foreach(var assignment in authority.Assignments)
        {
            var reasons=AssignmentExclusionReasons(authority,assignment);
            var key=assignment.TradeStrategyFamily?.CatalogDeployment;
            if(reasons.Length!=0) {excluded.Add(new(){AssignmentVersion=assignment.AssignmentVersion,DeploymentKey=key,ReasonCodes=reasons});continue;}
            Require(expectedGraphs.Add(key!),"TS.CONFIG.INVALID","Ambiguous effective assignment.");
            var deployment=b.CatalogDefinitions.SingleOrDefault(x=>x.Key==key)
                ?? throw new TradeSelectionValidationException("TS.CONFIG.MISSING","Authorized graph omitted.");
            Require(deployment.Products.Any(x=>x.Symbol==CommonPolicy(b).InstrumentRoot),"TS.CONFIG.INVALID","Authorized deployment has no matching product.");
            foreach(var product in deployment.Products.Where(x=>x.Symbol==CommonPolicy(b).InstrumentRoot))
            foreach(var variantKey in deployment.Variants)
            {
                var variant=b.CatalogDefinitions.Single(x=>x.Key==variantKey);
                expected.Add(CandidateIdentity(new(){AssignmentVersion=assignment.AssignmentVersion,DeploymentKey=key!,StrategyKey=deployment.Parent!,StructureKey=variant.Parent!,VariantKey=variantKey,Product=product}));
            }
        }
        Require(expected.SetEquals(b.Candidates.Select(CandidateIdentity)) && expectedGraphs.SetEquals(b.DeploymentSnapshots.Select(x=>x.DeploymentKey)),"TS.CONFIG.INVALID","Candidate enumeration is incomplete or unauthorized.");
        Require(excluded.Count==b.ExcludedAssignments.Length && excluded.All(x=>b.ExcludedAssignments.Count(y=>y.AssignmentVersion==x.AssignmentVersion && y.DeploymentKey==x.DeploymentKey && y.ReasonCodes.SequenceEqual(x.ReasonCodes))==1),"TS.CONFIG.INVALID","Assignment exclusion evidence mismatch.");
    }
}
