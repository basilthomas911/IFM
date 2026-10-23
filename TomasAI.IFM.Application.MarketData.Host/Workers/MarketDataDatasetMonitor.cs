using TomasAI.IFM.Application.MarketData.Contracts;

namespace TomasAI.IFM.Application.MarketData.Host;

/// <summary>Probes and recovers each owned dataset independently; it never drains API actor mailboxes.</summary>
public sealed class MarketDataDatasetMonitor(MarketDataWorkerHost host, ILogger<MarketDataDatasetMonitor> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            var candidates = host.GetBackendStatus().Datasets.Where(dataset => dataset.Lifecycle == "Healthy").ToArray();
            await Task.WhenAll(candidates.Select(ProbeAsync)).ConfigureAwait(false);
        }

        async Task ProbeAsync(MarketDataDatasetStatus dataset)
        {
            try
            {
                using var probe = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                probe.CancelAfter(TimeSpan.FromSeconds(10));
                var health = await host.RequireWorker(dataset.Role, dataset.WorkerInstanceId).ProbeHealthAsync(probe.Token)
                    .WaitAsync(probe.Token).ConfigureAwait(false);
                if (health.Healthy) return;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception error) { logger.LogWarning(error, "Dataset probe failed; Role={Role}; Dataset={Dataset}", dataset.Role, dataset.Dataset); }
            try
            {
                if (dataset.Role == "Options") host.ReportOptionProcessingFailure("DatasetProbe", new InvalidOperationException("Option worker health probe failed: " + dataset.Dataset));
                var result = await host.RecoverAsync(new(Guid.NewGuid(), dataset.Role, dataset.Dataset,
                    dataset.WorkerInstanceId, dataset.GenerationId, dataset.ValueDate!.Value, "Dataset health probe failed",
                    ReplaceProcessImmediately: dataset.Role == "Options",
                    DeadlineUtc: DateTimeOffset.UtcNow.AddMinutes(2)), stoppingToken).ConfigureAwait(false);
                if (!result.Recovered) logger.LogError("Dataset stopped after bounded recovery; Role={Role}; Dataset={Dataset}; CleanupConfirmed={CleanupConfirmed}; Error={Error}",
                    dataset.Role, dataset.Dataset, result.CleanupConfirmed, result.Error);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception error) { logger.LogError(error, "Dataset recovery request failed; Role={Role}; Dataset={Dataset}", dataset.Role, dataset.Dataset); }
        }
    }
}
