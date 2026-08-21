using TomasAI.IFM.Application.MarketData.Databento;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.Storage.SecuritiesDb.Schema;
using TomasAI.IFM.Domain.MarketData.Shared;

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

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var failureReported = false;
        while (!stoppingToken.IsCancellationRequested && _activeValueDate is null)
        {
            var now = timeProvider.GetUtcNow();
            if (!MarketDataFeedMonitoringWindow.IsOpen(now))
            {
                failureReported = false;
                var nextStart = MarketDataFeedMonitoringWindow.GetNextStartUtc(now);
                logger.LogInformation(
                    "Market-data startup is paused outside 03:00-16:00 Eastern; the core API and UI remain available. Next attempt: {NextAttemptUtc}.",
                    nextStart);
                await Task.Delay(nextStart - now, timeProvider, stoppingToken)
                    .ConfigureAwait(false);
                continue;
            }

            try
            {
                await StartMarketDataAsync(now, stoppingToken).ConfigureAwait(false);
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

    private async Task StartMarketDataAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Creating or validating the futures rollover schema.");
        await schema.CreateAsync(["futures_contract_rollover"], cancellationToken)
            .ConfigureAwait(false);
        logger.LogInformation("Futures rollover schema is ready.");
        var valueDate = FuturesTradingValueDate.GetOperational(now);
        logger.LogInformation(
            "Resolving required futures rollover contracts for value date {ValueDate}.",
            valueDate);
        var rows = await check.ExecuteAsync(valueDate, cancellationToken)
            .ConfigureAwait(false);
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
