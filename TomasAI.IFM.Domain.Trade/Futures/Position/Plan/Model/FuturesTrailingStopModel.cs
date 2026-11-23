using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;

namespace TomasAI.IFM.Domain.Trade.Futures.Position.Plan.Model;

/// <summary>Tracks an outright-futures stop in price points, independently of quantity and cash P&amp;L.</summary>
public static class FuturesTrailingStopModel
{
    public const decimal TrailingStopPoints = 10m;

    public static FuturesTrailingStop Calculate(
        StrategyPositionSnapshot position,
        TradePlanParameters parameters,
        StrategyTradePlanSnapshot? previous,
        DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(position);
        ArgumentNullException.ThrowIfNull(parameters);
        var leg = position.Legs[0];
        var sign = Math.Sign(leg.SignedQuantity);
        var extreme = leg.OpeningPrice;
        var samePosition = false;
        if (previous is { Position.Legs.Length: 1 }
            && previous.Position.Id == position.Id
            && previous.Position.Legs[0].TradeLegId == leg.TradeLegId
            && StringComparer.Ordinal.Equals(previous.Position.Legs[0].ContractId, leg.ContractId)
            && Math.Sign(previous.Position.Legs[0].SignedQuantity) == sign
            && previous.Position.Legs[0].OpeningPrice == leg.OpeningPrice)
        {
            samePosition = true;
            var priorExtreme = previous.FuturesTrailingExtremePrice ?? leg.OpeningPrice;
            extreme = sign > 0 ? Math.Max(extreme, priorExtreme) : Math.Min(extreme, priorExtreme);
        }

        var fresh = position.IsOpen && position.Phase != StrategyPositionPhase.Close
            && position.AsOfUtc <= nowUtc && leg.LastPriceAtUtc <= nowUtc
            && nowUtc - position.AsOfUtc <= TimeSpan.FromSeconds(parameters.MaximumDataAgeSeconds)
            && leg.LastPriceAtUtc.Kind == DateTimeKind.Utc
            && nowUtc - leg.LastPriceAtUtc <= TimeSpan.FromSeconds(parameters.MaximumDataAgeSeconds);
        if (fresh)
            extreme = sign > 0 ? Math.Max(extreme, leg.CurrentPrice) : Math.Min(extreme, leg.CurrentPrice);
        var entry = position.DailyFuturesEntry;
        var tightened = entry is not null && (samePosition && previous!.FuturesTrailTightened
            || sign * (extreme - entry.DcPrice) >= entry.Threshold * .5m);
        var distance = entry is null ? TrailingStopPoints : entry.Threshold * (tightened ? .5m : 1m);
        if (distance <= 0) throw new InvalidOperationException("PLAN.DAILY.ATR_THRESHOLD_INVALID");
        var stop = extreme - sign * distance;
        if (samePosition && previous!.FuturesStopPrice is { } priorStop)
            stop = sign > 0 ? Math.Max(stop, priorStop) : Math.Min(stop, priorStop);
        return new(extreme, stop, fresh && sign * (leg.CurrentPrice - stop) <= 0, tightened);
    }
}

public readonly record struct FuturesTrailingStop(decimal ExtremePrice, decimal StopPrice, bool Reached, bool Tightened = false);
