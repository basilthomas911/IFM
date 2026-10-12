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
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2213", Justification = "StopAsync exchanges ownership into a local and disposes it in finally; DisposeAsync also releases any residual field.")]
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
        if (stopping is null)
            return;
        await stopping.CancelAsync().ConfigureAwait(false);
        try
        {
            if (worker is not null) await worker.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stopping.IsCancellationRequested)
        {
            // Expected worker cancellation during host shutdown.
        }
        finally
        {
            stopping.Dispose();
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "Projector boundary records an isolated projection failure or cleanup error; failures must not silently stop unrelated projection processing.")]
    async Task RunAsync(CancellationToken cancellationToken)
    {
        // Projectors are constructed together during startup. Give each one a stable phase within the polling
        // interval so their PostgreSQL snapshot queries do not form a periodic thundering herd.
        var initialDelay = GetInitialDelay(_projectorName, _options.MetricsPollingInterval);
        if (initialDelay > TimeSpan.Zero)
            await Task.Delay(initialDelay, cancellationToken).ConfigureAwait(false);

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
                // Normal shutdown.
                break;
            }
            catch (Exception ex)
            {
                global::TomasAI.IFM.Application.EventProjector.PersistenceDiagnosticLog.Write4(_logger, ex, nameof(EventProjectorMetricsObserver), nameof(RunAsync), _projectorName);
            }
        }
        while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false));
    }

    internal static TimeSpan GetInitialDelay(string projectorName, TimeSpan pollingInterval)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectorName);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(pollingInterval, TimeSpan.Zero);

        // FNV-1a is deterministic across processes, unlike string.GetHashCode(). This keeps a projector's phase
        // stable between runs while distributing independently named projectors across the complete interval.
        const ulong offsetBasis = 14695981039346656037UL;
        const ulong prime = 1099511628211UL;
        var hash = offsetBasis;
        foreach (var character in projectorName)
        {
            hash ^= character;
            hash *= prime;
        }

        return TimeSpan.FromTicks((long)(hash % (ulong)pollingInterval.Ticks));
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
        _stopping?.Dispose();
        _stopping = null;
    }
}
