namespace TomasAI.IFM.Application.MarketData.Pricing;

/// <summary>Extracts expiry-specific ATM IV from accepted live selection or full valuation snapshots.</summary>
public static class OptionChainAtmIvEstimator
{
    /// <summary>Uses the nearest quoted strike within 0.5% of the forward; averages call/put IV when both are available.</summary>
    public static double? Read(MarketCompositionSnapshot snapshot, decimal forward)
    {
        if (forward <= 0) return null;
        var nearest = snapshot.Instruments.Where(x => x.Instrument.Strike is > 0
                && x.Instrument.Quote is not null && Math.Abs(x.Instrument.Strike.Value - forward) <= forward * .005m)
            .Select(x => new { Row = x, Iv = x.Valuation?.ImpliedVolatility
                ?? (x.Instrument.Selection is { } selection && selection.ValidUntilUtc > snapshot.EvaluatedAtUtc
                    && selection.CalculatedAtUtc <= snapshot.EvaluatedAtUtc ? (double?)selection.ImpliedVolatility : null) })
            .Where(x => x.Iv is > 0 && double.IsFinite(x.Iv.Value))
            .GroupBy(x => x.Row.Instrument.Strike!.Value)
            .OrderBy(x => Math.Abs(x.Key - forward)).ThenBy(x => x.Key).FirstOrDefault();
        return nearest?.GroupBy(x => x.Row.Instrument.IsCall).Select(x => x.OrderBy(r => r.Row.Instrument.ContractId,
            StringComparer.Ordinal).First().Iv!.Value).Average();
    }
}
