using System.Globalization;
using TomasAI.IFM.Framework.MarketData.Contracts;

namespace TomasAI.IFM.Framework.MarketData.FinancialModelingPrep;

public sealed class FinancialModelingPrepTreasuryCurve : ITreasuryCurve
{
    private const string ProviderSource = "FinancialModelingPrep";
    private readonly FinancialModelingPrepOptions _options;
    private readonly FinancialModelingPrepHttpClient _client;
    private readonly TimeProvider _timeProvider;

    public FinancialModelingPrepTreasuryCurve(
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

    internal FinancialModelingPrepTreasuryCurve(
        HttpClient httpClient,
        FinancialModelingPrepOptions options,
        FinancialModelingPrepRequestGate requestGate,
        TimeProvider timeProvider)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _client = new FinancialModelingPrepHttpClient(httpClient, options, requestGate, timeProvider);
    }

    public Task<TreasuryCurveSnapshot?> GetLatestAsync(
        DateOnly asOfDate,
        CancellationToken cancellationToken = default)
    {
        var fromDayNumber = Math.Max(
            DateOnly.MinValue.DayNumber,
            asOfDate.DayNumber - _options.LatestTreasuryLookbackDays + 1);
        var fromInclusive = DateOnly.FromDayNumber(fromDayNumber);

        return FinancialModelingPrepProviderUtilities.RunBoundedAsync(
            _options,
            cancellationToken,
            async operationToken =>
            {
                var rows = await GetRangeCoreAsync(fromInclusive, asOfDate, operationToken).ConfigureAwait(false);
                return rows.Count == 0 ? null : rows[^1];
            });
    }

    public Task<IReadOnlyList<TreasuryCurveSnapshot>> GetRangeAsync(
        DateOnly fromInclusive,
        DateOnly toInclusive,
        CancellationToken cancellationToken = default)
    {
        FinancialModelingPrepProviderUtilities.ValidateRange(fromInclusive, toInclusive, _options);
        return FinancialModelingPrepProviderUtilities.RunBoundedAsync(
            _options,
            cancellationToken,
            token => GetRangeCoreAsync(fromInclusive, toInclusive, token));
    }

    private async Task<IReadOnlyList<TreasuryCurveSnapshot>> GetRangeCoreAsync(
        DateOnly fromInclusive,
        DateOnly toInclusive,
        CancellationToken cancellationToken)
    {
        var retrievedAtUtc = _timeProvider.GetUtcNow();
        var results = new Dictionary<DateOnly, TreasuryCurveSnapshot>();

        foreach (var chunk in FinancialModelingPrepProviderUtilities.ChunkRange(
                     fromInclusive,
                     toInclusive,
                     _options.MaximumProviderWindowDays))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var uri = FinancialModelingPrepHttpClient.BuildDateRangeUri(
                _options.TreasuryRatesEndpoint,
                chunk.From,
                chunk.To);
            var payload = await _client.GetAsync(uri, cancellationToken).ConfigureAwait(false);
            var providerRows = FinancialModelingPrepProviderUtilities.DeserializeArray<FinancialModelingPrepTreasuryRateDto>(
                payload,
                "Treasury-rate");

            foreach (var providerRow in providerRows)
            {
                var snapshot = Map(providerRow, retrievedAtUtc);
                if (snapshot.ValueDate < fromInclusive || snapshot.ValueDate > toInclusive)
                {
                    throw new FinancialModelingPrepContractException("FMP returned a Treasury curve outside the requested date range.");
                }

                if (results.TryGetValue(snapshot.ValueDate, out var existing))
                {
                    if (!existing.Rates.SequenceEqual(snapshot.Rates))
                    {
                        throw new FinancialModelingPrepContractException(
                            $"FMP returned conflicting Treasury curves for {snapshot.ValueDate:yyyy-MM-dd}.");
                    }

                    continue;
                }

                results.Add(snapshot.ValueDate, snapshot);
                if (results.Count > _options.MaximumNormalizedRows)
                {
                    throw new FinancialModelingPrepResponseException("The normalized FMP Treasury result exceeded the configured row limit.");
                }
            }
        }

        return results.Values.OrderBy(row => row.ValueDate).ToArray();
    }

    private static TreasuryCurveSnapshot Map(
        FinancialModelingPrepTreasuryRateDto row,
        DateTimeOffset retrievedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(row.Date)
            || !DateOnly.TryParseExact(row.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var valueDate))
        {
            throw new FinancialModelingPrepContractException("An FMP Treasury row has a missing or invalid date.");
        }

        var rates = new[]
        {
            Required(TreasuryTenor.OneMonth, row.Month1, "month1"),
            Required(TreasuryTenor.TwoMonth, row.Month2, "month2"),
            Required(TreasuryTenor.ThreeMonth, row.Month3, "month3"),
            Required(TreasuryTenor.SixMonth, row.Month6, "month6"),
            Required(TreasuryTenor.OneYear, row.Year1, "year1"),
            Required(TreasuryTenor.TwoYear, row.Year2, "year2"),
            Required(TreasuryTenor.ThreeYear, row.Year3, "year3"),
            Required(TreasuryTenor.FiveYear, row.Year5, "year5"),
            Required(TreasuryTenor.SevenYear, row.Year7, "year7"),
            Required(TreasuryTenor.TenYear, row.Year10, "year10"),
            Required(TreasuryTenor.TwentyYear, row.Year20, "year20"),
            Required(TreasuryTenor.ThirtyYear, row.Year30, "year30")
        };

        return new TreasuryCurveSnapshot(valueDate, rates, retrievedAtUtc, ProviderSource);
    }

    private static TreasuryRatePoint Required(TreasuryTenor tenor, decimal? value, string providerField)
    {
        if (value is null)
        {
            throw new FinancialModelingPrepContractException(
                $"FMP Treasury field '{providerField}' is required and cannot be represented as zero when absent.");
        }

        return new TreasuryRatePoint(tenor, value.Value);
    }
}
