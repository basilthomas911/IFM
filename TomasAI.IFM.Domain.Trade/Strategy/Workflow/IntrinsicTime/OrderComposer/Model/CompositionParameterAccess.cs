using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition;
namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Model;
public static partial class CompositionParameterResolver
{
    public static decimal Read(CompositionParameters p,CompositionParameter key)=>key switch
    {
        CompositionParameter.LoadingMilliseconds=>p.LoadingMilliseconds,
        CompositionParameter.ExecutionMilliseconds=>p.ExecutionMilliseconds,
        CompositionParameter.CandidateLifetimeMilliseconds=>p.CandidateLifetimeMilliseconds,
        CompositionParameter.MaximumQuoteAgeMilliseconds=>p.MaximumQuoteAgeMilliseconds,
        CompositionParameter.MaximumQuoteSkewMilliseconds=>p.MaximumQuoteSkewMilliseconds,
        CompositionParameter.MinimumDisplayedSize=>p.MinimumDisplayedSize,
        CompositionParameter.ParticipationFraction=>p.ParticipationFraction,
        CompositionParameter.MaximumUnderlyingSpreadTicks=>p.MaximumUnderlyingSpreadTicks,
        CompositionParameter.MaximumLegSpreadTicks=>p.MaximumLegSpreadTicks,
        CompositionParameter.MaximumComboSpreadTicks=>p.MaximumComboSpreadTicks,
        CompositionParameter.TargetDaysToExpiry=>p.TargetDaysToExpiry,
        CompositionParameter.MinimumDaysToExpiry=>p.MinimumDaysToExpiry,
        CompositionParameter.MaximumDaysToExpiry=>p.MaximumDaysToExpiry,
        CompositionParameter.TargetLegDelta=>p.TargetLegDelta,
        CompositionParameter.LegDeltaTolerance=>p.LegDeltaTolerance,
        CompositionParameter.TargetPutDelta=>p.TargetPutDelta,
        CompositionParameter.TargetCallDelta=>p.TargetCallDelta,
        CompositionParameter.TargetNetDelta=>p.TargetNetDelta,
        CompositionParameter.BalanceTolerance=>p.BalanceTolerance,
        CompositionParameter.MinimumCreditToWidth=>p.MinimumCreditToWidth,
        CompositionParameter.MaximumDebitToWidth=>p.MaximumDebitToWidth,
        CompositionParameter.MinimumCreditTicks=>p.MinimumCreditTicks,
        CompositionParameter.MinimumRewardToRisk=>p.MinimumRewardToRisk,
        CompositionParameter.MidpointToNaturalFraction=>p.MidpointToNaturalFraction,
        CompositionParameter.MaximumAdverseMoveTicks=>p.MaximumAdverseMoveTicks,
        CompositionParameter.FeePerContract=>p.FeePerContract,
        CompositionParameter.SlippageTicksPerLeg=>p.SlippageTicksPerLeg,
        CompositionParameter.FuturesPlannedDistance=>p.FuturesPlannedDistance,
        CompositionParameter.FuturesStressDistance=>p.FuturesStressDistance,
        CompositionParameter.FuturesRollHours=>p.FuturesRollHours,
        _=>throw new CompositionException("OC.CONFIG.RULE_INVALID")
    };
    static CompositionParameters Write(CompositionParameters p,CompositionParameter key,decimal value)=>key switch
    {
        CompositionParameter.LoadingMilliseconds=>p with {LoadingMilliseconds=ExactInteger(value)},
        CompositionParameter.ExecutionMilliseconds=>p with {ExecutionMilliseconds=ExactInteger(value)},
        CompositionParameter.CandidateLifetimeMilliseconds=>p with {CandidateLifetimeMilliseconds=ExactInteger(value)},
        CompositionParameter.MaximumQuoteAgeMilliseconds=>p with {MaximumQuoteAgeMilliseconds=ExactInteger(value)},
        CompositionParameter.MaximumQuoteSkewMilliseconds=>p with {MaximumQuoteSkewMilliseconds=ExactInteger(value)},
        CompositionParameter.MinimumDisplayedSize=>p with {MinimumDisplayedSize=value},
        CompositionParameter.ParticipationFraction=>p with {ParticipationFraction=value},
        CompositionParameter.MaximumUnderlyingSpreadTicks=>p with {MaximumUnderlyingSpreadTicks=value},
        CompositionParameter.MaximumLegSpreadTicks=>p with {MaximumLegSpreadTicks=value},
        CompositionParameter.MaximumComboSpreadTicks=>p with {MaximumComboSpreadTicks=value},
        CompositionParameter.TargetDaysToExpiry=>p with {TargetDaysToExpiry=value},
        CompositionParameter.MinimumDaysToExpiry=>p with {MinimumDaysToExpiry=value},
        CompositionParameter.MaximumDaysToExpiry=>p with {MaximumDaysToExpiry=value},
        CompositionParameter.TargetLegDelta=>p with {TargetLegDelta=value},
        CompositionParameter.LegDeltaTolerance=>p with {LegDeltaTolerance=value},
        CompositionParameter.TargetPutDelta=>p with {TargetPutDelta=value},
        CompositionParameter.TargetCallDelta=>p with {TargetCallDelta=value},
        CompositionParameter.TargetNetDelta=>p with {TargetNetDelta=value},
        CompositionParameter.BalanceTolerance=>p with {BalanceTolerance=value},
        CompositionParameter.MinimumCreditToWidth=>p with {MinimumCreditToWidth=value},
        CompositionParameter.MaximumDebitToWidth=>p with {MaximumDebitToWidth=value},
        CompositionParameter.MinimumCreditTicks=>p with {MinimumCreditTicks=value},
        CompositionParameter.MinimumRewardToRisk=>p with {MinimumRewardToRisk=value},
        CompositionParameter.MidpointToNaturalFraction=>p with {MidpointToNaturalFraction=value},
        CompositionParameter.MaximumAdverseMoveTicks=>p with {MaximumAdverseMoveTicks=ExactInteger(value)},
        CompositionParameter.FeePerContract=>p with {FeePerContract=value},
        CompositionParameter.SlippageTicksPerLeg=>p with {SlippageTicksPerLeg=value},
        CompositionParameter.FuturesPlannedDistance=>p with {FuturesPlannedDistance=value},
        CompositionParameter.FuturesStressDistance=>p with {FuturesStressDistance=value},
        CompositionParameter.FuturesRollHours=>p with {FuturesRollHours=value},
        _=>throw new CompositionException("OC.CONFIG.RULE_INVALID")
    };
    static int ExactInteger(decimal value)
    {
        CompositionRulesContract.Require(value == decimal.Truncate(value), "OC.CONFIG.RULE_INVALID");
        return checked((int)value);
    }
}
