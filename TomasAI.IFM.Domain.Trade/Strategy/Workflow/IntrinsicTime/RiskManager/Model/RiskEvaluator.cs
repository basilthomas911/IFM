using System.Collections.Immutable;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Model;
/// <summary>Evaluates one immutable upstream chain, preserving catalog selection and proposed order identities.</summary>
public sealed class RiskEvaluator : IRiskEvaluator
{
    public RiskAssessmentResult Calculate(ExecuteRiskManagementPipelineCommand command, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        token.ThrowIfCancellationRequested();
        var composition = OrderCompositionContracts.ReadResult(command.CompositionResult);
        var candidate = composition.Candidate ?? throw new RiskCalculationException("RM.INPUT.NO_CANDIDATE");
        var assessment = command.MarketConditionResult.AssessmentResult!.Assessment;
        var regime = command.RegimeResult.RegimeResult!.Decision;
        RiskUnitModel.Require(regime.IsComplete && assessment.Availability == AssessmentAvailability.Available && assessment.ConditionType is not null and not AssessmentCondition.Undefined and not AssessmentCondition.Unclassified && assessment.SessionState != MarketSessionStatus.Unknown && assessment.LiquidityCondition != AssessmentLiquidity.Unknown && assessment.StressState != AssessmentStress.Unknown && assessment.VolatilityBehavior != AssessmentVolatility.Unknown && assessment.EventRiskState != AssessmentEventContext.Unknown && Enum.IsDefined(assessment.ConditionType.Value) && Enum.IsDefined(assessment.SessionState) && Enum.IsDefined(assessment.LiquidityCondition) && Enum.IsDefined(assessment.StressState) && Enum.IsDefined(assessment.VolatilityBehavior) && Enum.IsDefined(assessment.EventRiskState) && assessment.InheritedRestrictions.All(Enum.IsDefined) && regime.Restrictions.All(Enum.IsDefined), "RM.INPUT.UNKNOWN_MARKET_STATE");
        var result = CreateBaseResult(command, composition, candidate);
        if (assessment.SessionState == MarketSessionStatus.Closed || assessment.LiquidityCondition == AssessmentLiquidity.Poor || assessment.ConditionType == AssessmentCondition.Dislocated || assessment.VolatilityBehavior == AssessmentVolatility.Shock || assessment.InheritedRestrictions.Contains(RegimeRestriction.NoNewTrade) || regime.Restrictions.Contains(RegimeRestriction.NoNewTrade))
            return result with
            {
                Reasons = ["RM.MARKET.NEW_ENTRY_BLOCKED"]
            };
        var legs = RiskUnitModel.ReadLegs(candidate, command.MarketSnapshot, command.EvaluatedAtUtc, command.SizingAuthority.Environment);
        RiskUnitModel.Require(legs.All(x => string.Equals(x.UnderlyingId, candidate.Legs[0].UnderlyingInstrumentId, StringComparison.Ordinal)) && string.Equals(command.SizingAuthority.UnderlyingId, FinancialScopeKeys.Underlying(candidate.Product.Symbol, candidate.Product.Exchange, candidate.Product.Currency), StringComparison.Ordinal), "RM.INPUT.UNDERLYING");
        var unit = RiskUnitModel.Calculate(legs, candidate.Pricing.WorstDebit, candidate.Pricing.CostReserve, command.IncrementalLossReserve, candidate.RiskEvidence.PlannedLoss, candidate.RiskEvidence.StressLoss, token);
        decimal includedFees = composition.ResolvedParameters.Values.FeePerContract * unit.GrossContracts;
        RiskUnitModel.Require(includedFees >= 0 && includedFees <= candidate.Pricing.CostReserve, "RM.INPUT.FEE_RESERVE");
        unit = unit with
        {
            ComposerFeeReserve = includedFees
        };
        decimal multiplier = assessment.LiquidityCondition == AssessmentLiquidity.Degraded || assessment.StressState == AssessmentStress.Elevated || assessment.EventRiskState == AssessmentEventContext.Elevated || assessment.InheritedRestrictions.Any(x => x != RegimeRestriction.None) || regime.Restrictions.Any(x => x != RegimeRestriction.None) ? .5m : 1m;
        var sizing = RiskSizingModel.Calculate(unit, command.Policy, command.SizingAuthority, candidate.LiquidityCapacityUnits, command.Funding, multiplier, token);
        result = result with
        {
            UnitRisk = unit,
            StrategyUnits = sizing.StrategyUnits,
            Requirements = sizing.Requirements,
            MarginEvidence = sizing.MarginEvidence,
            Reasons = sizing.Reasons
        };
        if (sizing.StrategyUnits == 0)
            return result;
        var sized = candidate.Legs.Select(x => new RiskSizedLeg(x.InstrumentId, x.Side, checked(x.Ratio * sizing.StrategyUnits), candidate.PrimaryTradeId)).ToImmutableArray();
        string sizedHash = RiskContracts.Hash(new { candidate.OrderId, candidate.CandidateHash, Units = sizing.StrategyUnits, Legs = sized, candidate.ExecutionEnvelope, RequirementsHash = sizing.Requirements!.ContentHash, command.ExpiresAtUtc });
        return result with
        {
            Outcome = RiskAssessmentOutcome.Approved,
            SizedOrderHash = sizedHash,
            Legs = sized,
            Reasons = ["RM.CAPACITY.ELIGIBLE"]
        };
    }

    /// <summary>Copies accepted upstream identities, authority and volatility evidence into the initially rejected risk result.</summary>
    static RiskAssessmentResult CreateBaseResult(ExecuteRiskManagementPipelineCommand command,
        OrderCompositionResult composition, CompositionCandidate candidate)
    {
        return new RiskAssessmentResult
        {
            ResultId = command.CommandId,
            InvocationId = command.CommandId,
            WorkflowId = command.WorkflowId,
            EntityId = command.WorkflowEntityId,
            InputWorkflowRevision = command.InputWorkflowRevision,
            CompositionResultId = composition.ResultId,
            CompositionResultHash = command.CompositionResult.PayloadSha256,
            UnitCandidateHash = candidate.CandidateHash,
            OrderId = candidate.OrderId,
            EvaluatedAtUtc = command.EvaluatedAtUtc,
            ProducedAtUtc = command.EvaluatedAtUtc,
            ValidUntilUtc = command.ExpiresAtUtc,
            Authority = command.Authority,
            Environment = command.SizingAuthority.Environment,
            PortfolioId = candidate.PortfolioId,
            FundId = candidate.FundId,
            TargetHorizon = candidate.TargetHorizon,
            InputHash = command.InputSha256,
            PolicyHash = command.PolicyHash,
            Outcome = RiskAssessmentOutcome.Rejected,
            VolatilityEvidence = composition.DecisionContext.VolatilityEvidence
        };
    }
}
