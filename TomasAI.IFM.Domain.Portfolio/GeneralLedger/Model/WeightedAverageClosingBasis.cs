namespace TomasAI.IFM.Domain.Portfolio.GeneralLedger.Model;

/// <summary>One opening execution lot within one leg of one strategy position.</summary>
public readonly record struct OpeningBasisLot(decimal SignedQuantity, decimal Price, decimal CashMultiplier);

/// <summary>Signed opening cost allocated to this close and the remaining position.</summary>
public readonly record struct ClosingBasisAllocation(decimal AllocatedSignedBasis, decimal RemainingSignedQuantity,
    decimal RemainingSignedBasis, decimal CumulativeClosedQuantity, decimal CumulativeAllocatedSignedBasis);

/// <summary>
/// Pure weighted-average allocation. The caller must durably serialize consumed quantity and basis
/// per position/leg; this calculator does not provide concurrency control or execution deduplication.
/// Commissions remain separate from opening settlement cost.
/// </summary>
public static class WeightedAverageClosingBasis
{
    public static ClosingBasisAllocation Allocate(IReadOnlyList<OpeningBasisLot> opening,
        decimal signedClosingQuantity, decimal previouslyClosedQuantity = 0,
        decimal previouslyAllocatedSignedBasis = 0)
    {
        ArgumentNullException.ThrowIfNull(opening);
        if (opening.Count == 0 || signedClosingQuantity == 0 || previouslyClosedQuantity < 0)
            throw new ArgumentException("Opening evidence and a nonzero closing quantity are required.");
        var direction = Math.Sign(opening[0].SignedQuantity);
        var multiplier = opening[0].CashMultiplier;
        decimal quantity = 0, signedBasis = 0;
        foreach (var lot in opening)
        {
            if (lot.SignedQuantity == 0 || Math.Sign(lot.SignedQuantity) != direction ||
                lot.Price <= 0 || lot.CashMultiplier <= 0 || lot.CashMultiplier != multiplier)
                throw new ArgumentException("Opening lots must describe one consistently directed leg and multiplier.");
            var amount = lot.SignedQuantity * lot.Price * lot.CashMultiplier;
            if (Money(amount) != amount)
                throw new ArgumentException("Opening settlement must resolve exactly to cents.");
            quantity += Math.Abs(lot.SignedQuantity);
            signedBasis += amount;
        }
        var closingQuantity = Math.Abs(signedClosingQuantity);
        if (Math.Sign(signedClosingQuantity) == direction || previouslyClosedQuantity > quantity ||
            closingQuantity > quantity - previouslyClosedQuantity)
            throw new ArgumentException("Closing evidence reverses direction or exceeds the remaining position.");
        // Cumulative rounding makes fragmentation irrelevant and assigns every residual cent on final close.
        var expectedConsumed = Money(signedBasis * (previouslyClosedQuantity / quantity));
        if (previouslyAllocatedSignedBasis != expectedConsumed)
            throw new ArgumentException("Persisted consumed basis disagrees with weighted-average quantity allocation.");
        var cumulativeQuantity = previouslyClosedQuantity + closingQuantity;
        var cumulativeBasis = cumulativeQuantity == quantity
            ? signedBasis : Money(signedBasis * (cumulativeQuantity / quantity));
        return new(cumulativeBasis - previouslyAllocatedSignedBasis,
            direction * (quantity - cumulativeQuantity), signedBasis - cumulativeBasis,
            cumulativeQuantity, cumulativeBasis);
    }

    private static decimal Money(decimal value) => decimal.Round(value, 2, MidpointRounding.ToEven);
}
