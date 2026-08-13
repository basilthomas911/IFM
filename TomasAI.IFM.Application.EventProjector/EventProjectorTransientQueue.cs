using Microsoft.Extensions.Logging;
using System.Threading.Channels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.EventProjector;

internal interface IEventProjectorTransientQueue
{
    ValueTask StartAsync(
        Func<IEvent, CancellationToken, ValueTask> handler,
        CancellationToken cancellationToken = default);

    ValueTask EnqueueAsync(IEvent domainEvent, CancellationToken cancellationToken = default);

    ValueTask StopAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Projector-scoped, process-local queue for descriptors that explicitly opt out of durable replay.
/// </summary>
internal sealed class EventProjectorTransientQueue(
    string projectorName,
    int capacity,
    ILogger logger) : IEventProjectorTransientQueue
{
    readonly string _projectorName = string.IsNullOrWhiteSpace(projectorName)
        ? throw new ArgumentException("The projector name is required.", nameof(projectorName))
        : projectorName;
    readonly int _capacity = capacity > 0
        ? capacity
        : throw new ArgumentOutOfRangeException(nameof(capacity));
    readonly ILogger _logger = logger
        ?? throw new ArgumentNullException(nameof(logger));
    readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    Channel<IEvent>? _channel;
    CancellationTokenSource? _workerCancellation;
    Task? _worker;

    public async ValueTask StartAsync(
        Func<IEvent, CancellationToken, ValueTask> handler,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(handler);
        cancellationToken.ThrowIfCancellationRequested();
        await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_worker is { IsCompleted: false })
                return;

            _workerCancellation?.Dispose();
            _workerCancellation = new CancellationTokenSource();
            _channel = Channel.CreateBounded<IEvent>(new BoundedChannelOptions(_capacity)
            {
                SingleReader = true,
                SingleWriter = false,
                FullMode = BoundedChannelFullMode.Wait,
                AllowSynchronousContinuations = false
            });
            _worker = RunAsync(_channel.Reader, handler, _workerCancellation.Token);
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    public async ValueTask EnqueueAsync(
        IEvent domainEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        var channel = Volatile.Read(ref _channel)
            ?? throw new InvalidOperationException(
                $"The non-durable queue for projector '{_projectorName}' has not been started.");
        try
        {
            await channel.Writer.WriteAsync(domainEvent, cancellationToken).ConfigureAwait(false);
        }
        catch (ChannelClosedException ex)
        {
            throw new InvalidOperationException(
                $"The non-durable queue for projector '{_projectorName}' is not accepting events.", ex);
        }
    }

    public async ValueTask StopAsync(CancellationToken cancellationToken = default)
    {
        await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var channel = _channel;
            var worker = _worker;
            if (channel is null || worker is null)
                return;

            channel.Writer.TryComplete();
            try
            {
                await worker.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                _workerCancellation?.Cancel();
                var abandoned = channel.Reader.CanCount ? channel.Reader.Count : 0;
                EventProjectorMetrics.RecordEvent(_projectorName, "abandoned-on-shutdown", "transient");
                _logger.LogWarning(
                    "Abandoned {EventCount} queued non-durable events while stopping projector {ProjectorName}.",
                    abandoned,
                    _projectorName);
                throw;
            }

            _channel = null;
            _worker = null;
            _workerCancellation.Dispose();
            _workerCancellation = null;
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    async Task RunAsync(
        ChannelReader<IEvent> reader,
        Func<IEvent, CancellationToken, ValueTask> handler,
        CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var domainEvent in reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                try
                {
                    await handler(domainEvent, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    EventProjectorMetrics.RecordEvent(_projectorName, "worker-failed", "transient");
                    _logger.LogError(
                        ex,
                        "Unhandled non-durable projection failure for event {EventId} in projector {ProjectorName}.",
                        domainEvent.EventId,
                        _projectorName);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }
}
