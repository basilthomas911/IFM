using System.Collections.ObjectModel;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Application.MarketData.Databento.Workers;
using TomasAI.IFM.Application.MarketData.MarketOutlook;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.MarketData.Contracts.TickAggregation;

namespace TomasAI.IFM.Application.MarketData.OperationsHealth;

/// <summary>Observes cached state, not native/worker commands. Independent of the watchdog and processor loops.</summary>
public sealed class MarketDataOperationsHealthObserver(
    MarketDataOperationsHealthService health, DatasetWorkerProcessRecoveryService workers,
    IMarketOutlookOperations outlook, IFuturesMarketSessionAuthority sessions,
    DatabentoMarketDataWatchdogService watchdog, DatabentoStage3Options options,
    ITickAggregationEventPublisher publisher, TimeProvider time,
    ILogger<MarketDataOperationsHealthObserver> logger) : BackgroundService
{
    public void ObserveOnce() => health.ObserveRuntime(sessions.Current, watchdog.Current, workers.Current,
        outlook.GetMetrics(), (publisher as ITickAggregationPublisherDiagnostics)?.GetSnapshot(), options);

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "The owned background worker records or returns its operation failure while keeping subsequent independent work available.")]
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { ObserveOnce(); }
            catch (Exception exception) { ApplicationDiagnosticLog.Write4(logger, exception, nameof(MarketDataOperationsHealthObserver), nameof(ExecuteAsync)); }
            await Task.Delay(TimeSpan.FromSeconds(15), time, stoppingToken).ConfigureAwait(false);
        }
    }
}
