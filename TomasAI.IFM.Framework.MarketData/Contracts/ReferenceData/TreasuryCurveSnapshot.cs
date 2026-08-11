namespace TomasAI.IFM.Framework.MarketData.Contracts;

/// <summary>
/// A normalized Treasury curve for one value date.
/// </summary>
/// <param name="ValueDate">The published curve value date.</param>
/// <param name="Rates">
/// Unique tenor points ordered by <see cref="TreasuryRatePoint.Tenor"/>.
/// Missing tenors are omitted and are never represented by a zero rate.
/// </param>
/// <param name="RetrievedAtUtc">UTC time at which the source was acquired.</param>
/// <param name="Source">
/// Stable, non-secret source identifier used for storage provenance and
/// diagnostics.
/// </param>
public sealed record TreasuryCurveSnapshot(
    DateOnly ValueDate,
    IReadOnlyList<TreasuryRatePoint> Rates,
    DateTimeOffset RetrievedAtUtc,
    string Source)
{
    /// <summary>ISO-like country code for the issuing sovereign.</summary>
    public string CountryCode { get; init; } = "US";

    /// <summary>ISO 4217 currency code in which the rates are expressed.</summary>
    public string CurrencyCode { get; init; } = "USD";

    /// <summary>Attempts to read one tenor without allocating.</summary>
    public bool TryGetRate(
        TreasuryTenor tenor,
        out TreasuryRatePoint rate)
    {
        for (var index = 0; index < Rates.Count; index++)
        {
            if (Rates[index].Tenor != tenor)
                continue;

            rate = Rates[index];
            return true;
        }

        rate = default;
        return false;
    }
}
