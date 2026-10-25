using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Application.OptionPricing;
using TomasAI.IFM.Shared.OptionPricing;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Model;

public sealed partial class IronCondorTradePlanSnapshotCalculator
{
    /// <summary>Calculates from the four retained position legs. No scope capture, cross-leg timestamp equality or provider read.</summary>
    static IronCondorDailyRiskInputs CaptureRetainedInputs(StrategyPositionSnapshot position, IronCondorDailyRiskInputs previous, DateTime at)
    {
        var policy = previous.StrategyRiskParameterSet?.IronCondor;
        var observations = position.Legs.Select(StrategyPositionLegOptionData.Observe).ToArray();
        var metadata = position.Legs.Select(leg => (leg.OptionTick ?? leg.OptionRisk)?.PricingInputs).ToArray();
        var currencies = metadata.Where(item => item is not null).Select(item => item!.Currency).Distinct(StringComparer.Ordinal).ToArray();
        var multiplier = StrategyPositionLegOptionData.CommonContractMultiplier(position);
        var newest = position.Legs.Where(leg => leg.OptionRisk is not null).OrderByDescending(leg => leg.OptionRisk!.CalculatedAtUtc).FirstOrDefault()?.OptionRisk;
        decimal? underlying = newest is { UnderlyingPrice: > 0 } ? (decimal)newest.UnderlyingPrice : null;
        var result = previous with
        {
            Legs = observations, ValueDate = position.ValueDate, PositionSequence = position.PositionSequence,
            RouteGeneration = position.RouteGeneration, ContractMultiplier = multiplier,
            Currency = currencies.Length == 1 && metadata.All(item => item is not null) ? currencies[0] : null,
            DailyPnl = multiplier is > 0 ? position.DailyPnl * multiplier - previous.ActualDailyCommission : null,
            DailyLossLimit = policy?.DailyLossLimit, UnderlyingPrice = underlying,
            ScenarioHorizonSeconds = policy?.ScenarioHorizonSeconds, ScenarioVolatilityShift = policy?.ScenarioVolatilityShiftPercentagePoints,
            EstimatedExitCommission = policy is null ? null : position.Legs.Sum(leg => Math.Abs(leg.SignedQuantity)) * policy.ExitCommissionPerContract,
            RiskValidUntilUtc = position.Legs.All(leg => leg.OptionRisk is not null) ? position.Legs.Min(leg => leg.OptionRisk!.ValidUntilUtc.UtcDateTime) : null,
            TimeToExpiryDays = metadata.All(item => item is not null) ? (metadata.Max(item => item!.ExpirationUtc).UtcDateTime - at).TotalDays : null,
            PutOTMProbability = Probability(position.Legs.FirstOrDefault(leg => leg.PutCall == 2 && leg.SignedQuantity < 0), at),
            CallOTMProbability = Probability(position.Legs.FirstOrDefault(leg => leg.PutCall == 1 && leg.SignedQuantity < 0), at),
            // Never reuse a scenario belonging to a different set of current observations.
            ScenarioUnderlyingPrice = null, ScenarioKind = null, PricingModelVersion = null, EstimatedExitSlippage = null,
            UnavailableReasons = position.Legs.Where(leg => leg.OptionRisk is null)
                .Select(leg => $"{leg.ContractId}: no valid risk calculation retained ({leg.OptionTick?.PricingFailure ?? "not received"}).").ToArray()
        };
        if (policy is not null && multiplier is > 0 && metadata.All(item => item is not null))
        {
            var close = CalculateCloseValue(observations, multiplier);
            var tickAllowance = position.Legs.Select((leg, index) => (leg.SignedQuantity > 0
                ? metadata[index]!.BidTickIncrement : metadata[index]!.AskTickIncrement) * Math.Abs(leg.SignedQuantity)).ToArray();
            if (close.HasValue && tickAllowance.All(tick => tick > 0))
                result = result with { EstimatedExitSlippage = Math.Max(0,
                    position.Legs.Sum(leg => leg.CurrentPrice * leg.SignedQuantity) * multiplier.Value - close.Value)
                    + tickAllowance.Sum() * policy.AdditionalExitSlippageTicksPerLeg * multiplier.Value };
        }
        if (policy is null || underlying is null || multiplier is not > 0 || position.Legs.Length != 4
            || position.Legs.Any(leg => leg.OptionRisk is null) || metadata.Any(item => item is null)) return result;
        var calculator = new OptionCalculator();
        var sets = new List<(decimal Future, IronCondorTradePlanLegObservation[] Legs)>();
        foreach (var direction in new[] { -1, 1 })
        {
            var future = underlying.Value + direction * policy.AdverseFuturesMovePoints;
            if (future <= 0) return result with { UnavailableReasons = ["Scenario underlying must be positive."] };
            var projected = new IronCondorTradePlanLegObservation[observations.Length];
            for (var i = 0; i < observations.Length; i++)
            {
                var leg = position.Legs[i]; var data = leg.OptionRisk!; var context = metadata[i]!;
                var years = (context.ExpirationUtc.UtcDateTime - at.AddSeconds(policy.ScenarioHorizonSeconds)).TotalDays / context.DaysPerYear;
                if (context.DaysPerYear is not (360 or 365) || years <= 0 || !double.IsFinite(context.AnnualContinuousRate)
                    || !Enum.TryParse<ExerciseKind>(context.Exercise, out var exercise)
                    || !Enum.TryParse<PremiumKind>(context.Premium, out var premium) || leg.Strike is not > 0)
                    return result with { UnavailableReasons = [$"{leg.ContractId}: incomplete pricing conventions or scenario reaches expiry."] };
                var priced = calculator.PriceAndDelta(new(UnderlyingKind.Futures, exercise, premium,
                    leg.PutCall == 1 ? OptionSide.Call : OptionSide.Put, (double)future, (double)leg.Strike.Value,
                    years, context.AnnualContinuousRate), data.ImpliedVolatility + policy.ScenarioVolatilityShiftPercentagePoints / 100);
                if (!priced.Success) return result with { UnavailableReasons = [$"{leg.ContractId}: scenario calculator failed ({priced.Failure})."] };
                projected[i] = observations[i] with { ForwardPrice = (decimal)priced.Value!.Value.Price, ForwardDelta = priced.Value.Value.Delta };
            }
            sets.Add((future, projected));
        }
        var selected = sets.MinBy(set => set.Legs.Sum(leg => leg.ForwardPrice!.Value * leg.SignedQuantity!.Value));
        return result with
        {
            Legs = selected.Legs, ScenarioUnderlyingPrice = selected.Future,
            ScenarioKind = selected.Future < underlying ? "AdverseFuturesDown" : "AdverseFuturesUp",
            PricingModelVersion = string.Join(";", metadata.Select(item => item!.ModelVersion).Distinct(StringComparer.Ordinal))
        };
    }

    static double? Probability(StrategyPositionLeg? leg, DateTime at)
    {
        if (leg?.OptionRisk is not { } risk || risk.PricingInputs is not { } context || leg.Strike is not > 0) return null;
        var years = (context.ExpirationUtc.UtcDateTime - at).TotalDays / context.DaysPerYear;
        if (years <= 0 || context.DaysPerYear is not (360 or 365)) return null;
        var z = Math.Log((double)leg.Strike.Value / risk.UnderlyingPrice) / (risk.ImpliedVolatility * Math.Sqrt(years));
        var probability = MathNet.Numerics.ExcelFunctions.NormSDist(z);
        return leg.PutCall == 1 ? probability : 1 - probability;
    }
}
