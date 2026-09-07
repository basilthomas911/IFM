using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.TradeSelection;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;
using MessagePack;
namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
public static partial class TradeSelectionContracts
{
    static void ValidateResultEvidence(TradeSelectionResult r,TradeSelectionParameterSet p)
    {
        var b=r.DecisionContext.SelectionBinding;var authority=b.PortfolioSnapshot;
        Require(r.DecisionContext.SchemaVersion==1 && r.ResultId!=Guid.Empty && r.WorkflowId.Value==authority.WorkflowId && r.InputWorkflowRevision>authority.WorkflowRevision
            && r.EntityId.ItiSignalEntityId.TimePeriod==p.TargetHorizon && r.DecisionHorizon==p.TargetHorizon && r.TriggerEventId!=Guid.Empty
            && r.PortfolioId==authority.Portfolio.PortfolioId && r.FundId==authority.Fund.FundId && SamePolicy(r.CommonPolicyReference,b.CommonPolicy),"TS.RESULT.INVALID","Result identity/context mismatch.");
        Require(Utc(r.EvaluatedAtUtc) && Utc(r.ProducedAtUtc) && Utc(r.ValidUntilUtc) && r.ProducedAtUtc==r.EvaluatedAtUtc && r.EvaluatedAtUtc>=b.FrozenAtUtc
            && r.ValidUntilUtc>r.EvaluatedAtUtc && r.ValidUntilUtc<=b.ValidUntilUtc && r.ValidUntilUtc<=r.EvaluatedAtUtc.AddSeconds(p.ResultLifetimeSeconds),"TS.RESULT.INVALID","Result validity mismatch.");
        var assessment=MarketConditionAssessmentContracts.ReadResult(r.DecisionContext.AssessmentResultEnvelope);
        var regimeEnvelope=r.DecisionContext.RegimeResultEnvelope;
        Require(regimeEnvelope.HasValidPayloadSha256() && regimeEnvelope.ResultType==nameof(RegimeDiscoveryResult),"TS.RESULT.INVALID","Invalid result regime context.");
        var regime=regimeEnvelope.ReadRegimeResult();
        Require(assessment.WorkflowId==r.WorkflowId && assessment.EntityId==r.EntityId && assessment.TargetHorizon==r.DecisionHorizon
            && assessment.RegimePayloadSha256==regimeEnvelope.PayloadSha256 && r.ValidUntilUtc<=assessment.Assessment.ValidUntilUtc
            && r.SelectionConfidence==Math.Round(Math.Min(regime.Decision.Confidence,assessment.Assessment.AssessmentConfidence??-1),6,MidpointRounding.ToEven)
            && r.SelectionConfidence is >=0 and <=1,"TS.RESULT.INVALID","Result upstream/confidence mismatch.");
        Require(r.GlobalEvidence.Select(x=>x.RuleId).SequenceEqual(Enumerable.Range(1,21).Select(x=>$"G{x:D2}"))
            && r.CandidateDecisions.Select(x=>x.CandidateHash).SequenceEqual(b.Candidates.OrderBy(CandidateIdentity,StringComparer.Ordinal).Select(x=>x.CandidateHash)),"TS.RESULT.INVALID","Incomplete or unordered decision evidence.");
        foreach(var row in r.GlobalEvidence.Concat(r.CandidateDecisions.SelectMany(x=>x.RuleEvidence)))
            Require(row.Status is SelectionRuleStatus.Passed or SelectionRuleStatus.Rejected && !string.IsNullOrWhiteSpace(row.FieldPath)
                && row.ActualJson.Length<=4096 && row.ExpectedJson.Length<=4096 && (row.Status==SelectionRuleStatus.Passed?row.ReasonCode.Length==0:row.ReasonCode.StartsWith("TS.",StringComparison.Ordinal)),"TS.RESULT.INVALID","Invalid rule evidence.");
        foreach(var decision in r.CandidateDecisions)
        {
            var c=b.Candidates.Single(x=>x.CandidateHash==decision.CandidateHash);var comparison=decision.Comparison;
            Require(Enum.IsDefined(decision.Status) && comparison is not null && comparison.Priority==c.AssignmentPriority && comparison.Deployment==c.DeploymentKey
                && comparison.Strategy==c.StrategyKey && comparison.Structure==c.StructureKey && comparison.Variant==c.VariantKey && comparison.ProductId==c.Product.ProductId
                && comparison.AssignmentVersion==c.AssignmentVersion,"TS.RESULT.INVALID","Comparison identity mismatch.");
            Require(decision.Status==SelectionCandidateStatus.NotEvaluated ? decision.RuleEvidence.Length==0 && r.GlobalEvidence.Any(x=>x.Status==SelectionRuleStatus.Rejected)
                : decision.RuleEvidence.Select(x=>x.RuleId).SequenceEqual(Enumerable.Range(1,9).Select(x=>$"C{x:D2}")),"TS.RESULT.INVALID","Incomplete candidate evidence.");
            if(decision.Status is SelectionCandidateStatus.Selected or SelectionCandidateStatus.EligibleNotSelected)
                Require(decision.RuleEvidence.All(x=>x.Status==SelectionRuleStatus.Passed) && r.GlobalEvidence.All(x=>x.Status==SelectionRuleStatus.Passed),"TS.RESULT.INVALID","Rejected candidate marked eligible.");
        }
        Require(r.CandidateDecisions.Count(x=>x.Status==SelectionCandidateStatus.Selected)==(r.Outcome==SelectionOutcome.Selected?1:0)
            && !string.IsNullOrWhiteSpace(r.PrimaryReasonCode),"TS.RESULT.INVALID","Outcome evidence mismatch.");
        if(r.SelectedCandidate is {} selected)
        {
            var c=b.Candidates.SingleOrDefault(x=>x.CandidateHash==selected.CandidateHash);
            Require(c is not null && r.PrimaryReasonCode=="TS.SELECTED" && r.CandidateDecisions.Single(x=>x.Status==SelectionCandidateStatus.Selected).CandidateHash==selected.CandidateHash
                && selected.AssignmentVersion==c.AssignmentVersion && selected.DeploymentKey==c.DeploymentKey && selected.StrategyKey==c.StrategyKey && selected.StructureKey==c.StructureKey
                && selected.VariantKey==c.VariantKey && selected.Product==c.Product && selected.FamilyKeys.SequenceEqual(c.FamilyKeys) && selected.SpecializedParameterBindings.SequenceEqual(c.SpecializedParameterBindings)
                && selected.SelectionPolicyReference==c.SelectionPolicyReference && selected.CompositionPolicyReference==c.CompositionPolicyReference,"TS.RESULT.INVALID","Selected intent differs from candidate.");
            var variant=b.CatalogDefinitions.Single(x=>x.Key==selected.VariantKey);
            Require(selected.Side==variant.Side && selected.Bias==variant.Bias && selected.PremiumMode==variant.PremiumMode,"TS.RESULT.INVALID","Selected variant semantics mismatch.");
        }
        else Require(!r.CandidateDecisions.Any(x=>x.Status==SelectionCandidateStatus.EligibleNotSelected),"TS.RESULT.INVALID","NoTrade contains an eligible candidate.");
    }
}
