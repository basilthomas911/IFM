using System.Collections.Immutable;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Model;

public interface IRiskEvaluator
{
    RiskAssessmentResult Calculate(ExecuteRiskManagementPipelineCommand command, CancellationToken token = default);
}

/// <summary>Evaluates one immutable upstream chain, preserving catalog selection and proposed order identities.</summary>
public sealed class RiskEvaluator : IRiskEvaluator
{
    public RiskAssessmentResult Calculate(ExecuteRiskManagementPipelineCommand c, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        var composition = OrderCompositionContracts.ReadResult(c.CompositionResult);
        var candidate = composition.Candidate ?? throw new RiskCalculationException("RM.INPUT.NO_CANDIDATE");
        var assessment = c.MarketConditionResult.AssessmentResult!.Assessment;
        var regime = c.RegimeResult.RegimeResult!.Decision;
        RiskUnitModel.Require(regime.IsComplete && assessment.Availability == AssessmentAvailability.Available
            && assessment.ConditionType is not null and not AssessmentCondition.Undefined and not AssessmentCondition.Unclassified
            && assessment.SessionState != MarketSessionStatus.Unknown && assessment.LiquidityCondition != AssessmentLiquidity.Unknown
            && assessment.StressState != AssessmentStress.Unknown && assessment.VolatilityBehavior != AssessmentVolatility.Unknown
            && assessment.EventRiskState != AssessmentEventContext.Unknown
            && Enum.IsDefined(assessment.ConditionType.Value) && Enum.IsDefined(assessment.SessionState)
            && Enum.IsDefined(assessment.LiquidityCondition) && Enum.IsDefined(assessment.StressState)
            && Enum.IsDefined(assessment.VolatilityBehavior) && Enum.IsDefined(assessment.EventRiskState)
            && assessment.InheritedRestrictions.All(Enum.IsDefined) && regime.Restrictions.All(Enum.IsDefined), "RM.INPUT.UNKNOWN_MARKET_STATE");
        var result = new RiskAssessmentResult
        {
            ResultId=c.CommandId, InvocationId=c.CommandId, WorkflowId=c.WorkflowId, EntityId=c.WorkflowEntityId,
            InputWorkflowRevision=c.InputWorkflowRevision, CompositionResultId=composition.ResultId,
            CompositionResultHash=c.CompositionResult.PayloadSha256, UnitCandidateHash=candidate.CandidateHash, OrderId=candidate.OrderId,
            EvaluatedAtUtc=c.EvaluatedAtUtc, ProducedAtUtc=c.EvaluatedAtUtc, ValidUntilUtc=c.ExpiresAtUtc,
            Authority=c.Authority, Environment=c.SizingAuthority.Environment, PortfolioId=candidate.PortfolioId, FundId=candidate.FundId,
            TargetHorizon=candidate.TargetHorizon, InputHash=c.InputSha256, PolicyHash=c.PolicyHash, Outcome=RiskAssessmentOutcome.Rejected
        };
        if (assessment.SessionState == MarketSessionStatus.Closed || assessment.LiquidityCondition == AssessmentLiquidity.Poor
            || assessment.ConditionType == AssessmentCondition.Dislocated || assessment.VolatilityBehavior == AssessmentVolatility.Shock
            || assessment.InheritedRestrictions.Contains(RegimeRestriction.NoNewTrade) || regime.Restrictions.Contains(RegimeRestriction.NoNewTrade))
            return result with { Reasons=["RM.MARKET.NEW_ENTRY_BLOCKED"] };
        var legs = RiskUnitModel.ReadLegs(candidate, c.MarketSnapshot, c.EvaluatedAtUtc, c.SizingAuthority.Environment);
        RiskUnitModel.Require(legs.All(x => x.UnderlyingId == candidate.Legs[0].UnderlyingInstrumentId) &&
            c.SizingAuthority.UnderlyingId==FinancialScopeKeys.Underlying(candidate.Product.Symbol,candidate.Product.Exchange,candidate.Product.Currency), "RM.INPUT.UNDERLYING");
        var unit = RiskUnitModel.Calculate(legs, candidate.Pricing.WorstDebit, candidate.Pricing.CostReserve,
            c.IncrementalLossReserve, candidate.RiskEvidence.PlannedLoss, candidate.RiskEvidence.StressLoss, token);
        decimal includedFees=composition.ResolvedParameters.Values.FeePerContract*unit.GrossContracts;
        RiskUnitModel.Require(includedFees>=0 && includedFees<=candidate.Pricing.CostReserve,"RM.INPUT.FEE_RESERVE");
        unit=unit with { ComposerFeeReserve=includedFees };
        decimal multiplier = assessment.LiquidityCondition == AssessmentLiquidity.Degraded || assessment.StressState == AssessmentStress.Elevated
            || assessment.EventRiskState == AssessmentEventContext.Elevated || assessment.InheritedRestrictions.Any(x => x != RegimeRestriction.None)
            || regime.Restrictions.Any(x => x != RegimeRestriction.None) ? .5m : 1m;
        var sizing = RiskSizingModel.Calculate(unit, c.Policy, c.SizingAuthority, candidate.LiquidityCapacityUnits, c.Funding, multiplier, token);
        result = result with { UnitRisk=unit, StrategyUnits=sizing.StrategyUnits, Requirements=sizing.Requirements,
            MarginEvidence=sizing.MarginEvidence, Reasons=sizing.Reasons };
        if (sizing.StrategyUnits == 0) return result;
        var sized = candidate.Legs.Select(x => new RiskSizedLeg(x.InstrumentId,x.Side,
            checked(x.Ratio*sizing.StrategyUnits),candidate.PrimaryTradeId)).ToImmutableArray();
        string sizedHash = RiskContracts.Hash(new { candidate.OrderId, candidate.CandidateHash, Units=sizing.StrategyUnits,
            Legs=sized, candidate.ExecutionEnvelope, RequirementsHash=sizing.Requirements!.ContentHash, c.ExpiresAtUtc });
        return result with { Outcome=RiskAssessmentOutcome.Approved, SizedOrderHash=sizedHash, Legs=sized, Reasons=["RM.CAPACITY.ELIGIBLE"] };
    }
}
