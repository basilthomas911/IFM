namespace TomasAI.IFM.Framework.MarketData.Contracts;

/// <summary>
/// Provides normalized sovereign Treasury curves without exposing a market-data
/// vendor's transport or response models.
/// </summary>
public interface ITreasuryCurve
{
    /// <summary>
    /// Gets the newest curve whose value date is on or before
    /// <paramref name="asOfDate"/>.
    /// </summary>
    /// <remarks>
    /// Implementations must not return a curve from a future date. A
    /// <see langword="null"/> result means no qualifying curve exists.
    /// </remarks>
    Task<TreasuryCurveSnapshot?> GetLatestAsync(
        DateOnly asOfDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets curves whose value dates are in the inclusive requested range.
    /// </summary>
    /// <remarks>
    /// Results are ordered by <see cref="TreasuryCurveSnapshot.ValueDate"/>
    /// ascending and contain at most one curve per country, currency, and value
    /// date. An empty list is a successful no-data result.
    /// </remarks>
    Task<IReadOnlyList<TreasuryCurveSnapshot>> GetRangeAsync(
        DateOnly fromInclusive,
        DateOnly toInclusive,
        CancellationToken cancellationToken = default);
}
