using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using MessagePack;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.TradeSelection;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
using static TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection.TradeSelectionContracts;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection;

/// <summary>Deterministic selection over frozen authority and accepted market evidence. Performs no I/O.</summary>
public static class TradeSelectionEvaluator
{
    static readonly JsonSerializerOptions EvidenceJson = new() { Converters = { new JsonStringEnumConverter() } };
    public static TradeSelectionResult Evaluate(ExecuteTradeSelectionPipelineCommand command)
    {
        var (policy, assessmentResult, regimeResult) = ValidateRequest(command);
        var binding = command.SelectionBinding;
        var authority = binding.PortfolioSnapshot;
        var fund = authority.Fund;
        var regime = regimeResult.Decision;
        var assessment = assessmentResult.Assessment;
        Require(regime.IsComplete && regime.Confidence is >= 0 and <= 1 && assessment.AssessmentConfidence is >= 0 and <= 1
            && assessment.ConditionType.HasValue, "TS.UPSTREAM.INVALID", "Complete regime and assessment confidence are required.");
        var condition = assessment.ConditionType.Value;
        var directions = fund.PermittedDirections.Select(NormalizeDirection).ToArray();
        foreach (var permission in fund.PermittedConditions)
            Require(Enum.TryParse<Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment.AssessmentCondition>(permission, false, out var parsed)
                && Enum.IsDefined(parsed) && parsed.ToString() != "Undefined", "TS.CONFIG.PERMISSION", "Unknown Fund condition permission.");
        foreach (var asset in fund.EligibleAssetTypes)
            Require(asset is "Futures" or "FuturesOptions", "TS.CONFIG.PERMISSION", "Unsupported Fund asset permission.");
        List<SelectionRuleEvidence> globals = [];
        Add(globals,"G01","Authority.OperatingState", authority.Portfolio.OperatingState == PortfolioOperatingState.Active && fund.OperatingState == FundOperatingState.Active,
            new { Portfolio=authority.Portfolio.OperatingState, Fund=fund.OperatingState }, "Active", "TS.PERMISSION.OPERATING_STATE");
        Add(globals,"G02","Authority.RiskEnvelope", authority.FinancialPolicy.OperatingState == PortfolioFinancialPolicyState.Active && authority.RiskEnvelope.PermitsNewExposureAt(command.EvaluatedAtUtc),
            authority.RiskEnvelope.CapacityState, "AvailableOrConstrained", "TS.PERMISSION.ENVELOPE");
        Add(globals,"G03","Fund.PermittedConditions", fund.PermittedConditions.Contains(condition.ToString(),StringComparer.Ordinal), condition,fund.PermittedConditions,"TS.PERMISSION.CONDITION");
        var restrictions = regime.Restrictions.Concat(assessment.InheritedRestrictions).Distinct().OrderBy(x => (int)x).ToArray();
        Require(restrictions.All(x => Enum.IsDefined(x)), "TS.UPSTREAM.INVALID", "Unknown numeric regime restriction.");
        Add(globals,"G04","Regime.Restrictions",!restrictions.Intersect(policy.RejectedInheritedRestrictions).Any(),restrictions,policy.RejectedInheritedRestrictions,"TS.REGIME.RESTRICTION");
        Member(globals,"G05","Regime.Direction",regime.Direction,policy.AllowedRegimeDirections,"TS.REGIME.DIRECTION");
        Add(globals,"G06","Regime.Confidence",regime.Confidence>=policy.MinimumRegimeConfidence,regime.Confidence,policy.MinimumRegimeConfidence,"TS.REGIME.CONFIDENCE");
        Member(globals,"G07","Regime.Quality",regime.Quality,policy.AllowedRegimeQualities,"TS.REGIME.QUALITY");
        Member(globals,"G08","Regime.TrendPhase",regime.TrendPhase,policy.AllowedTrendPhases,"TS.REGIME.PHASE");
        Member(globals,"G09","Regime.TrendStrength",regime.TrendStrength,policy.AllowedTrendStrengths,"TS.REGIME.STRENGTH");
        Member(globals,"G10","Regime.VolatilityLevel",regime.VolatilityLevel,policy.AllowedRegimeVolatilityLevels,"TS.REGIME.VOLATILITY_LEVEL");
        Member(globals,"G11","Regime.VolatilityChange",regime.VolatilityChange,policy.AllowedRegimeVolatilityChanges,"TS.REGIME.VOLATILITY_CHANGE");
        Member(globals,"G12","Regime.StructureClassification",regime.StructureClassification,policy.AllowedStructureClassifications,"TS.REGIME.STRUCTURE");
        Add(globals,"G13","Assessment.Confidence",assessment.AssessmentConfidence>=policy.MinimumAssessmentConfidence,assessment.AssessmentConfidence,policy.MinimumAssessmentConfidence,"TS.ASSESSMENT.CONFIDENCE");
        Member(globals,"G14","Assessment.Condition",condition,policy.AllowedAssessmentConditions,"TS.ASSESSMENT.CONDITION");
        Member(globals,"G15","Assessment.Liquidity",assessment.LiquidityCondition,policy.AllowedLiquidity,"TS.ASSESSMENT.LIQUIDITY");
        Member(globals,"G16","Assessment.Session",assessment.SessionState,policy.AllowedSessions,"TS.ASSESSMENT.SESSION");
        Member(globals,"G17","Assessment.EventRisk",assessment.EventRiskState,policy.AllowedEventRisk,"TS.ASSESSMENT.EVENT");
        Member(globals,"G18","Assessment.Stress",assessment.StressState,policy.AllowedStress,"TS.ASSESSMENT.STRESS");
        Member(globals,"G19","Assessment.Volatility",assessment.VolatilityBehavior,policy.AllowedVolatilityBehavior,"TS.ASSESSMENT.VOLATILITY");
        Member(globals,"G20","Assessment.TriggerAlignment",assessment.TriggerAlignment,policy.AllowedTriggerAlignment,"TS.ASSESSMENT.TRIGGER");
        Member(globals,"G21","Assessment.DataQuality",assessment.DataQuality,policy.AllowedAssessmentDataQuality,"TS.ASSESSMENT.DATA_QUALITY");
        var blocker = globals.FirstOrDefault(x=>x.Status==SelectionRuleStatus.Rejected)?.ReasonCode;
        var nodes=binding.CatalogDefinitions.ToDictionary(x=>x.Key);
        List<SelectionCandidateDecision> decisions=[];
        var candidates=binding.Candidates.OrderBy(CandidateIdentity,StringComparer.Ordinal).ToArray();
        foreach(var candidate in candidates)
        {
            var variant=nodes[candidate.VariantKey];
            var structure=nodes[candidate.StructureKey];
            TradeSelectionCatalogCapabilities.ValidateVariant(variant,structure);
            SelectionConstructionPolicy.Read(Policy(binding,candidate.CompositionPolicyReference).PayloadJson).ValidateCandidate(structure,variant);
            var builder=structure.Capabilities.Single(x=>x.Role=="builder");
            var rules=policy.VariantRules;
            var specialized=candidate.SpecializedParameterBindings.Where(x=>x.Role=="TradeSelectionVariants").ToArray();
            Require(specialized.Length<=1,"TS.CONFIG.INVALID","Multiple specialized selector policies.");
            if(specialized.Length==1) rules=TradeSelectionPolicy.ReadSpecialized(nodes[specialized[0].ParameterSet].SettingsJson,policy).Rules;
            var rule=rules.SingleOrDefault(x=>x.BuilderCapabilityCode==builder.Code && x.BuilderCapabilityVersion==builder.Version && x.Side==variant.Side && x.Bias==variant.Bias && x.PremiumMode==variant.PremiumMode)
                ?? throw new TradeSelectionValidationException("TS.CONFIG.CAPABILITY_UNSUPPORTED","No exact variant rule.");
            var comparison=new SelectionComparisonTuple {Priority=candidate.AssignmentPriority,Preference=rule.Preference,Deployment=candidate.DeploymentKey,Strategy=candidate.StrategyKey,Structure=candidate.StructureKey,Variant=candidate.VariantKey,ProductId=candidate.Product.ProductId,AssignmentVersion=candidate.AssignmentVersion};
            if(blocker is not null) {decisions.Add(new(){CandidateHash=candidate.CandidateHash,Status=SelectionCandidateStatus.NotEvaluated,Comparison=comparison,ReasonCodes=[blocker]}); continue;}
            var assignment=authority.Assignments.Single(x=>x.AssignmentVersion==candidate.AssignmentVersion && x.TradeStrategyFamily?.CatalogDeployment==candidate.DeploymentKey);
            List<SelectionRuleEvidence> evidence=[];
            Add(evidence,"C01","Assignment.Deployment",assignment.IsEffectiveAt(command.EvaluatedAtUtc) && fund.PermittedTradeStrategyFamilies.Any(x=>x.CatalogDeployment==candidate.DeploymentKey),candidate.DeploymentKey,fund.PermittedTradeStrategyFamilies.Select(x=>x.CatalogDeployment),"TS.PERMISSION.DEPLOYMENT");
            var asset=builder.Code=="Future"?"Futures":"FuturesOptions";
            Add(evidence,"C02","Candidate.Product",fund.UnderlyingUniverse.Contains(candidate.Product.Symbol,StringComparer.Ordinal) && fund.EligibleAssetTypes.Contains(asset,StringComparer.Ordinal)
                && fund.PermittedTradeFamilies.Contains(assignment.TradeFamily,StringComparer.Ordinal) && assignment.AssetType==asset,
                new {candidate.Product,AssetType=asset,assignment.TradeFamily},new{fund.UnderlyingUniverse,fund.EligibleAssetTypes,fund.PermittedTradeFamilies},"TS.PERMISSION.PRODUCT");
            var bias=variant.Bias=="Balanced"?"Neutral":variant.Bias;
            var accepted=regime.Direction.ToString() switch {"Up"=>"Bullish","Down"=>"Bearish","Neutral"=>"Neutral",_=>"Unknown"};
            Add(evidence,"C03","Candidate.Bias",bias==accepted && directions.Contains(bias,StringComparer.Ordinal),bias,new{Accepted=accepted,Permitted=directions},"TS.PERMISSION.DIRECTION");
            Member(evidence,"C04","Variant.Direction",regime.Direction,rule.AllowedRegimeDirections,"TS.VARIANT.DIRECTION");
            Member(evidence,"C05","Variant.TrendPhase",regime.TrendPhase,rule.AllowedTrendPhases,"TS.VARIANT.PHASE");
            Member(evidence,"C06","Variant.TrendStrength",regime.TrendStrength,rule.AllowedTrendStrengths,"TS.VARIANT.STRENGTH");
            Member(evidence,"C07","Variant.Structure",regime.StructureClassification,rule.AllowedStructureClassifications,"TS.VARIANT.STRUCTURE");
            Member(evidence,"C08","Variant.Condition",condition,rule.AllowedAssessmentConditions,"TS.VARIANT.CONDITION");
            Member(evidence,"C09","Variant.Volatility",assessment.VolatilityBehavior,rule.AllowedVolatilityBehavior,"TS.VARIANT.VOLATILITY");
            var rejected=evidence.Where(x=>x.Status==SelectionRuleStatus.Rejected).Select(x=>x.ReasonCode).ToArray();
            decisions.Add(new(){CandidateHash=candidate.CandidateHash,Comparison=comparison,RuleEvidence=[..evidence],ReasonCodes=rejected,
                Status=rejected.Length==0?SelectionCandidateStatus.EligibleNotSelected:SelectionCandidateStatus.Ineligible});
        }
        var winner=decisions.Where(x=>x.Status==SelectionCandidateStatus.EligibleNotSelected).OrderBy(x=>x.Comparison,ComparisonComparer.Instance).FirstOrDefault();
        SelectionCandidateIntent? intent=null;
        if(winner is not null)
        {
            var c=candidates.Single(x=>x.CandidateHash==winner.CandidateHash); var v=nodes[c.VariantKey];
            intent=new(){CandidateHash=c.CandidateHash,AssignmentVersion=c.AssignmentVersion,DeploymentKey=c.DeploymentKey,StrategyKey=c.StrategyKey,StructureKey=c.StructureKey,VariantKey=c.VariantKey,Product=c.Product,Side=v.Side,Bias=v.Bias,PremiumMode=v.PremiumMode,SelectionPolicyReference=c.SelectionPolicyReference,CompositionPolicyReference=c.CompositionPolicyReference,SpecializedParameterBindings=c.SpecializedParameterBindings,FamilyKeys=c.FamilyKeys};
            decisions=decisions.Select(x=>x.Status!=SelectionCandidateStatus.EligibleNotSelected?x:x with {Status=x==winner?SelectionCandidateStatus.Selected:SelectionCandidateStatus.EligibleNotSelected,ReasonCodes=[x==winner?"TS.SELECTED":"TS.RANK.LOWER_PREFERENCE"]}).ToList();
        }
        var reason=winner is not null?"TS.SELECTED":blocker??(candidates.Length==0?"TS.NO_AUTHORIZED_CANDIDATE":"TS.NO_COMPATIBLE_CANDIDATE");
        var confidence=Math.Round(Math.Min(regime.Confidence,assessment.AssessmentConfidence.Value),6,MidpointRounding.ToEven);
        var result=new TradeSelectionResult
        {
            SchemaVersion=1,ResultId=command.CommandId,InvocationId=command.CommandId,WorkflowId=command.WorkflowId,EntityId=command.WorkflowEntityId,
            InputWorkflowRevision=command.InputWorkflowRevision,TriggerEventId=command.WorkflowView.TriggerEventId,PortfolioId=authority.Portfolio.PortfolioId,FundId=fund.FundId,DecisionHorizon=policy.TargetHorizon,
            Outcome=intent is null?SelectionOutcome.NoTrade:SelectionOutcome.Selected,SelectedCandidate=intent,
            DecisionContext=new(){SchemaVersion=1,RegimeResultEnvelope=command.RegimeResultEnvelope,AssessmentResultEnvelope=command.AssessmentResultEnvelope,SelectionBinding=binding},
            GlobalEvidence=[..globals],CandidateDecisions=[..decisions],SelectionConfidence=confidence,PrimaryReasonCode=reason,EvaluatedAtUtc=command.EvaluatedAtUtc,ProducedAtUtc=command.EvaluatedAtUtc,
            ValidUntilUtc=new[]{command.EvaluatedAtUtc.AddSeconds(policy.ResultLifetimeSeconds),assessment.ValidUntilUtc.Value,binding.ValidUntilUtc,command.WorkflowView.ExpiresAtUtc}.Min(),CommonPolicyReference=binding.CommonPolicy,
            SummaryText=intent is null?FormattableString.Invariant($"{policy.TargetHorizon} {policy.InstrumentRoot}: NoTrade ({reason}); {candidates.Length} candidate(s) evaluated."):
                FormattableString.Invariant($"{policy.TargetHorizon} {policy.InstrumentRoot}: selected {nodes[intent.DeploymentKey].Code}/{nodes[intent.VariantKey].Code} ({intent.Side}, {intent.Bias}, {intent.PremiumMode}); confidence {confidence:F6}.")
        };
        Require(result.SummaryText.Length<=2048 && MessagePackSerializer.Serialize(result).Length<=policy.MaximumResultPayloadBytes,"TS.CONTRACT.PAYLOAD_SIZE","Complete selector result exceeds its limit.");
        return result;
    }
    public static string NormalizeDirection(string value)=>value switch {"Up" or "Long" or "Bullish"=>"Bullish","Down" or "Short" or "Bearish"=>"Bearish","Neutral"=>"Neutral",_=>throw new TradeSelectionValidationException("TS.CONFIG.PERMISSION","Unknown Fund direction permission.")};
    static void Member<T>(List<SelectionRuleEvidence> rows,string id,string path,T actual,T[] allowed,string reason) where T:struct,Enum
    {
        Require(Enum.IsDefined(actual),"TS.UPSTREAM.INVALID","Unknown numeric value for "+path);
        var unknown=actual.ToString() is "Unknown" or "Undefined";
        Add(rows,id,path,!unknown && allowed.Contains(actual),actual,allowed,unknown?"TS.EVIDENCE.UNKNOWN":reason);
    }
    static void Add<TActual,TExpected>(List<SelectionRuleEvidence> rows,string id,string path,bool passed,TActual actual,TExpected expected,string reason)
    {
        var row=new SelectionRuleEvidence {RuleId=id,FieldPath=path,Status=passed?SelectionRuleStatus.Passed:SelectionRuleStatus.Rejected,ActualJson=JsonSerializer.Serialize(actual,EvidenceJson),ExpectedJson=JsonSerializer.Serialize(expected,EvidenceJson),ReasonCode=passed?string.Empty:reason};
        Require(System.Text.Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(row))<=4096,"TS.CONTRACT.PAYLOAD_SIZE","Evidence row exceeds its limit."); rows.Add(row);
    }
    public sealed class ComparisonComparer:IComparer<SelectionComparisonTuple>
    {
        public static readonly ComparisonComparer Instance=new();
        public int Compare(SelectionComparisonTuple? a,SelectionComparisonTuple? b)
        {
            ArgumentNullException.ThrowIfNull(a); ArgumentNullException.ThrowIfNull(b);
            var n=a.Priority.CompareTo(b.Priority); if(n!=0)return n; n=a.Preference.CompareTo(b.Preference);if(n!=0)return n;
            foreach(var pair in new[]{(a.Deployment,b.Deployment),(a.Strategy,b.Strategy),(a.Structure,b.Structure),(a.Variant,b.Variant)})
            {n=StringComparer.Ordinal.Compare(pair.Item1.Id.ToString("D"),pair.Item2.Id.ToString("D"));if(n!=0)return n;n=pair.Item1.Version.CompareTo(pair.Item2.Version);if(n!=0)return n;}
            n=a.ProductId.CompareTo(b.ProductId);return n!=0?n:a.AssignmentVersion.CompareTo(b.AssignmentVersion);
        }
    }
}
