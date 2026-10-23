using TomasAI.IFM.Domain.Trade.Shared.Order;

namespace TomasAI.IFM.Domain.Trade.Order.Query.Model;

/// <summary>Exact terminal option payoff bounds at strike breakpoints and the unbounded call tail.</summary>
public static class TradeOrderPayoffCompute
{
    public static (decimal? MaximumProfit, decimal? MaximumLoss) Calculate(TradeOrderPreviewLeg[] legs, decimal cashCost)
    {
        ArgumentNullException.ThrowIfNull(legs);
        if (legs.Length is < 1 or > 4 || legs.Any(l => l.IsFuture || l.Strike is null or < 0 || l.CashMultiplier is null or <= 0
            || l.Side is not (-1 or 1) || l.Quantity is < 1 or > 100000))
            throw new ArgumentException("Resolved option strikes, quantities, signs and multipliers are required.", nameof(legs));
        var profits = legs.Select(l => l.Strike!.Value).Append(0m).Distinct().Select(spot =>
            legs.Sum(l => l.Side * l.Quantity * l.CashMultiplier!.Value * Math.Max(l.IsCall ? spot - l.Strike!.Value : l.Strike!.Value - spot, 0m)) - cashCost).ToArray();
        var slope = legs.Where(l => l.IsCall).Sum(l => l.Side * l.Quantity * l.CashMultiplier!.Value);
        return (slope > 0 ? null : profits.Max(), slope < 0 ? null : Math.Max(0m, -profits.Min()));
    }
}
