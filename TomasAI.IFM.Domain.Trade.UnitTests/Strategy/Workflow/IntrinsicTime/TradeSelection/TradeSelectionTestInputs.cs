using MessagePack;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Workflow;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.TradeSelection;
internal static class TradeSelectionTestInputs
{
    internal static StrategyStageResultEnvelope Envelope(TradeSelectionResult r)=>StrategyStageResultEnvelope.Create(r.ResultId,nameof(TradeSelectionResult),1,MessagePackSerializer.Serialize(r),r.DecisionContext.AssessmentResultEnvelope.MarketDataAsOfUtc,r.ProducedAtUtc);
    internal static ExecuteTradeSelectionPipelineCommand Bind(ExecuteTradeSelectionPipelineCommand c,TradeSelectionBinding b)
    {
        b=TradeSelectionContracts.Seal(b);
        return c with {SelectionBinding=b,WorkflowView=c.WorkflowView with {SelectionBinding=b}};
    }
    internal static ExecuteTradeSelectionPipelineCommand Authority(ExecuteTradeSelectionPipelineCommand c,PortfolioFundStrategySnapshot a)
    {
        a=a with {PayloadSha256=PortfolioCanonicalHash.Compute(a with {PayloadSha256=""})};
        var excluded=a.Assignments.Select(x=>new SelectionAssignmentExclusion {AssignmentVersion=x.AssignmentVersion,DeploymentKey=x.TradeStrategyFamily?.CatalogDeployment,ReasonCodes=TradeSelectionContracts.AssignmentExclusionReasons(a,x)}).Where(x=>x.ReasonCodes.Length>0).ToArray();
        var allowed=a.Assignments.Where(x=>!excluded.Any(e=>e.AssignmentVersion==x.AssignmentVersion)).Select(x=>x.TradeStrategyFamily!.CatalogDeployment).ToHashSet();
        var graphs=c.SelectionBinding.DeploymentSnapshots.Where(x=>allowed.Contains(x.DeploymentKey)).ToArray();var keys=graphs.SelectMany(x=>x.DefinitionKeys).ToHashSet();
        return Bind(c,c.SelectionBinding with {PortfolioSnapshot=a,ExcludedAssignments=excluded,DeploymentSnapshots=graphs,CatalogDefinitions=c.SelectionBinding.CatalogDefinitions.Where(x=>keys.Contains(x.Key)).ToArray(),Candidates=c.SelectionBinding.Candidates.Where(x=>allowed.Contains(x.DeploymentKey)).ToArray()});
    }
    internal static ExecuteTradeSelectionPipelineCommand Evidence(ExecuteTradeSelectionPipelineCommand c,Action<RegimeDiscoveryDecision>? regimeChange=null,Action<HorizonAssessment>? assessmentChange=null)
    {
        var regime=c.RegimeResultEnvelope.ReadRegimeResult();var decision=regime.Decision;regimeChange?.Invoke(decision);regime=regime with {Decision=decision};
        var re=StrategyStageResultEnvelope.CreateRegime(regime);
        var assessment=MarketConditionAssessmentContracts.ReadResult(c.AssessmentResultEnvelope);
        assessment=assessment with {RegimePayloadSha256=re.PayloadSha256,Assessment=assessment.Assessment with {RegimePayloadSha256=re.PayloadSha256,UpstreamContext=regime.Decision,InheritedRestrictions=regime.Decision.Restrictions}};
        assessmentChange?.Invoke(assessment.Assessment);
        var ae=StrategyStageResultEnvelope.CreateAssessment(assessment);
        return c with {RegimeResultEnvelope=re,AssessmentResultEnvelope=ae,WorkflowView=c.WorkflowView with {RegimeDiscovery=c.WorkflowView.RegimeDiscovery with {Result=re},MarketCondition=c.WorkflowView.MarketCondition with {Result=ae}}};
    }
    internal static void Set<T>(T value,string name,object data)=>typeof(T).GetProperty(name)!.SetValue(value,data);
}
