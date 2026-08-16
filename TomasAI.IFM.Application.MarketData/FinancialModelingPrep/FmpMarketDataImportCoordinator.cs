using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Shared.ServiceApi;

namespace TomasAI.IFM.Application.MarketData.FinancialModelingPrep;

public sealed class FmpMarketDataImportCoordinator(
    IMarketDataCommandApi commandApi,
    FmpMarketDataImportOptions options,
    ILogger<FmpMarketDataImportCoordinator> logger) : IFmpMarketDataImportCoordinator
{
    private static readonly Meter Meter = new("TomasAI.IFM.Application.MarketData.FMP");
    private static readonly Counter<long> ImportRequests = Meter.CreateCounter<long>("ifm.fmp.import.requests");
    private static readonly Counter<long> ImportSubmissions = Meter.CreateCounter<long>("ifm.fmp.import.submissions");
    private static readonly Counter<long> ImportFailures = Meter.CreateCounter<long>("ifm.fmp.import.failures");
    private static readonly Histogram<double> ImportDuration = Meter.CreateHistogram<double>("ifm.fmp.import.duration.ms");

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
        var operations = new List<ImportOperation>();
        for (var date = request.FromInclusive; date <= request.ToInclusive; date = date.AddDays(1))
        {
            if (request.IncludeTreasury)
                operations.Add(new ImportOperation(date, FmpImportDataset.Treasury));
            if (request.IncludeEconomicCalendar)
                operations.Add(new ImportOperation(date, FmpImportDataset.EconomicCalendar));
        }

        operations.Sort(static (left, right) =>
        {
            var date = left.Date.CompareTo(right.Date);
            return date != 0 ? date : left.Dataset.CompareTo(right.Dataset);
        });

        var requestedDays = request.ToInclusive.DayNumber - request.FromInclusive.DayNumber + 1;
        var requestedDatasetDates = requestedDays
            * (Convert.ToInt32(request.IncludeTreasury) + Convert.ToInt32(request.IncludeEconomicCalendar));
        var dateResults = new List<FmpImportDateResult>(operations.Count);
        var submittedCommands = 0;
        var rejectedSubmissions = 0;
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
                    operation.Date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)).ConfigureAwait(false),
                FmpImportDataset.EconomicCalendar => await _commandApi.ImportEconomicCalendarsAsync(
                    operation.Date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                    request.CountryCodes).ConfigureAwait(false),
                _ => throw new UnreachableException()
            };

            if (!result.Success)
            {
                rejectedSubmissions++;
                remaining = operations.Count - index - 1;
                dateResults.Add(new FmpImportDateResult(
                    operation.Date,
                    operation.Dataset,
                    FmpImportDateStatus.Rejected,
                    null,
                    result.ErrorCode,
                    result.ErrorMessage));
                _logger.LogError(
                    "FMP import command failed for dataset {Dataset} with error code {ErrorCode}.",
                    operation.Dataset,
                    result.ErrorCode);
                break;
            }

            submittedCommands++;
            dateResults.Add(new FmpImportDateResult(
                operation.Date,
                operation.Dataset,
                FmpImportDateStatus.Submitted,
                result.Value,
                0,
                null));
        }

        ImportSubmissions.Add(submittedCommands);
        if (rejectedSubmissions > 0)
        {
            ImportFailures.Add(rejectedSubmissions);
        }

        return new FmpMarketDataImportResult(
            request.FromInclusive,
            request.ToInclusive,
            requestedDatasetDates,
            submittedCommands,
            rejectedSubmissions,
            remaining,
            cancelled,
            dateResults);
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

    private sealed record ImportOperation(DateOnly Date, FmpImportDataset Dataset);
}
