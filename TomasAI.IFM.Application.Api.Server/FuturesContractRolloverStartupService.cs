using TomasAI.IFM.Application.MarketData.Databento;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.Storage.SecuritiesDb.Schema;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Application.Api.Server;

/// <summary>
/// Supervises the optional market-data runtime across API startup and successive
/// futures market-open/value-date sessions without making it a prerequisite for
/// the core API or UI.
/// </summary>
internal sealed class FuturesContractRolloverStartupService(
    SecuritiesSchemaDb schema,
    FuturesContractRolloverStartupCheck check,
    IMarketDataApi marketDataApi,
    IFuturesMarketSessionAuthority marketSessionAuthority,
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
            await InitializeContractsAsync(
                    marketSessionAuthority.Current.OperationalValueDate,
                    cancellationToken)
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
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var session = marketSessionAuthority.Current;
                if (session.ActiveValueDate is { } activeValueDate)
                {
                    var (_, rows) = await InitializeContractsAsync(
                            activeValueDate,
                            stoppingToken)
                        .ConfigureAwait(false);
                    if (_activeValueDate != activeValueDate)
                    {
                        await StopActiveMarketDataAsync().ConfigureAwait(false);
                        await StartMarketDataAsync(activeValueDate, rows, stoppingToken)
                            .ConfigureAwait(false);
                    }
                }
                else
                {
                    await StopActiveMarketDataAsync().ConfigureAwait(false);
                }

                failureReported = false;
                await Task.Delay(
                        GetNextSupervisionDelay(session, timeProvider.GetUtcNow()),
                        timeProvider,
                        stoppingToken)
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
                        "Market-data session supervision is unavailable ({ExceptionType}: {ExceptionMessage}). The core API and UI remain available; retrying.",
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
        DateOnly valueDate,
        CancellationToken cancellationToken)
    {
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

    private async Task StopActiveMarketDataAsync()
    {
        if (_activeValueDate is not { } valueDate)
            return;

        await marketDataApi.StopAsync(valueDate).ConfigureAwait(false);
        _activeValueDate = null;
        logger.LogInformation(
            "Stopped the market-data runtime for completed value date {ValueDate}.",
            valueDate);
    }

    private static TimeSpan GetNextSupervisionDelay(
        MarketSessionReadModel session,
        DateTimeOffset now)
    {
        var transitionUtc = new DateTimeOffset(
            DateTime.SpecifyKind(session.NextTransitionUtc, DateTimeKind.Utc));
        var untilTransition = transitionUtc - now + TimeSpan.FromMilliseconds(200);
        if (untilTransition <= TimeSpan.Zero)
            return TimeSpan.FromMilliseconds(200);
        return untilTransition < RetryDelay ? untilTransition : RetryDelay;
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
        await StopActiveMarketDataAsync().ConfigureAwait(false);
    }
}
