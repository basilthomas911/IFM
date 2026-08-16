using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.MarketData.Contracts;

namespace TomasAI.IFM.Application.MarketData.FinancialModelingPrep;

public sealed class FmpMarketDataImportCoordinator(
    ITreasuryCurve treasuryCurve,
    IEconomicCalendar economicCalendar,
    IMarketDataCommandApi commandApi,
    FmpMarketDataImportOptions options,
    ILogger<FmpMarketDataImportCoordinator> logger) : IFmpMarketDataImportCoordinator
{
    private const string Source = "FinancialModelingPrep";
    private static readonly Meter Meter = new("TomasAI.IFM.Application.MarketData.FMP");
    private static readonly Counter<long> ImportRequests = Meter.CreateCounter<long>("ifm.fmp.import.requests");
    private static readonly Counter<long> ImportRows = Meter.CreateCounter<long>("ifm.fmp.import.rows");
    private static readonly Counter<long> ImportFailures = Meter.CreateCounter<long>("ifm.fmp.import.failures");
    private static readonly Histogram<double> ImportDuration = Meter.CreateHistogram<double>("ifm.fmp.import.duration.ms");

    private readonly ITreasuryCurve _treasuryCurve = treasuryCurve
        ?? throw new ArgumentNullException(nameof(treasuryCurve));
    private readonly IEconomicCalendar _economicCalendar = economicCalendar
        ?? throw new ArgumentNullException(nameof(economicCalendar));
    private readonly IMarketDataCommandApi _commandApi = commandApi
        ?? throw new ArgumentNullException(nameof(commandApi));
    private readonly FmpMarketDataImportOptions _options = Validate(options);
    private readonly ILogger<FmpMarketDataImportCoordinator> _logger = logger
        ?? throw new ArgumentNullException(nameof(logger));

    public async Task<FmpMarketDataImportResult> ImportAsync(
        FmpMarketDataImportRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateRequest(request);

        var started = Stopwatch.GetTimestamp();
        ImportRequests.Add(1);
        try
        {
            return await ImportCoreAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            ImportFailures.Add(1);
            throw;
        }
        finally
        {
            ImportDuration.Record(Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        }
    }

    private async Task<FmpMarketDataImportResult> ImportCoreAsync(
        FmpMarketDataImportRequest request,
        CancellationToken cancellationToken)
    {
        var countries = NormalizeCountries(request.CountryCodes);
        IReadOnlyList<TreasuryCurveSnapshot> treasurySnapshots = [];
        IReadOnlyList<EconomicCalendarEntry> calendarEntries = [];

        // Complete acquisition and validation before submitting the first command.
        if (request.IncludeTreasury)
        {
            treasurySnapshots = await _treasuryCurve
                .GetRangeAsync(request.FromInclusive, request.ToInclusive, cancellationToken)
                .ConfigureAwait(false);
        }

        if (request.IncludeEconomicCalendar)
        {
            calendarEntries = await _economicCalendar
                .GetAsync(request.FromInclusive, request.ToInclusive, countries, cancellationToken)
                .ConfigureAwait(false);
        }

        var operations = new List<ImportOperation>();
        if (request.IncludeTreasury)
        {
            operations.AddRange(treasurySnapshots
                .GroupBy(snapshot => snapshot.ValueDate)
                .Select(group => new ImportOperation(
                    group.Key,
                    FmpImportDataset.Treasury,
                    group.Select(MapTreasury).ToArray())));
        }

        if (request.IncludeEconomicCalendar)
        {
            operations.AddRange(calendarEntries
                .GroupBy(entry => DateOnly.FromDateTime(entry.EventTimeUtc.UtcDateTime))
                .Select(group => new ImportOperation(
                    group.Key,
                    FmpImportDataset.EconomicCalendar,
                    group.Select(MapEconomicCalendar).ToArray())));
        }

        operations.Sort(static (left, right) =>
        {
            var date = left.Date.CompareTo(right.Date);
            return date != 0 ? date : left.Dataset.CompareTo(right.Dataset);
        });

        var requestedDays = request.ToInclusive.DayNumber - request.FromInclusive.DayNumber + 1;
        var requestedDatasetDates = requestedDays
            * (Convert.ToInt32(request.IncludeTreasury) + Convert.ToInt32(request.IncludeEconomicCalendar));
        var noDataDates = requestedDatasetDates - operations.Count;
        var acquiredRows = treasurySnapshots.Count + calendarEntries.Count;
        var dateResults = new List<FmpImportDateResult>(operations.Count);
        var acceptedCommands = 0;
        var acceptedRows = 0;
        var failedDates = 0;
        var remaining = 0;
        var cancelled = false;

        for (var index = 0; index < operations.Count; index++)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                cancelled = true;
                remaining = operations.Count - index;
                break;
            }

            var operation = operations[index];
            var result = operation.Dataset switch
            {
                FmpImportDataset.Treasury => await _commandApi.ImportYieldCurveRatesAsync(
                    operation.Date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                    (YieldCurveRateReadModel[])operation.Rows).ConfigureAwait(false),
                FmpImportDataset.EconomicCalendar => await _commandApi.ImportEconomicCalendarsAsync(
                    operation.Date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                    (EconomicCalendarReadModel[])operation.Rows).ConfigureAwait(false),
                _ => throw new UnreachableException()
            };

            if (!result.Success)
            {
                failedDates++;
                remaining = operations.Count - index - 1;
                dateResults.Add(new FmpImportDateResult(
                    operation.Date,
                    operation.Dataset,
                    FmpImportDateStatus.Failed,
                    operation.Rows.Length,
                    result.Value,
                    result.ErrorCode,
                    result.ErrorMessage));
                _logger.LogError(
                    "FMP import command failed for dataset {Dataset} with error code {ErrorCode}.",
                    operation.Dataset,
                    result.ErrorCode);
                break;
            }

            acceptedCommands++;
            acceptedRows += operation.Rows.Length;
            dateResults.Add(new FmpImportDateResult(
                operation.Date,
                operation.Dataset,
                FmpImportDateStatus.Accepted,
                operation.Rows.Length,
                result.Value,
                0,
                null));
        }

        ImportRows.Add(acceptedRows);
        if (failedDates > 0)
        {
            ImportFailures.Add(failedDates);
        }

        return new FmpMarketDataImportResult(
            request.FromInclusive,
            request.ToInclusive,
            requestedDatasetDates,
            acquiredRows,
            acceptedCommands,
            acceptedRows,
            noDataDates,
            failedDates,
            remaining,
            cancelled,
            dateResults);
    }

    private static YieldCurveRateReadModel MapTreasury(TreasuryCurveSnapshot snapshot) =>
        new(
            snapshot.ValueDate,
            Rate(snapshot, TreasuryTenor.OneMonth),
            Rate(snapshot, TreasuryTenor.TwoMonth),
            Rate(snapshot, TreasuryTenor.ThreeMonth),
            Rate(snapshot, TreasuryTenor.SixMonth),
            Rate(snapshot, TreasuryTenor.OneYear),
            Rate(snapshot, TreasuryTenor.TwoYear),
            Rate(snapshot, TreasuryTenor.ThreeYear),
            Rate(snapshot, TreasuryTenor.FiveYear),
            Rate(snapshot, TreasuryTenor.SevenYear),
            Rate(snapshot, TreasuryTenor.TenYear),
            Rate(snapshot, TreasuryTenor.TwentyYear),
            Rate(snapshot, TreasuryTenor.ThirtyYear));

    private static EconomicCalendarReadModel MapEconomicCalendar(EconomicCalendarEntry entry) =>
        new(
            entry.EventTimeUtc.UtcDateTime,
            entry.CountryCode,
            entry.EventName,
            entry.Actual,
            entry.Forecast,
            entry.Previous,
            entry.RetrievedAtUtc.UtcDateTime,
            Source,
            entry.Impact,
            entry.Unit,
            entry.Change,
            entry.ChangePercentage);

    private static double Rate(TreasuryCurveSnapshot snapshot, TreasuryTenor tenor)
    {
        if (!snapshot.TryGetRate(tenor, out var point))
        {
            throw new FmpMarketDataImportException(
                $"Treasury curve {snapshot.ValueDate:yyyy-MM-dd} is missing tenor {tenor}.");
        }

        return decimal.ToDouble(point.RatePercent);
    }

    private static HashSet<string>? NormalizeCountries(string[]? countryCodes)
    {
        if (countryCodes is null || countryCodes.Length == 0)
        {
            return null;
        }

        var result = new HashSet<string>(StringComparer.Ordinal);
        foreach (var countryCode in countryCodes)
        {
            var normalized = countryCode?.Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(normalized)
                || normalized.Length is < 2 or > 3
                || normalized.Any(character => !char.IsAsciiLetter(character)))
            {
                throw new FmpMarketDataImportException(
                    "FMP country filters must be two- or three-letter codes.");
            }

            result.Add(normalized);
        }

        return result;
    }

    private void ValidateRequest(FmpMarketDataImportRequest request)
    {
        if (!request.IncludeTreasury && !request.IncludeEconomicCalendar)
        {
            throw new FmpMarketDataImportException("At least one FMP dataset must be selected.");
        }

        if (request.FromInclusive > request.ToInclusive)
        {
            throw new FmpMarketDataImportException("The FMP import start date is after its end date.");
        }

        var days = request.ToInclusive.DayNumber - request.FromInclusive.DayNumber + 1;
        if (days > _options.MaximumRangeDays)
        {
            throw new FmpMarketDataImportException(
                $"FMP imports may span at most {_options.MaximumRangeDays} days.");
        }
    }

    private static FmpMarketDataImportOptions Validate(FmpMarketDataImportOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        return options;
    }

    private sealed record ImportOperation(
        DateOnly Date,
        FmpImportDataset Dataset,
        Array Rows);
}
