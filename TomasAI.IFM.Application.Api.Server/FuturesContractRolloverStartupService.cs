using TomasAI.IFM.Application.MarketData.Databento;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.Storage.SecuritiesDb.Schema;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Application.Api.Server;

/// <summary>
/// Starts the optional market-data runtime without making it a prerequisite for
/// the core API or UI. Startup is deferred while feed monitoring is out of hours.
/// </summary>
internal sealed class FuturesContractRolloverStartupService(
    SecuritiesSchemaDb schema,
    FuturesContractRolloverStartupCheck check,
    IMarketDataApi marketDataApi,
    TimeProvider timeProvider,
    ILogger<FuturesContractRolloverStartupService> logger) : BackgroundService
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMinutes(1);
    private DateOnly? _activeValueDate;
    private DateOnly? _initializedValueDate;
    private IReadOnlyCollection<FuturesContractRolloverReadModel>? _initializedRows;

    /// <summary>
    /// Initializes the current futures contract catalog before API readiness can
    /// admit a manual feed-start request. Provider failures remain non-fatal and
    /// are retried by the background execution loop.
    /// </summary>
    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await InitializeContractsAsync(timeProvider.GetUtcNow(), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                "Initial market-data contract configuration is unavailable ({ExceptionType}: {ExceptionMessage}). The core API remains available and background retry will continue.",
                exception.GetType().Name,
                exception.Message);
        }

        await base.StartAsync(cancellationToken).ConfigureAwait(false);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var failureReported = false;
        while (!stoppingToken.IsCancellationRequested && _activeValueDate is null)
        {
            var now = timeProvider.GetUtcNow();
            try
            {
                var (valueDate, rows) = await InitializeContractsAsync(now, stoppingToken)
                    .ConfigureAwait(false);
                failureReported = false;
                if (!MarketDataFeedMonitoringWindow.IsOpen(now))
                {
                    var nextStart = MarketDataFeedMonitoringWindow.GetNextStartUtc(now);
                    logger.LogInformation(
                        "Market-data contracts are initialized for manual feed startup. Automatic feed startup is paused outside 03:00-16:00 Eastern; next attempt: {NextAttemptUtc}.",
                        nextStart);
                    await Task.Delay(nextStart - now, timeProvider, stoppingToken)
                        .ConfigureAwait(false);
                    continue;
                }

                await StartMarketDataAsync(valueDate, rows, stoppingToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                if (!failureReported)
                {
                    logger.LogWarning(
                        "Market-data startup is unavailable ({ExceptionType}: {ExceptionMessage}). The core API and UI remain available; retrying while the monitoring window is open.",
                        exception.GetType().Name,
                        exception.Message);
                    failureReported = true;
                }

                await Task.Delay(RetryDelay, timeProvider, stoppingToken)
                    .ConfigureAwait(false);
            }
        }
    }

    private async Task<(DateOnly ValueDate, IReadOnlyCollection<FuturesContractRolloverReadModel> Rows)>
        InitializeContractsAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var valueDate = FuturesTradingValueDate.GetOperational(now);
        if (_initializedValueDate == valueDate && _initializedRows is { } initializedRows)
            return (valueDate, initializedRows);

        logger.LogInformation("Creating or validating the futures rollover schema.");
        await schema.CreateAsync(["futures_contract_rollover"], cancellationToken)
            .ConfigureAwait(false);
        logger.LogInformation("Futures rollover schema is ready.");
        logger.LogInformation(
            "Resolving required futures rollover contracts for value date {ValueDate}.",
            valueDate);
        var rows = await check.ExecuteAsync(valueDate, cancellationToken)
            .ConfigureAwait(false);
        _initializedValueDate = valueDate;
        _initializedRows = rows;
        logger.LogInformation(
            "Initialized {RolloverCount} futures rollover rows for value date {ValueDate}.",
            rows.Count,
            valueDate);
        return (valueDate, rows);
    }

    private async Task StartMarketDataAsync(
        DateOnly valueDate,
        IReadOnlyCollection<FuturesContractRolloverReadModel> rows,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Resolved {RolloverCount} futures rollover rows; starting the market-data runtime.",
            rows.Count);
        await marketDataApi.StartAsync(valueDate, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        _activeValueDate = valueDate;
        logger.LogInformation(
            "Validated {RolloverCount} futures rollover rows for value date {ValueDate}.",
            rows.Count,
            valueDate);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken).ConfigureAwait(false);
        if (_activeValueDate is not { } valueDate)
            return;
        await marketDataApi.StopAsync(valueDate).ConfigureAwait(false);
        _activeValueDate = null;
    }
}
