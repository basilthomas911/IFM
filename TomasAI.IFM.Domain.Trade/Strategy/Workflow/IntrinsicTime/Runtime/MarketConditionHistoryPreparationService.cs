using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Application.MarketData.Databento.Historical;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Realtime.Actor;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Runtime;

public sealed partial class MarketConditionHistoryPreparationService(IMarketDataApi marketData, IDbContextFactory storage,
    IMarketSessionCalendar calendar, MarketConditionHistoryCache cache, IntrinsicTimeStrategyWorkflowOptions options,
    ILogger<MarketConditionHistoryPreparationService> logger, TimeProvider? timeProvider = null) : BackgroundService
{
    readonly TimeProvider clock = timeProvider ?? TimeProvider.System;
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(10), clock);
        do
        {
            try
            {
                if (!options.Enabled || !marketData.TryGetOnTheRunFuturesContract("VX", out var contract)
                    || !marketData.TryGetLastTickPrice(contract.ContractId, out var price)) continue;
                var now = clock.GetUtcNow();
                var target = MarketTradingTime.Add(calendar, now, TimeSpan.FromMinutes(-5));
                var from = MarketTradingTime.Add(calendar, target, TimeSpan.FromMinutes(-5));
                var to = MarketTradingTime.Add(calendar, target, TimeSpan.FromMinutes(1));
                var bars = await storage.MarketDataDb.GetFuturesBarDataAsync(contract.ContractId, "VX", price.ValueDate,
                    from.UtcDateTime, to.UtcDateTime).ConfigureAwait(false);
                cache.Publish(contract.ContractId, price.ValueDate, now.UtcDateTime, bars);
            }
            catch (Exception error) when (!stoppingToken.IsCancellationRequested) { HistoryUnavailable(logger, error); }
        } while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }
    [LoggerMessage(Level = LogLevel.Warning, Message = "Market Condition historical context preparation failed.")]
    static partial void HistoryUnavailable(ILogger logger, Exception error);
}
