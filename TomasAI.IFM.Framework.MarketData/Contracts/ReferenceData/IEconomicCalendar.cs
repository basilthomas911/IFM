namespace TomasAI.IFM.Framework.MarketData.Contracts;

/// <summary>
/// Provides normalized economic-release calendar entries without exposing a
/// market-data vendor's transport or response models.
/// </summary>
public interface IEconomicCalendar
{
    /// <summary>
    /// Gets economic events occurring within the inclusive UTC-date range.
    /// </summary>
    /// <param name="fromInclusive">First UTC event date to include.</param>
    /// <param name="toInclusive">Last UTC event date to include.</param>
    /// <param name="countryCodes">
    /// Optional normalized country-code filter. A <see langword="null"/> or
    /// empty set requests all countries permitted by host policy.
    /// </param>
    /// <remarks>
    /// Results are ordered by event time, country code, and event name. The
    /// logical event identity is event time UTC, country code, and event name.
    /// An empty list is a successful no-data result.
    /// </remarks>
    Task<IReadOnlyList<EconomicCalendarEntry>> GetAsync(
        DateOnly fromInclusive,
        DateOnly toInclusive,
        IReadOnlySet<string>? countryCodes = null,
        CancellationToken cancellationToken = default);
}
