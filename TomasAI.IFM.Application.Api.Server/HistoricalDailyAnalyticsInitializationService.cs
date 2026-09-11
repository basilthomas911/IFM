using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Application.MarketData.Historical;
using TomasAI.IFM.Application.MarketData.MarketOutlook;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Framework.MarketData.Contracts.Historical;

namespace TomasAI.IFM.Application.Api.Server;

/// <summary>
/// Initializes Daily EMA and Bollinger state from durable EOD history independently
/// of the optional application-start workflow.
/// </summary>
public sealed class HistoricalDailyAnalyticsInitializationService(
    IHostApplicationLifetime lifetime,
    IApplicationBootstrapReadiness bootstrapReadiness,
    IFuturesMarketSessionAuthority marketSessionAuthority,
    ICurrentFuturesContractCatalog contractCatalog,
    HistoricalAnalyticsWarmupOptions options,
    HistoricalAnalyticsWarmupService warmup,
    IMarketOutlookOperations marketOutlookOperations,
    TimeProvider timeProvider,
    ILogger<HistoricalDailyAnalyticsInitializationService> logger) : BackgroundService
{
    static readonly MarketSeriesIdentity EsContinuation = MarketSeriesIdentity.ForFuturesSeries(
        new FuturesSeriesId("ES", "calendar-front", "unadjusted", 1));

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Enabled)
            return;

        try
        {
            if (!await HostedServiceLifecycle.WaitForSignalAsync(
                    lifetime.ApplicationStarted, stoppingToken).ConfigureAwait(false))
                return;

            DateOnly? initializedValueDate = null;
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    if (!await bootstrapReadiness.IsHealthyAsync(stoppingToken).ConfigureAwait(false))
                    {
                        await Task.Delay(TimeSpan.FromMilliseconds(250), timeProvider, stoppingToken)
                            .ConfigureAwait(false);
                        continue;
                    }

                    var valueDate = marketSessionAuthority.Current.OperationalValueDate;
                    if (initializedValueDate == valueDate)
                    {
                        await Task.Delay(TimeSpan.FromMinutes(1), timeProvider, stoppingToken)
                            .ConfigureAwait(false);
                        continue;
                    }
                    var contractId = await ResolveCurrentEsContractIdAsync(valueDate, stoppingToken)
                        .ConfigureAwait(false);
                    var result = await warmup.EnsureAsync(new MarketDataHistoricalRequest
                    {
                        DataLoadAttemptId = Guid.NewGuid(),
                        Series =
                        [
                            new MarketDataHistoricalSeriesRequest
                            {
                                SeriesIdentity = EsContinuation,
                                Schema = HistoricalDataSchema.OhlcvDaily
                            }
                        ],
                        StartDate = valueDate.AddYears(-1),
                        EndDate = valueDate,
                        MaximumCostUsd = options.MaximumCostUsd,
                        MaximumBytes = options.MaximumBytes,
                        NormalizationVersion = options.NormalizationVersion,
                        RequestedBy = nameof(HistoricalDailyAnalyticsInitializationService),
                        AnalyticsTargetContractId = contractId
                    }, stoppingToken).ConfigureAwait(false);

                    if (!await marketOutlookOperations.WaitForIdleAsync(
                            TimeSpan.FromMinutes(2), stoppingToken).ConfigureAwait(false))
                        throw new TimeoutException(
                            "Market Outlook did not apply the historical EMA/Bollinger initialization.");

                    logger.LogInformation(
                        "Daily EMA and Bollinger initialization completed from historical EOD data. Outcome={Outcome}; ValueDate={ValueDate}; ContractId={ContractId}; ValidSessions={ValidSessions}.",
                        result.Outcome,
                        valueDate,
                        contractId,
                        result.ValidSessionCount);
                    initializedValueDate = valueDate;
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception exception)
                {
                    logger.LogError(
                        exception,
                        "Daily EMA and Bollinger initialization failed; retrying in one minute.");
                    await Task.Delay(TimeSpan.FromMinutes(1), timeProvider, stoppingToken)
                        .ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    async Task<string> ResolveCurrentEsContractIdAsync(
        DateOnly valueDate,
        CancellationToken cancellationToken)
    {
        var contracts = await contractCatalog.GetByRootAsync("ES", cancellationToken)
            .ConfigureAwait(false);
        var current = contracts
            .Where(contract => contract.IsValid
                && contract.LastTradeDate >= valueDate
                && contract.LastTradeDate.Month is 3 or 6 or 9 or 12)
            .OrderBy(contract => contract.LastTradeDate)
            .ThenBy(contract => contract.ContractId, StringComparer.Ordinal)
            .FirstOrDefault();
        return current?.ContractId
            ?? throw new InvalidOperationException(
                $"No eligible current ES quarterly contract is available for {valueDate:yyyy-MM-dd}.");
    }
}
