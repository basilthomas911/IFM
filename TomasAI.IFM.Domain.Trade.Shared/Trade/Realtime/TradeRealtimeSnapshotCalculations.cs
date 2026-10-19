using System.Collections.Immutable;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;

namespace TomasAI.IFM.Domain.Trade.Shared.Trade.Realtime;

/// <summary>Allocation-conscious immutable display calculations. Financial provenance rules remain unchanged.</summary>
internal static class TradeRealtimeSnapshotCalculations
{
    internal static int FindLegIndex(ImmutableArray<TradeLegRealtimeSnapshot> legs, string contractId)
    {
        for (var i = 0; i < legs.Length; i++)
            if (legs[i].Definition.ContractId == contractId) return i;
        return -1;
    }

    internal static TradeRealtimeSnapshot Qualify(TradeRealtimeSnapshot snapshot, DateTimeOffset now)
    {
        var plan = snapshot.Plan;
        var detail = plan?.IronCondorTradePlanSnapshot;
        var planCurrent = plan is not null && snapshot.Position is { } position
            && plan.Position.RouteGeneration == position.RouteGeneration && plan.Position.PositionSequence == position.PositionSequence
            && detail?.IsComplete == true && (position.Legs.All(leg => leg.OptionTick is not null && leg.OptionRisk is not null)
                || detail.RiskValidUntilUtc is { } deadline && deadline > now.UtcDateTime);
        if (planCurrent)
        {
            foreach (var leg in snapshot.Legs)
            {
                var quote = leg.Observation;
                var matched = false;
                if (quote is not null && (leg.Position?.OptionRisk is not null || quote.GreeksAvailable == true && quote.ValidUntilUtc > now))
                    foreach (var input in detail!.Legs)
                        if (input.TradeLegId == leg.Definition.TradeLegId && input.QuoteAtUtc >= quote.ObservedAtUtc.UtcDateTime)
                        { matched = true; break; }
                if (!matched) { planCurrent = false; break; }
            }
        }

        ImmutableArray<TradeLegRealtimeSnapshot>.Builder? changedLegs = null;
        var complete = snapshot.Legs.Length > 0;
        var anyStale = false;
        Guid? generation = null;
        var oldest = DateTimeOffset.MaxValue;
        var newest = DateTimeOffset.MinValue;
        var latestUnderlyingCalculation = DateTimeOffset.MinValue;
        double? underlying = null;
        double delta = 0, gamma = 0, vega = 0, theta = 0, rho = 0;
        for (var i = 0; i < snapshot.Legs.Length; i++)
        {
            var leg = snapshot.Legs[i];
            if (leg.Observation is not { } quote) { complete = false; continue; }
            var stale = quote.ValidUntilUtc != default && quote.ValidUntilUtc <= now;
            var quoteStatus = stale ? "Stale" : "Available";
            var pricingStatus = stale ? "Stale" : quote.GreeksAvailable == true ? "Available"
                : quote.PricingFailure == "CalculationPending" ? "Pending" : "Unavailable";
            IronCondorTradePlanLegObservation? scenario = null;
            if (planCurrent)
                foreach (var input in detail!.Legs)
                    if (input.TradeLegId == leg.Definition.TradeLegId) { scenario = input; break; }
            if (leg.QuoteStatus != quoteStatus || leg.PricingStatus != pricingStatus || leg.Scenario != scenario)
            {
                changedLegs ??= snapshot.Legs.ToBuilder();
                changedLegs[i] = leg with { QuoteStatus = quoteStatus, PricingStatus = pricingStatus, Scenario = scenario };
            }
            anyStale |= stale;
            var retainedRisk = leg.Position?.OptionRisk;
            var risk = retainedRisk ?? quote;
            complete &= retainedRisk is not null || pricingStatus == "Available";
            generation ??= quote.SourceGeneration;
            complete &= retainedRisk is not null || generation == quote.SourceGeneration;
            if (quote.ObservedAtUtc < oldest) oldest = quote.ObservedAtUtc;
            if (quote.ObservedAtUtc > newest) newest = quote.ObservedAtUtc;
            if ((!stale || retainedRisk is not null) && risk.UnderlyingPrice > 0
                && (underlying is null || risk.CalculatedAtUtc > latestUnderlyingCalculation))
            { underlying = risk.UnderlyingPrice; latestUnderlyingCalculation = risk.CalculatedAtUtc; }
            var quantity = leg.Position?.SignedQuantity ?? leg.Definition.SignedQuantity;
            delta += risk.Delta * quantity; gamma += risk.Gamma * quantity;
            vega += risk.Vega * quantity; theta += risk.Theta * quantity; rho += risk.Rho * quantity;
        }
        complete &= snapshot.Legs.All(leg => leg.Position?.OptionRisk is not null) || newest - oldest <= TimeSpan.FromSeconds(1);
        decimal? positionValue = snapshot.Position is { } currentPosition && currentPosition.Legs.Length > 0
            && currentPosition.Legs.All(leg => StrategyPositionLegOptionData.ContractMultiplier(leg) is > 0)
            ? currentPosition.Legs.Sum(leg => leg.CurrentPrice * leg.SignedQuantity * StrategyPositionLegOptionData.ContractMultiplier(leg)!.Value)
            : null;
        var summary = snapshot.Summary;
        double? netDelta = complete ? delta : null, netGamma = complete ? gamma : null,
            netVega = complete ? vega : null, netTheta = complete ? theta : null, netRho = complete ? rho : null;
        if (summary.UnderlyingPrice != underlying || summary.NetDelta != netDelta || summary.NetGamma != netGamma
            || summary.NetVega != netVega || summary.NetTheta != netTheta || summary.NetRho != netRho
            || summary.PositionValue != positionValue || summary.DailyPnlPoints != snapshot.Position?.DailyPnl)
            summary = summary with { UnderlyingPrice = underlying, NetDelta = netDelta, NetGamma = netGamma,
                NetVega = netVega, NetTheta = netTheta, NetRho = netRho,
                PositionValue = positionValue, DailyPnlPoints = snapshot.Position?.DailyPnl };
        ImmutableArray<TradeSpreadRealtimeSnapshot>.Builder? changedSpreads = null;
        if (!planCurrent)
            for (var i = 0; i < snapshot.Spreads.Length; i++)
            {
                var spread = snapshot.Spreads[i];
                if (spread.OtmProbability is null && spread.ForwardPrice is null) continue;
                changedSpreads ??= snapshot.Spreads.ToBuilder();
                changedSpreads[i] = spread with { OtmProbability = null, ForwardPrice = null };
            }
        var status = complete ? "Available" : anyStale ? "Stale" : "Pending";
        var planStatus = planCurrent ? "Available" : plan is null ? "Unavailable" : "Stale";
        if (changedLegs is null && changedSpreads is null && ReferenceEquals(summary, snapshot.Summary)
            && status == snapshot.Status && planStatus == snapshot.PlanStatus) return snapshot;
        return snapshot with { Legs = changedLegs?.ToImmutable() ?? snapshot.Legs,
            Spreads = changedSpreads?.ToImmutable() ?? snapshot.Spreads, Summary = summary, Status = status, PlanStatus = planStatus };
    }

    internal static ImmutableArray<TradeSpreadRealtimeSnapshot> CalculateSpreads(TradeRealtimeSnapshot snapshot)
    {
        var plan = snapshot.PlanStatus == "Available" ? snapshot.Plan?.IronCondorTradePlanSnapshot : null;
        return [Spread(snapshot.Legs, 2, plan), Spread(snapshot.Legs, 1, plan)];
    }

    static TradeSpreadRealtimeSnapshot Spread(ImmutableArray<TradeLegRealtimeSnapshot> legs, byte right, IronCondorTradePlanSnapshot? plan)
    {
        TradeLegRealtimeSnapshot? first = null, second = null;
        var count = 0;
        foreach (var leg in legs)
        {
            if (leg.Definition.PutCall != right) continue;
            if (++count == 1) first = leg;
            else if (count == 2) second = leg;
            else break;
        }
        var firstMultiplier = first?.Position is { } firstPosition ? StrategyPositionLegOptionData.ContractMultiplier(firstPosition) : null;
        var secondMultiplier = second?.Position is { } secondPosition ? StrategyPositionLegOptionData.ContractMultiplier(secondPosition) : null;
        var valid = count == 2 && first!.Position is not null && second!.Position is not null;
        decimal? netSpread = null;
        decimal value = 0, pnl = 0;
        if (valid)
        {
            var a = first!.Position!; var b = second!.Position!;
            var quantity = Math.Abs(a.SignedQuantity);
            var balanced = quantity == Math.Abs(b.SignedQuantity);
            // Preserve Enumerable.Sum's checked quantity arithmetic and short-circuiting.
            if (balanced && checked(a.SignedQuantity + b.SignedQuantity) == 0 && quantity > 0)
                netSpread = (a.CurrentPrice * a.SignedQuantity + b.CurrentPrice * b.SignedQuantity) / quantity;
            if (firstMultiplier is > 0 && secondMultiplier is > 0)
            {
                value = a.CurrentPrice * a.SignedQuantity * firstMultiplier.Value
                    + b.CurrentPrice * b.SignedQuantity * secondMultiplier.Value;
                pnl = (a.CurrentPrice - a.OpeningPrice) * a.SignedQuantity * firstMultiplier.Value
                    + (b.CurrentPrice - b.OpeningPrice) * b.SignedQuantity * secondMultiplier.Value;
            }
        }
        return new() { PutCall = right, NetSpread = netSpread,
            TradeValue = valid && firstMultiplier is > 0 && secondMultiplier is > 0 ? value : null,
            TradePnl = valid && firstMultiplier is > 0 && secondMultiplier is > 0 ? pnl : null,
            OtmProbability = plan?.IsComplete == true ? right == 2 ? plan.PutOTMProbability : plan.CallOTMProbability : null,
            ForwardPrice = plan?.IsComplete == true ? right == 2 ? plan.PutForwardPrice : plan.CallForwardPrice : null };
    }
}
