using Microsoft.Extensions.Diagnostics.HealthChecks;
using TomasAI.IFM.Application.MarketData.Databento;
using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ServiceApi;
using TomasAI.IFM.Framework.MarketData.DataBento.TickAggregation.Contracts;

namespace TomasAI.IFM.Application.Api.Server;

/// <summary>
/// Exposes the live market-data epoch state and source-record counters through
/// the API readiness response.
/// </summary>
public sealed class MarketDataRuntimeHealthCheck(
    DatabentoMarketDataApi marketDataApi,
    IFuturesMarketSessionAuthority marketSessionAuthority,
    IFuturesContractRolloverStore rolloverStore,
    IFuturesExchangeBusinessCalendar businessCalendar,
    TimeProvider timeProvider) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var health = marketDataApi.GetHealth();
        var epoch = health.Epoch;
        var now = timeProvider.GetUtcNow();
        var marketState = marketSessionAuthority.Current.State;
        var databentoFeedUp = marketDataApi.IsDatabentoFeedUp();
        var easternNow = TimeZoneInfo.ConvertTime(
            now,
            FuturesTradingValueDate.MarketTimeZone);
        var data = new Dictionary<string, object>
        {
            ["marketTimeEastern"] = easternNow.ToString("O"),
            ["marketState"] = marketState.ToString(),
            ["databentoFeedUp"] = databentoFeedUp,
            ["running"] = health.Running,
            ["valueDate"] = health.ValueDate?.ToString("yyyy-MM-dd") ?? string.Empty,
            ["aggregationRunning"] = epoch?.AggregationRunning ?? false,
            ["configuredContracts"] = epoch?.ConfiguredContracts ?? 0,
            ["lastPriceStoreActive"] = epoch?.LastPriceStoreActive ?? false,
            ["sourceQuoteRecords"] = epoch?.SourceQuoteRecords ?? 0,
            ["sourceTradeRecords"] = epoch?.SourceTradeRecords ?? 0,
            ["publicationFailures"] = epoch?.PublicationFailures ?? 0,
            ["processingFailures"] = epoch?.ProcessingFailures ?? 0
        };
        foreach (var datasetFeed in epoch?.DatasetFeedStatuses ?? [])
        {
            var key = datasetFeed.Dataset
                .Replace(".", "_", StringComparison.Ordinal)
                .Replace("-", "_", StringComparison.Ordinal)
                .ToLowerInvariant();
            var feed = datasetFeed.Health;
            data[$"{key}NativeState"] = feed.State.ToString();
            data[$"{key}NativeTerminalStatus"] = feed.TerminalStatus.ToString();
            data[$"{key}NativeWarning"] = feed.Warning ?? string.Empty;
            data[$"{key}TransportReady"] = feed.TransportReady;
            data[$"{key}TradingReady"] = feed.TradingReady;
            data[$"{key}RingCapacityRecords"] = feed.RingCapacityRecords;
            data[$"{key}RingUsedRecords"] = feed.RingUsedRecords;
            data[$"{key}RingHighWaterRecords"] = feed.RingHighWaterRecords;
            data[$"{key}RecordsProduced"] = feed.RecordsProduced;
            data[$"{key}RecordsConsumed"] = feed.RecordsConsumed;
            data[$"{key}ChannelFullCount"] = feed.ChannelFullCount;
            data[$"{key}PoolMissCount"] = feed.PoolMissCount;
        }
        data["sourceValueDateRevision"] = marketSessionAuthority.Current.Revision;
        foreach (var symbol in new[] { "ES", "VX" })
        {
            var key = symbol.ToLowerInvariant();
            var rollover = await rolloverStore.GetFuturesContractRolloverAsync(
                symbol, cancellationToken).ConfigureAwait(false);
            data[$"{key}NextRolloverValueDate"] =
                rollover?.NextRolloverDate?.ToString("yyyy-MM-dd") ?? string.Empty;
            data[$"{key}RolloverPreparationDate"] = rollover?.NextRolloverDate is { } effective
                ? businessCalendar.GetPreparationDate(effective).ToString("yyyy-MM-dd")
                : string.Empty;
            var set = marketDataApi.TryGetFuturesTermStructureContracts(symbol, out var pair)
                ? new[] { pair.Front.ContractId, pair.Back.ContractId }
                : marketDataApi.TryGetOnTheRunFuturesContract(symbol, out var onTheRun)
                    ? new[] { onTheRun.ContractId }
                    : [];
            data[$"{key}RolloverSet"] = string.Join(",", set);
        }
        var infrastructureReady = databentoFeedUp
            && health.Running
            && epoch is { Running: true, AggregationRunning: true, LastPriceStoreActive: true }
            && epoch.Value.ConfiguredContracts > 0;

        var currentContractsLive = AddRoute("ES") & AddRoute("VX");
        data["currentContractsLive"] = currentContractsLive;

        return marketState == FuturesMarketState.Closed
                ? HealthCheckResult.Healthy(
                    "Futures market is closed; live feed health is inactive and core services remain ready.",
                    data)
                : !infrastructureReady
                ? HealthCheckResult.Degraded(
                    "Market-data feeds or aggregation are not ready; market-data features are unavailable but core application services remain ready.",
                    data: data)
                : currentContractsLive
                ? HealthCheckResult.Healthy(
                    marketState == FuturesMarketState.LiveTrading
                        ? "Current futures contracts are green during live trading."
                        : "Current futures contracts are active within the off-hours fifteen-minute allowance.",
                    data)
                : HealthCheckResult.Degraded(
                    marketState == FuturesMarketState.LiveTrading
                        ? "One or more current futures contracts are not green during live trading."
                        : "One or more current futures contracts have received no accepted off-hours update for over fifteen minutes; feeds remain live.",
                    data: data);

        bool AddRoute(string symbol)
        {
            var key = symbol.ToLowerInvariant();
            if (!marketDataApi.TryGetOnTheRunFuturesContract(symbol, out var contract))
            {
                data[$"{key}ContractId"] = string.Empty;
                data[$"{key}RouteActive"] = false;
                return false;
            }

            data[$"{key}ContractId"] = contract.ContractId;
            var status = epoch?.ContractStatuses?.SingleOrDefault(item =>
                StringComparer.Ordinal.Equals(item.ContractId, contract.ContractId));
            var routeActive = marketDataApi.IsTickDataStreamActive(contract.ContractId);
            var activationUtc = health.ValueDate is { } valueDate
                ? FuturesTradingValueDate.GetSessionStartUtc(valueDate)
                : now;
            var routeHealth = MarketDataFeedSessionHealthPolicy.Evaluate(
                marketState,
                now,
                activationUtc,
                status?.LastAcceptedCacheUpdateAtUtc,
                routeActive,
                status is { ContractConfigured: true, ContractRunning: true });
            data[$"{key}RouteActive"] = routeActive;
            data[$"{key}AggregationRunning"] = status?.ContractRunning ?? false;
            data[$"{key}LastSourceRecordUtc"] =
                status?.LastSourceRecordObservedAtUtc?.ToString("O") ?? string.Empty;
            data[$"{key}LastAcceptedCacheUpdateUtc"] =
                status?.LastAcceptedCacheUpdateAtUtc?.ToString("O") ?? string.Empty;
            data[$"{key}LastAcceptedSourceEventUtc"] =
                status?.LastAcceptedSourceEventAtUtc?.ToString("O") ?? string.Empty;
            data[$"{key}LastNotificationUtc"] =
                status?.LastMarketPricePublishedAtUtc?.ToString("O") ?? string.Empty;
            data[$"{key}LastDurableTickUtc"] =
                status?.LastDurableTickPublishedAtUtc?.ToString("O") ?? string.Empty;
            data[$"{key}AcceptedCacheUpdates"] = status?.AcceptedCacheUpdates ?? 0;
            data[$"{key}RejectedCacheUpdates"] = status?.RejectedCacheUpdates ?? 0;
            var sessionHealth = marketState switch
            {
                FuturesMarketState.Closed => "Inactive",
                _ => routeHealth.ToString()
            };
            data[$"{key}FeedHealth"] = sessionHealth;
            if (marketDataApi.TryGetFuturesSessionStatistics(contract.ContractId, out var statistics))
            {
                data[$"{key}SessionStatisticsComplete"] = statistics.IsComplete;
                data[$"{key}SessionOpen"] = statistics.OpenPrice;
                data[$"{key}SessionHigh"] = statistics.HighPrice;
                data[$"{key}SessionLow"] = statistics.LowPrice;
                data[$"{key}SessionVolume"] = statistics.Volume;
            }
            else
            {
                data[$"{key}SessionStatisticsComplete"] = false;
            }

            // Only explicitly owned routes are monitored. Live yellow/red degrade
            // market-data readiness; off-hours degradation leaves ownership intact.
            return !routeActive
                || status is { ContractConfigured: true, ContractRunning: true }
                && (marketState switch
                    {
                        FuturesMarketState.Closed => true,
                        FuturesMarketState.OffTrading => routeHealth == MarketDataFeedSessionHealthState.OffHoursActive,
                        _ => routeHealth == MarketDataFeedSessionHealthState.Green
                    });
        }
    }
}
