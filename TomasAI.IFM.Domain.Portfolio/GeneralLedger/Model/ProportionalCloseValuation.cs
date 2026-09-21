namespace TomasAI.IFM.Domain.Portfolio.GeneralLedger.Model;

/// <summary>Absolute quantities immediately before and during this close, for one position leg.</summary>
public readonly record struct PositionLegCloseQuantity(decimal RemainingBeforeClose, decimal ClosingQuantity);

/// <summary>Allocates aggregate MTM only when every remaining leg closes in the same proportion.</summary>
public static class ProportionalCloseValuation
{
    public static decimal Remaining(decimal previousUnrealized, IReadOnlyList<PositionLegCloseQuantity> legs)
    {
        ArgumentNullException.ThrowIfNull(legs);
        LedgerPostingModel.Money(previousUnrealized);
        if (legs.Count == 0 || legs.Any(x => x.RemainingBeforeClose < 0 || x.ClosingQuantity < 0 ||
                x.ClosingQuantity > x.RemainingBeforeClose))
            throw new ArgumentException("Invalid position-leg closing quantities.");
        var active = legs.Where(x => x.RemainingBeforeClose > 0).ToArray();
        if (active.Length == 0 || active.All(x => x.ClosingQuantity == 0))
            throw new ArgumentException("A close must consume remaining position quantity.");
        var first = active[0];
        // Cross multiplication avoids comparing rounded ratios.
        if (active.Any(x => x.ClosingQuantity * first.RemainingBeforeClose !=
                first.ClosingQuantity * x.RemainingBeforeClose))
            throw new InvalidOperationException("PORTFOLIO_ACCOUNTING.NON_PROPORTIONAL_CLOSE_MTM_RECONCILIATION_REQUIRED");
        var reversed = decimal.Round(previousUnrealized * (first.ClosingQuantity / first.RemainingBeforeClose),
            2, MidpointRounding.ToEven);
        return previousUnrealized - reversed;
    }
}
