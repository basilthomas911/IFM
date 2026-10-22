using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Application.MarketData.MarketOutlook;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Framework.MarketData.Contracts.TickAggregation;
namespace TomasAI.IFM.Application.MarketData.OperationsHealth;
/// <summary>Projects remote worker observations into the existing operations screen without owning recovery.</summary>
public sealed class MarketDataBackendHealthObserver(IMarketDataApi api, MarketDataOperationsHealthService health,
    IMarketOutlookOperations outlook, IFuturesMarketSessionAuthority sessions, ITickAggregationEventPublisher publisher,
    TimeProvider time, ILogger<MarketDataBackendHealthObserver> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken); deadline.CancelAfter(TimeSpan.FromSeconds(3));
                var status = await api.GetBackendStatusAsync(deadline.Token).ConfigureAwait(false);
                health.ObserveRuntime(sessions.Current, new DatabentoLifecycleSnapshot
                {
                    State = status.Datasets.All(x => x.Healthy) ? DatabentoLifecycleState.Healthy : DatabentoLifecycleState.Degraded,
                    StateRevision = status.Revision, ValueDate = status.ValueDate, CorrelationId = status.HostInstanceId,
                    NativeGeneration = Guid.Empty, RecoveryAttempt = 0, Reason = "Recovery owned by MarketData Host.", ChangedOnUtc = time.GetUtcNow().UtcDateTime
                }, status.WorkerObservations?.Select(Databento.Workers.MarketDataHostConnection.Decode<Databento.Workers.DatasetWorkerProcessSnapshot>).ToArray() ?? [], outlook.GetMetrics(), (publisher as ITickAggregationPublisherDiagnostics)?.GetSnapshot(), new() { Enabled = true });
            }
            catch (Exception error) when (!stoppingToken.IsCancellationRequested) { logger.LogWarning(error, "MarketData Host operations observation unavailable."); }
            await Task.Delay(TimeSpan.FromSeconds(15), time, stoppingToken).ConfigureAwait(false);
        }
    }
}
