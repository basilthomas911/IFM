using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position.IronCondor.ForwardPrice;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Model;

public sealed partial class IronCondorTradePlanSnapshotCalculator
{
    /// <summary>Builds current observations and consumes the exact Function price; no independent forward scenario or daily-PnL gate.</summary>
    public IronCondorTradePlanSnapshot CreateFromForwardPrice(StrategyTradePlanSnapshot plan, UpdateIronCondorTradePlanCommand command)
    {
        var position = plan.Position;
        var references = command.IronCondorTradePlanInputs;
        var daily = references?.DailyRiskInputs ?? CaptureUnavailableInputs(position, references);
        var observations = position.Legs.Select(leg => {
            var retained = StrategyPositionLegOptionData.Observe(leg);
            var captured = daily.Legs.FirstOrDefault(candidate => candidate.TradeLegId == leg.TradeLegId && candidate.ContractId == leg.ContractId);
            return retained with { TheoreticalPrice = retained.TheoreticalPrice ?? captured?.TheoreticalPrice,
                Delta = retained.Delta ?? captured?.Delta, Gamma = retained.Gamma ?? captured?.Gamma,
                Vega = retained.Vega ?? captured?.Vega, Theta = retained.Theta ?? captured?.Theta };
        }).ToArray();
        var quantity = position.Legs.Length == 4 && position.Legs.All(l => l.SignedQuantity is not (0 or int.MinValue))
            && position.Legs.Select(l => Math.Abs(l.SignedQuantity)).Distinct().Count() == 1 ? Math.Abs(position.Legs[0].SignedQuantity) : (int?)null;
        // Opening prices are the position's executed entry basis, not current theoretical or daily-reference marks.
        var entryCreditPoints = quantity is { } entryQuantity && position.Legs.All(leg => leg.OpeningPrice > 0)
            ? -position.Legs.Sum(leg => leg.OpeningPrice * leg.SignedQuantity) / entryQuantity : (decimal?)null;
        var snapshot = CreateObservationSnapshot(plan, command.SourceEventId, references?.TradeDate, references?.MaturityDate,
            daily, observations, quantity, observations.Length == 0 ? null : observations.Min(l => l.QuoteAtUtc));
        var reasons = new List<string>();
        IronCondorForwardRisk? risk = null;
        var completed = command.IronCondorForwardPriceCalculated;
        if (completed is null)
            reasons.Add(command.ForwardPriceFailure ?? "IronCondorForwardPrice.RESULT.UNAVAILABLE");
        else
        {
            var price = completed.IronCondorForwardPrice;
            if (completed.EntityId != command.EntityId || completed.SourceEventId != command.SourceEventId
                || price.SourceEventId != command.SourceEventId || price.PositionSession != command.EntityId
                || price.PositionSequence != position.PositionSequence || price.RouteGeneration != position.RouteGeneration
                || price.StrategyQuantity != quantity || completed.CommandId == Guid.Empty || string.IsNullOrEmpty(price.InputFingerprint)
                || completed.RequestFingerprint != price.InputFingerprint
                || daily.ContractMultiplier is { } multiplier && multiplier != price.ContractMultiplier
                || daily.Currency is { Length: > 0 } currency && currency != price.Currency)
                reasons.Add("IronCondorForwardPrice.RESULT.MISMATCH: exact position/session/source and actual contract currency required.");
            else if (references?.TradeLimits is not { MaxLoss: < 0 } limits || limits.TradeId != position.Id.Trade.TradeId)
                reasons.Add("IronCondorForwardPrice.MAX_LOSS.UNAVAILABLE: initialized trade maximum loss in contract currency required; price-point MaxLossLimit is not a currency amount.");
            else if (entryCreditPoints is not > 0)
                reasons.Add("IronCondorForwardPrice.ENTRY_CREDIT.UNAVAILABLE: four opening leg prices and a positive short-condor entry credit are required.");
            else
                risk = IronCondorForwardPriceCalculation.CalculateRisk(completed, entryCreditPoints.Value, -limits.MaxLoss);
        }
        var multiplierForCurrent = risk?.IronCondorForwardPrice.ContractMultiplier ?? daily.ContractMultiplier;
        var dailyPnl = multiplierForCurrent is > 0 ? position.DailyPnl * multiplierForCurrent - daily.ActualDailyCommission : null;
        var currentValue = position.Legs.Sum(l => l.CurrentPrice * l.SignedQuantity) * multiplierForCurrent;
        var q = quantity ?? 1;
        decimal Spread(byte right) => position.Legs.Where(l => l.PutCall == right).Sum(l => l.CurrentPrice * l.SignedQuantity) / q;
        return snapshot with
        {
            CalculationVersion = IronCondorForwardRisk.EntryCreditCalculationVersion, IronCondorForwardRisk = risk,
            CalculationStatus = reasons.Count == 0 ? "Complete" : "Unavailable", UnavailableReasons = reasons.ToArray(),
            ForwardPriceCalculationError = reasons.Count == 0 ? null : $"Forward price calculation error: {string.Join("; ", reasons)}",
            VolatilityPrice = references?.VolatilityPrice, TradeType = references?.TradeType.ToString(),
            DailyPnl = dailyPnl, DailyLoss = dailyPnl is { } pnl ? Math.Max(0, -pnl) : null,
            CurrentPositionValue = currentValue, TradePnl = (position.UnrealizedPnl + position.RealizedPnl) * multiplierForCurrent,
            PutSpreadPrice = quantity.HasValue ? Spread(2) : null, CallSpreadPrice = quantity.HasValue ? Spread(1) : null,
            CombinedSpreadPrice = quantity.HasValue ? Spread(2) + Spread(1) : null,
            Currency = risk?.IronCondorForwardPrice.Currency ?? daily.Currency, ContractMultiplier = multiplierForCurrent,
            ForwardPrice = risk?.IronCondorForwardPrice.ForwardPrice ?? 0m, CombinedForwardPrice = risk?.IronCondorForwardPrice.ForwardPrice ?? 0m,
            ForwardLossRatio = risk?.ForwardLossRatio,
            ForwardDelta = CalculateForwardDelta(observations),
            NetDelta = quantity is { } deltaQuantity ? CalculateExposure(observations, deltaQuantity, leg => leg.Delta) : null,
            NetGamma = quantity is { } gammaQuantity ? CalculateExposure(observations, gammaQuantity, leg => leg.Gamma) : null,
            NetVega = quantity is { } vegaQuantity ? CalculateExposure(observations, vegaQuantity, leg => leg.Vega) : null,
            NetTheta = quantity is { } thetaQuantity ? CalculateExposure(observations, thetaQuantity, leg => leg.Theta) : null,
            // Do not repurpose historical nonnegative loss or forward-daily-PnL members.
            ForwardLoss = null, ForwardDailyPnl = null, ForwardLossHeadroom = null,
            ForwardPositionValue = null,
            PutForwardPrice = risk is null ? null : risk.IronCondorForwardPrice.PutForwardPricePoints * risk.IronCondorForwardPrice.ContractMultiplier * risk.IronCondorForwardPrice.StrategyQuantity,
            CallForwardPrice = risk is null ? null : risk.IronCondorForwardPrice.CallForwardPricePoints * risk.IronCondorForwardPrice.ContractMultiplier * risk.IronCondorForwardPrice.StrategyQuantity,
            ExitRecommended = risk?.ExitRecommended, ExitReason = risk is null ? $"Forward price calculation error: {string.Join("; ", reasons)}"
                : risk.ExitRecommended ? "Projected forward loss reaches the configured trade maximum loss." : "Projected forward loss is below the configured trade maximum loss.",
            ActionType = risk?.ExitRecommended == true ? "Exit" : "Monitor",
            ActionState = risk is null ? "Unavailable" : risk.ExitRecommended ? "ExitRequired" : "Normal"
        };
    }

    /// <summary>Calculates per-strategy-unit forward delta from the current priced leg deltas.
    /// Each vertical contributes its signed long and short deltas; no loss amplification,
    /// adverse scenario, contract multiplier or position quantity scaling is applied.</summary>
    public static double? CalculateForwardDelta(IronCondorTradePlanLegObservation[] legs)
    {
        ArgumentNullException.ThrowIfNull(legs);
        if (legs.Length != 4 || legs.Any(leg => leg.SignedQuantity is null or 0 or int.MinValue
                || leg.Delta is not { } delta || !double.IsFinite(delta) || Math.Abs(delta) > 1)
            || legs.Select(leg => Math.Abs(leg.SignedQuantity!.Value)).Distinct().Count() != 1)
            return null;
        var put = legs.Where(leg => leg.PutCall == 2).ToArray();
        var call = legs.Where(leg => leg.PutCall == 1).ToArray();
        if (put.Length != 2 || call.Length != 2
            || !put.Any(leg => leg.SignedQuantity > 0) || !put.Any(leg => leg.SignedQuantity < 0)
            || !call.Any(leg => leg.SignedQuantity > 0) || !call.Any(leg => leg.SignedQuantity < 0))
            return null;
        var quantity = Math.Abs(legs[0].SignedQuantity!.Value);
        double VerticalDelta(IronCondorTradePlanLegObservation[] spread)
            => spread.Sum(leg => leg.Delta!.Value * leg.SignedQuantity!.Value) / quantity;
        return VerticalDelta(put) + VerticalDelta(call);
    }
}
