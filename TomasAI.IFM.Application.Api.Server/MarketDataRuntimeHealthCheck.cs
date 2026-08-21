using Microsoft.Extensions.Diagnostics.HealthChecks;
using TomasAI.IFM.Application.MarketData.Databento;

namespace TomasAI.IFM.Application.Api.Server;

/// <summary>
/// Exposes the live market-data epoch state and source-record counters through
/// the API readiness response.
/// </summary>
public sealed class MarketDataRuntimeHealthCheck(DatabentoMarketDataApi marketDataApi) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var health = marketDataApi.GetHealth();
        var epoch = health.Epoch;
        var data = new Dictionary<string, object>
        {
            ["running"] = health.Running,
            ["valueDate"] = health.ValueDate?.ToString("yyyy-MM-dd") ?? string.Empty,
            ["aggregationRunning"] = epoch?.AggregationRunning ?? false,
            ["configuredContracts"] = epoch?.ConfiguredContracts ?? 0,
            ["lastPriceStoreActive"] = epoch?.LastPriceStoreActive ?? false,
            ["sourceQuoteRecords"] = epoch?.SourceQuoteRecords ?? 0,
            ["sourceTradeRecords"] = epoch?.SourceTradeRecords ?? 0,
            ["publicationFailures"] = epoch?.PublicationFailures ?? 0
        };
        var ready = health.Running
            && epoch is { Running: true, AggregationRunning: true, LastPriceStoreActive: true }
            && epoch.Value.ConfiguredContracts > 0;

        AddRoute("ES");
        AddRoute("VX");

        return Task.FromResult(ready
            ? HealthCheckResult.Healthy("Market-data feeds and aggregation are running.", data)
            : HealthCheckResult.Unhealthy(
                "Market-data feeds or aggregation are not ready.",
                data: data));

        void AddRoute(string symbol)
        {
            var key = symbol.ToLowerInvariant();
            if (!marketDataApi.TryGetCurrentlyTradedFuturesContract(symbol, out var contract))
            {
                data[$"{key}ContractId"] = string.Empty;
                data[$"{key}RouteActive"] = false;
                return;
            }

            data[$"{key}ContractId"] = contract.ContractId;
            data[$"{key}RouteActive"] =
                marketDataApi.IsTickDataStreamActive(contract.ContractId);
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
        }
    }
}
