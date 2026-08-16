using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.MarketData.Contracts;
using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.EconomicCalendarsDb;

/// <summary>
/// Compatibility facade for callers of the former external-URI repository.
/// Provider failures remain failures and are never converted into empty data.
/// </summary>
public sealed class EconomicCalendarsDbContext(
    IEconomicCalendar economicCalendar,
    ExternalMarketDataCompatibilityOptions options,
    TimeProvider timeProvider,
    ILogger<DbProvider> logger)
    : ObjectDataRepository<EconomicCalendarsDbContext>(null!, logger), IEconomicCalendarsDbContext
{
    private readonly IEconomicCalendar _economicCalendar = economicCalendar
        ?? throw new ArgumentNullException(nameof(economicCalendar));
    private readonly ExternalMarketDataCompatibilityOptions _options = Validate(options);
    private readonly TimeProvider _timeProvider = timeProvider
        ?? throw new ArgumentNullException(nameof(timeProvider));

    public override EconomicCalendarsDbContext Database => this;

    public Task<ICollection<EconomicCalendarReadModel>> ReadAsync() =>
        ReadAsync(CancellationToken.None);

    public Task<ICollection<EconomicCalendarReadModel>> ReadAsync(
        CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);
        return ReadAsync(
            today.AddDays(-_options.EconomicCalendarLookbackDays),
            today.AddDays(_options.EconomicCalendarForwardDays),
            _options.EconomicCalendarCountryCodes,
            cancellationToken);
    }

    public async Task<ICollection<EconomicCalendarReadModel>> ReadAsync(
        DateOnly fromInclusive,
        DateOnly toInclusive,
        IReadOnlySet<string>? countryCodes = null,
        CancellationToken cancellationToken = default)
    {
        var entries = await _economicCalendar
            .GetAsync(fromInclusive, toInclusive, countryCodes, cancellationToken)
            .ConfigureAwait(false);

        return entries.Select(Map).ToArray();
    }

    private static EconomicCalendarReadModel Map(EconomicCalendarEntry entry) =>
        new(
            entry.EventTimeUtc.UtcDateTime,
            entry.CountryCode,
            entry.EventName,
            entry.Actual,
            entry.Forecast,
            entry.Previous,
            entry.RetrievedAtUtc.UtcDateTime,
            entry.Source,
            entry.Impact,
            entry.Unit,
            entry.Change,
            entry.ChangePercentage);

    private static ExternalMarketDataCompatibilityOptions Validate(
        ExternalMarketDataCompatibilityOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        return options;
    }
}
