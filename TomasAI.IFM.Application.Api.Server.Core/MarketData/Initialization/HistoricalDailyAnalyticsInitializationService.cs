using TomasAI.IFM.Application.Api.Server.Core.Hosting;
using TomasAI.IFM.Application.Api.Server.Core.Startup.Readiness;
using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Application.MarketData.Historical;
using TomasAI.IFM.Application.MarketData.MarketOutlook;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Shared.MarketData.Contracts.Historical;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.Api.Server.Core.MarketData.Initialization;

/// <summary>
/// Initializes Daily EMA and Bollinger state from durable EOD history independently
/// of the optional application-start workflow.
/// </summary>
public sealed class HistoricalDailyAnalyticsInitializationService(
    IHostApplicationLifetime lifetime,
    IApplicationBootstrapReadiness bootstrapReadiness,
    IFuturesMarketSessionAuthority marketSessionAuthority,
    TomasAI.IFM.Application.MarketData.Contracts.IMarketDataApi marketDataApi,
    HistoricalAnalyticsWarmupOptions options,
    HistoricalAnalyticsWarmupService warmup,
    IMarketOutlookOperations marketOutlookOperations,
    TimeProvider timeProvider,
    ILogger<HistoricalDailyAnalyticsInitializationService> logger,
    IJSActorProducer? producer = null) : BackgroundService
{
    string? lastStatusKey;
    DailyAnalyticsInitializationStatusEvent? status;
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
                        await DeliverStatusAsync(stoppingToken).ConfigureAwait(false);
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
                        "{Component}.{Method} "+"Daily EMA and Bollinger initialization completed from historical EOD data. Outcome={Outcome}; ValueDate={ValueDate}; ContractId={ContractId}; ValidSessions={ValidSessions}.",nameof(HistoricalDailyAnalyticsInitializationService),nameof(ExecuteAsync),                        result.Outcome,                        valueDate,                        contractId,                        result.ValidSessionCount);
                    initializedValueDate = valueDate;
                    await PublishStatusAsync(valueDate, null, stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception exception)
                {
                    logger.LogError(
                        exception,                        "{Component}.{Method} "+"Daily EMA and Bollinger initialization failed; retrying in one minute.",nameof(HistoricalDailyAnalyticsInitializationService),nameof(ExecuteAsync));
                    await PublishStatusAsync(marketSessionAuthority.Current.OperationalValueDate, exception, stoppingToken).ConfigureAwait(false);
                    await Task.Delay(TimeSpan.FromMinutes(1), timeProvider, stoppingToken)
                        .ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    async Task PublishStatusAsync(DateOnly valueDate, Exception? error, CancellationToken token)
    {
        var baseline = warmup.BaselineValueDate;
        var key = $"{valueDate:O}|{baseline:O}|{error is not null}";
        if (key != lastStatusKey)
        {
            var id = new MarketDataFeedId(valueDate);
            status = new DailyAnalyticsInitializationStatusEvent
            {
                Id = Guid.NewGuid(), CommandId = Guid.NewGuid(), EntityId = id,
                Subject = new(ActorType.Event, DailyAnalyticsInitializationStatusEvent.Actor, DailyAnalyticsInitializationStatusEvent.Verb, id.Format()),
                EventSource = nameof(HistoricalDailyAnalyticsInitializationService),
                ReceivedOn = timeProvider.GetUtcNow().UtcDateTime, ErrorDate = timeProvider.GetUtcNow().UtcDateTime,
                BaselineValueDate = baseline, BaselineIsStale = warmup.BaselineIsStale,
                RefreshFailed = error is not null, ErrorCode = error is null ? 0 : 503,
                ErrorMessage = (baseline is { } date ? $"Daily EMA / Bollinger using {(warmup.BaselineIsStale ? "STALE " : "")}baseline dated {date:yyyy-MM-dd}. "
                    : "Daily EMA / Bollinger initialization unavailable; live previews have no initialized baseline. ")
                    + (error is null ? "Historical initialization completed." : "Historical refresh failed and will retry: " + error.Message)
            };
            lastStatusKey = key;
        }
        await DeliverStatusAsync(token).ConfigureAwait(false);
    }

    async Task DeliverStatusAsync(CancellationToken token)
    {
        if (producer is null || status is null) return;
        try
        {
            if (!producer.IsRunning)
                await producer.StartAsync(new(ActorType.Event, nameof(HistoricalDailyAnalyticsInitializationService)), token).ConfigureAwait(false);
            await producer.SendAsync<DailyAnalyticsInitializationStatusEvent, MarketDataFeedId>(status.Subject, status, token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception error) { logger.LogWarning(error, "Daily analytics status delivery failed; retained status will be retried."); }
    }

    async Task<string> ResolveCurrentEsContractIdAsync(
        DateOnly valueDate,
        CancellationToken cancellationToken)
    {
        var contracts = await marketDataApi.PrepareSessionAsync(valueDate, cancellationToken)
            .ConfigureAwait(false);
        var current = contracts
            .Where(contract => contract.Symbol == "ES" && contract.IsValid
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
