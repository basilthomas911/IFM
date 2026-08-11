namespace TomasAI.IFM.Framework.MarketData.Contracts;

/// <summary>
/// A normalized economic data release suitable for application processing,
/// caching, and durable storage.
/// </summary>
/// <param name="EventTimeUtc">
/// Scheduled or published release time normalized to UTC.
/// </param>
/// <param name="CountryCode">Normalized ISO-like country code.</param>
/// <param name="EventName">Normalized, non-empty event name.</param>
/// <param name="Actual">Provider representation of the actual value.</param>
/// <param name="Forecast">Provider representation of the forecast value.</param>
/// <param name="Previous">Provider representation of the previous value.</param>
/// <param name="Impact">Optional normalized impact classification.</param>
/// <param name="Unit">Optional unit supplied for the release values.</param>
/// <param name="Change">Optional provider representation of the change.</param>
/// <param name="ChangePercentage">
/// Optional provider representation of percentage change.
/// </param>
/// <param name="RetrievedAtUtc">UTC time at which the source was acquired.</param>
/// <param name="Source">
/// Stable, non-secret source identifier used for storage provenance and
/// diagnostics.
/// </param>
/// <remarks>
/// Value fields remain strings because economic releases may contain units,
/// suffixes, or non-numeric status text. Missing values remain
/// <see langword="null"/> and are never converted to zero. Logical identity is
/// <c>(EventTimeUtc, CountryCode, EventName)</c>.
/// </remarks>
public sealed record EconomicCalendarEntry(
    DateTimeOffset EventTimeUtc,
    string CountryCode,
    string EventName,
    string? Actual,
    string? Forecast,
    string? Previous,
    string? Impact,
    string? Unit,
    string? Change,
    string? ChangePercentage,
    DateTimeOffset RetrievedAtUtc,
    string Source);
