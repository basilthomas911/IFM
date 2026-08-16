using TomasAI.IFM.Framework.MarketData.Contracts;

namespace TomasAI.IFM.Framework.MarketData.FinancialModelingPrep;

public sealed class FinancialModelingPrepEconomicCalendar : IEconomicCalendar
{
    private const string ProviderSource = "FinancialModelingPrep";
    private readonly FinancialModelingPrepOptions _options;
    private readonly FinancialModelingPrepHttpClient _client;
    private readonly TimeProvider _timeProvider;

    public FinancialModelingPrepEconomicCalendar(
        HttpClient httpClient,
        FinancialModelingPrepOptions options,
        TimeProvider? timeProvider = null)
        : this(
            httpClient,
            options,
            new FinancialModelingPrepRequestGate(options),
            timeProvider ?? TimeProvider.System)
    {
    }

    internal FinancialModelingPrepEconomicCalendar(
        HttpClient httpClient,
        FinancialModelingPrepOptions options,
        FinancialModelingPrepRequestGate requestGate,
        TimeProvider timeProvider)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _client = new FinancialModelingPrepHttpClient(httpClient, options, requestGate, timeProvider);
    }

    public Task<IReadOnlyList<EconomicCalendarEntry>> GetAsync(
        DateOnly fromInclusive,
        DateOnly toInclusive,
        IReadOnlySet<string>? countryCodes = null,
        CancellationToken cancellationToken = default)
    {
        FinancialModelingPrepProviderUtilities.ValidateRange(fromInclusive, toInclusive, _options);
        var normalizedCountries = NormalizeCountries(countryCodes);
        return FinancialModelingPrepProviderUtilities.RunBoundedAsync(
            _options,
            cancellationToken,
            token => GetCoreAsync(fromInclusive, toInclusive, normalizedCountries, token));
    }

    private async Task<IReadOnlyList<EconomicCalendarEntry>> GetCoreAsync(
        DateOnly fromInclusive,
        DateOnly toInclusive,
        HashSet<string>? countryCodes,
        CancellationToken cancellationToken)
    {
        var retrievedAtUtc = _timeProvider.GetUtcNow();
        var results = new Dictionary<(DateTimeOffset EventTimeUtc, string CountryCode, string EventName), EconomicCalendarEntry>();

        foreach (var chunk in FinancialModelingPrepProviderUtilities.ChunkRange(
                     fromInclusive,
                     toInclusive,
                     _options.MaximumProviderWindowDays))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var uri = FinancialModelingPrepHttpClient.BuildDateRangeUri(
                _options.EconomicCalendarEndpoint,
                chunk.From,
                chunk.To);
            var payload = await _client.GetAsync(uri, cancellationToken).ConfigureAwait(false);
            var providerRows = FinancialModelingPrepProviderUtilities.DeserializeArray<FinancialModelingPrepEconomicCalendarDto>(
                payload,
                "economic-calendar");

            foreach (var providerRow in providerRows)
            {
                var entry = Map(providerRow, retrievedAtUtc);
                var eventDate = DateOnly.FromDateTime(entry.EventTimeUtc.UtcDateTime);
                if (eventDate < fromInclusive || eventDate > toInclusive)
                {
                    throw new FinancialModelingPrepContractException("FMP returned an economic-calendar event outside the requested UTC-date range.");
                }

                if (countryCodes is not null && !countryCodes.Contains(entry.CountryCode))
                {
                    continue;
                }

                var key = (entry.EventTimeUtc, entry.CountryCode, entry.EventName);
                if (results.TryGetValue(key, out var existing))
                {
                    if (existing != entry)
                    {
                        throw new FinancialModelingPrepContractException(
                            "FMP returned conflicting economic-calendar rows for one logical event identity.");
                    }

                    continue;
                }

                results.Add(key, entry);
                if (results.Count > _options.MaximumNormalizedRows)
                {
                    throw new FinancialModelingPrepResponseException("The normalized FMP economic-calendar result exceeded the configured row limit.");
                }
            }
        }

        return results.Values
            .OrderBy(row => row.EventTimeUtc)
            .ThenBy(row => row.CountryCode, StringComparer.Ordinal)
            .ThenBy(row => row.EventName, StringComparer.Ordinal)
            .ToArray();
    }

    private static EconomicCalendarEntry Map(
        FinancialModelingPrepEconomicCalendarDto row,
        DateTimeOffset retrievedAtUtc)
    {
        var eventTimeUtc = FinancialModelingPrepProviderUtilities.ParseEventTimeUtc(row.Date);
        var countryCode = NormalizeRequired(row.Country, "country").ToUpperInvariant();
        var eventName = NormalizeRequired(row.Event, "event");

        return new EconomicCalendarEntry(
            eventTimeUtc,
            countryCode,
            eventName,
            FinancialModelingPrepProviderUtilities.PreserveScalar(row.Actual, "actual"),
            FinancialModelingPrepProviderUtilities.PreserveScalar(row.Estimate, "estimate"),
            FinancialModelingPrepProviderUtilities.PreserveScalar(row.Previous, "previous"),
            FinancialModelingPrepProviderUtilities.PreserveScalar(row.Impact, "impact"),
            FinancialModelingPrepProviderUtilities.PreserveScalar(row.Unit, "unit"),
            FinancialModelingPrepProviderUtilities.PreserveScalar(row.Change, "change"),
            FinancialModelingPrepProviderUtilities.PreserveScalar(row.ChangePercentage, "changePercentage"),
            retrievedAtUtc,
            ProviderSource);
    }

    private static HashSet<string>? NormalizeCountries(IReadOnlySet<string>? countries)
    {
        if (countries is null || countries.Count == 0)
        {
            return null;
        }

        var normalized = new HashSet<string>(StringComparer.Ordinal);
        foreach (var country in countries)
        {
            var value = country?.Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(value)
                || value.Length is < 2 or > 3
                || value.Any(character => !char.IsAsciiLetter(character)))
            {
                throw new FinancialModelingPrepValidationException("Economic-calendar country filters must be two- or three-letter codes.");
            }

            normalized.Add(value);
        }

        return normalized;
    }

    private static string NormalizeRequired(string? value, string providerField)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrEmpty(normalized))
        {
            throw new FinancialModelingPrepContractException(
                $"FMP economic-calendar field '{providerField}' is required.");
        }

        return normalized;
    }
}
