using TomasAI.IFM.Domain.Trade.Shared.Order;

namespace TomasAI.IFM.Domain.Trade.Order.Query.Model;

/// <summary>Stateless provisional strategy economics. No persistence, financial authorization, or quote mutation.</summary>
public static class TradeOrderPreviewCompute
{
    /// <summary>Validates the bounded draft and computes totals using its exact per-leg quote inputs.</summary>
    /// <param name="request">Selected quote snapshots, quantities, price and tick increment.</param>
    /// <returns>Typed strategy totals preserving unavailable inputs and unbounded payoff directions.</returns>
    /// <exception cref="ArgumentException">The draft has invalid identities, quantities, prices, or risk inputs.</exception>
    public static TradeOrderPreview Create(TradeOrderPreviewRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var legs = request.SelectedLegs;
        if (request.InputRevision < 0 || request.StrategyQuantity is < 1 or > 100000 || request.TickIncrement <= 0
            || legs is null || legs.Length > 4 || legs.Any(x => x is null || string.IsNullOrWhiteSpace(x.ContractId)
                || x.Side is not (-1 or 1) || x.Quantity is < 1 or > 100000 || x.CashMultiplier is <= 0
                || x.Bid is < 0 || x.Ask is < 0 || (x.Bid.HasValue && x.Ask.HasValue && x.Ask < x.Bid)
                || x.Strike is < 0 || x.Delta.HasValue && !double.IsFinite(x.Delta.Value)
                || x.Vega.HasValue && !double.IsFinite(x.Vega.Value))
            || legs.Select(x => x.ContractId).Distinct(StringComparer.Ordinal).Count() != legs.Length)
            throw new ArgumentException("TradeOrder.Preview.INVALID_INPUT: valid distinct legs, quantities, quotes, Greeks and tick increment are required.", nameof(request));

        var preview = new TradeOrderPreview { InputRevision = request.InputRevision, ResolvedTickIncrement = request.TickIncrement,
            HasBalancedQuantities = legs.Select(x => x.Quantity).Distinct().Count() <= 1 };
        if (legs is [{ IsFuture: true } future])
            return preview with { OrderPrice = request.EnteredOrderPrice ?? (request.UnderlyingReferencePrice is > 0 ? future.Side * request.UnderlyingReferencePrice : null),
                NetMid = request.UnderlyingReferencePrice is > 0 ? request.UnderlyingReferencePrice : null,
                NetDelta = future.CashMultiplier.HasValue ? future.Side * future.Quantity * (double)future.CashMultiplier.Value : null, NetVega = 0 };
        if (legs.Any(x => x.IsFuture)) throw new ArgumentException("TradeOrder.Preview.UNSUPPORTED_LEGS: futures and options cannot be mixed in this preview.", nameof(request));
        if (legs.Length == 0 || legs.Any(x => !x.Bid.HasValue || !x.Ask.HasValue)) return preview;
        var netBid = legs.Sum(x => x.Side * (x.Side > 0 ? x.Bid!.Value : x.Ask!.Value) * x.Quantity) / request.StrategyQuantity;
        var netAsk = legs.Sum(x => x.Side * (x.Side > 0 ? x.Ask!.Value : x.Bid!.Value) * x.Quantity) / request.StrategyQuantity;
        var mid = (netBid + netAsk) / 2;
        preview = preview with { HasCompleteQuotes = true, NetBid = netBid, NetAsk = netAsk, NetMid = mid,
            OrderPrice = request.EnteredOrderPrice ?? Math.Round(mid / request.TickIncrement, MidpointRounding.AwayFromZero) * request.TickIncrement,
            NetDelta = legs.All(x => x.Delta.HasValue && x.CashMultiplier.HasValue) ? legs.Sum(x => x.Side * x.Delta!.Value * x.Quantity * (double)x.CashMultiplier!.Value) : null,
            NetVega = legs.All(x => x.Vega.HasValue && x.CashMultiplier.HasValue) ? legs.Sum(x => x.Side * x.Vega!.Value * x.Quantity * (double)x.CashMultiplier!.Value) : null,
            CallWidth = Width(legs.Where(x => x.IsCall).ToArray()), PutWidth = Width(legs.Where(x => !x.IsCall).ToArray()),
            CallSpreadValue = SpreadValue(legs.Where(x => x.IsCall).ToArray()), PutSpreadValue = SpreadValue(legs.Where(x => !x.IsCall).ToArray()) };
        if (preview.NetDelta.HasValue && !double.IsFinite(preview.NetDelta.Value)
            || preview.NetVega.HasValue && !double.IsFinite(preview.NetVega.Value))
            throw new ArgumentException("TradeOrder.Preview.NONFINITE_EXPOSURE: aggregate Greeks exceed supported numeric limits.", nameof(request));
        if (legs.Any(x => !x.CashMultiplier.HasValue || !x.Strike.HasValue)) return preview;
        var cost = preview.HasBalancedQuantities && legs.Select(x => x.CashMultiplier).Distinct().Count() == 1
            ? preview.OrderPrice!.Value * request.StrategyQuantity * legs[0].CashMultiplier!.Value
            : legs.Sum(x => x.Side * (x.Bid!.Value + x.Ask!.Value) / 2 * x.Quantity * x.CashMultiplier!.Value);
        var (maximumProfit, maximumLoss) = TradeOrderPayoffCompute.Calculate(legs, cost);
        return preview with { HasPayoffEstimate = true, EstimatedCredit = -cost, MaximumProfit = maximumProfit, MaximumLoss = maximumLoss,
            HasUnlimitedProfit = !maximumProfit.HasValue, HasUnlimitedLoss = !maximumLoss.HasValue,
            MaximumReturn = maximumLoss is > 0 && maximumProfit.HasValue ? maximumProfit.Value / maximumLoss.Value : null };
    }

    /// <summary>Returns the strike distance of an opposing pair when both strikes are known.</summary>
    static decimal? Width(TradeOrderPreviewLeg[] legs) => legs.Length == 2 && legs.All(x => x.Strike.HasValue) && legs.Sum(x => x.Side) == 0
        ? Math.Abs(legs[0].Strike!.Value - legs[1].Strike!.Value) : null;

    /// <summary>Returns the side's cash credit at quote midpoints with actual contract quantities.</summary>
    static decimal? SpreadValue(TradeOrderPreviewLeg[] legs) => legs.Length > 0 && legs.All(x => x.CashMultiplier.HasValue)
        ? -legs.Sum(x => x.Side * (x.Bid!.Value + x.Ask!.Value) / 2 * x.Quantity * x.CashMultiplier!.Value) : null;
}
