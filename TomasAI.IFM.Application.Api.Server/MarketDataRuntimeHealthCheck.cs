using Microsoft.Extensions.Diagnostics.HealthChecks;
using TomasAI.IFM.Application.MarketData.Databento;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Framework.MarketData.DataBento.TickAggregation.Contracts;

namespace TomasAI.IFM.Application.Api.Server;

/// <summary>
/// Exposes the live market-data epoch state and source-record counters through
/// the API readiness response.
/// </summary>
public sealed class MarketDataRuntimeHealthCheck(
    DatabentoMarketDataApi marketDataApi,
    TimeProvider timeProvider) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var health = marketDataApi.GetHealth();
        var epoch = health.Epoch;
        var now = timeProvider.GetUtcNow();
        var easternNow = TimeZoneInfo.ConvertTime(
            now,
            FuturesTradingValueDate.MarketTimeZone);
        var data = new Dictionary<string, object>
        {
            ["marketTimeEastern"] = easternNow.ToString("O"),
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
        var infrastructureReady = health.Running
            && epoch is { Running: true, AggregationRunning: true, LastPriceStoreActive: true }
            && epoch.Value.ConfiguredContracts > 0;

        var currentContractsLive = AddRoute("ES") & AddRoute("VX");
        data["currentContractsLive"] = currentContractsLive;

        return Task.FromResult(!infrastructureReady
                ? HealthCheckResult.Degraded(
                    "Market-data feeds or aggregation are not ready; market-data features are unavailable but core application services remain ready.",
                    data: data)
                : currentContractsLive
                ? HealthCheckResult.Healthy(
                    "Current futures contracts are feeding downstream notifications.", data)
                : HealthCheckResult.Degraded(
                    "Current futures contracts have not produced recent downstream notifications.",
                    data: data));

        bool AddRoute(string symbol)
        {
            var key = symbol.ToLowerInvariant();
            if (!marketDataApi.TryGetCurrentlyTradedFuturesContract(symbol, out var contract))
            {
                data[$"{key}ContractId"] = string.Empty;
                data[$"{key}RouteActive"] = false;
                return false;
            }

            data[$"{key}ContractId"] = contract.ContractId;
            var status = epoch?.ContractStatuses?.SingleOrDefault(item =>
                StringComparer.Ordinal.Equals(item.ContractId, contract.ContractId));
            var routeActive = marketDataApi.IsTickDataStreamActive(contract.ContractId);
            var routeHealth = status?.HealthAt(now)
                ?? DatabentoLiveFeedHealthState.Inactive;
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
            data[$"{key}FeedHealth"] = routeHealth.ToString();
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

            // Only explicitly owned routes are monitored. Yellow is usable with a
            // warning; red makes live market-data features unavailable.
            return !routeActive
                || status is { ContractConfigured: true, ContractRunning: true }
                && routeHealth is DatabentoLiveFeedHealthState.Green
                    or DatabentoLiveFeedHealthState.Yellow;
        }
    }
}
