using System.Collections.Immutable;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Model;

public static class RiskExplanationModel
{
    public static RiskExplanation Create(ExecuteRiskManagementPipelineCommand input, RiskAssessmentResult result,
        CancellationToken token = default)
    {
        RiskUnitModel.Require(result.InputHash == input.InputSha256 && input.InputSha256 == input.Fingerprint(), "RM.EXPLANATION.INPUT");
        var market = input.MarketConditionResult.AssessmentResult!.Assessment;
        var regime = input.RegimeResult.RegimeResult!.Decision;
        decimal multiplier = market.LiquidityCondition == AssessmentLiquidity.Degraded || market.StressState == AssessmentStress.Elevated
            || market.EventRiskState == AssessmentEventContext.Elevated || market.InheritedRestrictions.Any(x => x != RegimeRestriction.None)
            || regime.Restrictions.Any(x => x != RegimeRestriction.None) ? .5m : 1m;
        var candidate = input.CompositionResult.ReadCompositionResult().Candidate!;
        var latency = RiskLatency.Measure(input);
        var quantities = ImmutableArray.CreateBuilder<RiskQuantityCheck>();
        if (result.UnitRisk is not null)
        {
            var sized = RiskSizingModel.Calculate(result.UnitRisk, input.Policy, input.SizingAuthority,
                candidate.LiquidityCapacityUnits, input.Funding, multiplier, token, quantities.Add);
            RiskUnitModel.Require(sized.StrategyUnits == result.StrategyUnits &&
                sized.Requirements?.ContentHash == result.Requirements?.ContentHash, "RM.EXPLANATION.RESULT");
        }
        var explanation = new RiskExplanation
        {
            ResultHash = RiskContracts.Hash(result), MaximumUnits = Math.Min(input.Policy.MaximumUnits, Math.Max(0, candidate.LiquidityCapacityUnits)),
            AvailableCash = input.SizingAuthority.AvailableCash,
            EffectiveLossBudget = Math.Min(input.SizingAuthority.PerTradeLossBudget, input.SizingAuthority.RiskCapital * input.Policy.PerTradeRiskFraction) * multiplier,
            MarketMultiplier = multiplier, Limits = input.SizingAuthority.Limits, Quantities = quantities.ToImmutable(),
            Conditions = [.. result.Reasons,
                FormattableString.Invariant($"Candidate age: {latency.CandidateAgeMilliseconds:F1} ms"),
                latency.OldestQuoteAgeMilliseconds is { } quoteAge ? FormattableString.Invariant($"Oldest quote age: {quoteAge:F1} ms") : "Oldest quote age: unavailable",
                latency.AgeLimitEnforced ? "Latency policy: production age limit" : "Latency policy: observation only", $"Session: {market.SessionState}", $"Liquidity: {market.LiquidityCondition}",
                $"Stress: {market.StressState}", $"Event risk: {market.EventRiskState}", $"Volatility: {market.VolatilityBehavior}",
                .. market.InheritedRestrictions.Select(x => $"Assessment restriction: {x}"), .. regime.Restrictions.Select(x => $"Regime restriction: {x}")]
        };
        RiskUnitModel.Require(TomasAI.IFM.Framework.Serialization.MessagePackBinarySerializer.MeasureContent(explanation) <= 524288,
            "RM.EXPLANATION.SIZE");
        return explanation with { ContentHash = explanation.Hash() };
    }
}
