using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;

namespace TomasAI.IFM.Application.EventProjector;

/// <summary>Samples durable projector and outbox backlog state for observable OpenTelemetry gauges.</summary>
internal sealed class EventProjectorMetricsObserver(
    IEventSourceActorDbContext eventSource,
    EventProjectorReliabilityOptions options,
    string projectorName,
    ILogger logger) : IAsyncDisposable
{
    readonly IEventSourceActorDbContext _eventSource = eventSource;
    readonly EventProjectorReliabilityOptions _options = options.Validate();
    readonly string _projectorName = projectorName;
    readonly ILogger _logger = logger;
    CancellationTokenSource? _stopping;
    Task? _worker;

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_worker is { IsCompleted: false })
            return Task.CompletedTask;
        _stopping?.Dispose();
        _stopping = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _worker = RunAsync(_stopping.Token);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        var stopping = Interlocked.Exchange(ref _stopping, null);
        var worker = Interlocked.Exchange(ref _worker, null);
        if (stopping is null || worker is null)
            return;
        await stopping.CancelAsync().ConfigureAwait(false);
        try
        {
            await worker.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stopping.IsCancellationRequested)
        {
        }
        finally
        {
            stopping.Dispose();
        }
    }

    async Task RunAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(_options.MetricsPollingInterval);
        do
        {
            try
            {
                var nowUtc = DateTime.UtcNow;
                var snapshot = await _eventSource.GetEventProjectorOperationalSnapshotAsync(
                    _projectorName, nowUtc, cancellationToken).ConfigureAwait(false);
                EventProjectorMetrics.UpdateSnapshot(_projectorName, snapshot, nowUtc);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Unable to sample operational metrics for projector {ProjectorName}.", _projectorName);
            }
        }
        while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false));
    }

    public async ValueTask DisposeAsync() => await StopAsync().ConfigureAwait(false);
}
