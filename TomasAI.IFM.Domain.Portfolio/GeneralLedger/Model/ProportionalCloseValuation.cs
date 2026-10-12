namespace TomasAI.IFM.Domain.Portfolio.GeneralLedger.Model;

/// <summary>Allocates aggregate MTM only when every remaining leg closes in the same proportion.</summary>
public static class ProportionalCloseValuation
{
    public static decimal Remaining(decimal previousUnrealized, IReadOnlyList<PositionLegCloseQuantity> legs)
    {
        ArgumentNullException.ThrowIfNull(legs);
        LedgerPostingModel.Money(previousUnrealized);
        if (legs.Count == 0 || legs.Any(x => x.RemainingBeforeClose < 0 || x.ClosingQuantity < 0 ||
                x.ClosingQuantity > x.RemainingBeforeClose))
            throw new ArgumentException("Invalid position-leg closing quantities.",nameof(legs));
        var active = legs.Where(x => x.RemainingBeforeClose > 0).ToArray();
        if (active.Length == 0 || active.All(x => x.ClosingQuantity == 0))
            throw new ArgumentException("A close must consume remaining position quantity.", nameof(legs));
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
