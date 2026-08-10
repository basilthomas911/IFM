using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventProjector;
using TomasAI.IFM.Shared.EventProjector.ReadModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.EventProjector;

/// <summary>
/// Claims and publishes bounded projector outbox batches. Ambiguous delivery is retried with the same event ID and
/// message identity; a lease prevents a stalled dispatcher from owning a record indefinitely.
/// </summary>
internal sealed class EventProjectorOutboxDispatcher(
    IEventSourceActorDbContext eventSource,
    EventProjectorReliabilityOptions options,
    string projectorName,
    Func<IEvent, CancellationToken, ValueTask> publishAsync,
    ILogger logger) : IAsyncDisposable
{
    readonly IEventSourceActorDbContext _eventSource = eventSource ?? throw new ArgumentNullException(nameof(eventSource));
    readonly EventProjectorReliabilityOptions _options = (options ?? throw new ArgumentNullException(nameof(options))).Validate();
    readonly string _projectorName = RequireName(projectorName);
    readonly Func<IEvent, CancellationToken, ValueTask> _publishAsync = publishAsync ?? throw new ArgumentNullException(nameof(publishAsync));
    readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    readonly SemaphoreSlim _signal = new(0, 1);
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
        Signal();
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

    public void Signal()
    {
        try
        {
            if (_signal.CurrentCount == 0)
                _signal.Release();
        }
        catch (SemaphoreFullException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
    }

    async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                while (await DispatchBatchAsync(cancellationToken).ConfigureAwait(false) == _options.OutboxBatchSize)
                {
                }
                await _signal.WaitAsync(_options.OutboxPollingInterval, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Projector outbox dispatch failed for {ProjectorName}; polling will continue.", _projectorName);
                await Task.Delay(_options.OutboxPollingInterval, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    internal async Task<int> DispatchBatchAsync(CancellationToken cancellationToken = default)
    {
        var nowUtc = DateTime.UtcNow;
        var dispatchToken = Guid.NewGuid();
        var messages = await _eventSource.ClaimEventProjectorOutboxAsync(
            _projectorName,
            dispatchToken,
            nowUtc,
            _options.OutboxDispatchLeaseDuration,
            _options.OutboxBatchSize,
            cancellationToken).ConfigureAwait(false);
        foreach (var message in messages)
            await DispatchAsync(message, cancellationToken).ConfigureAwait(false);
        return messages.Count;
    }

    async Task DispatchAsync(EventProjectorOutboxReadModel message, CancellationToken cancellationToken)
    {
        try
        {
            var domainEvent = EventProjectorOutboxSerializer.Deserialize(message.EventTypeName, message.EventPayload);
            await _publishAsync(domainEvent, cancellationToken).ConfigureAwait(false);
            var publishedAtUtc = DateTime.UtcNow;
            if (!await _eventSource.MarkEventProjectorOutboxPublishedAsync(
                    message,
                    publishedAtUtc,
                    cancellationToken).ConfigureAwait(false))
            {
                _logger.LogWarning(
                    "Projector outbox delivery marker lost its lease for {MessageId}; safe re-publication may occur.",
                    message.MessageId);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            var nowUtc = DateTime.UtcNow;
            var terminal = message.AttemptCount >= _options.MaximumOutboxAttempts;
            DateTime? nextAttemptAtUtc = terminal ? null : nowUtc.Add(GetRetryDelay(message.AttemptCount));
            _ = await _eventSource.ReleaseEventProjectorOutboxAsync(
                message,
                terminal ? EventProjectorOutboxStatus.Failed : EventProjectorOutboxStatus.Retrying,
                nextAttemptAtUtc,
                ex.ToString(),
                nowUtc,
                CancellationToken.None).ConfigureAwait(false);
            _logger.LogWarning(
                ex,
                "Projector outbox publication {MessageId} failed on attempt {AttemptCount}; terminal={Terminal}.",
                message.MessageId,
                message.AttemptCount,
                terminal);
        }
    }

    TimeSpan GetRetryDelay(int attemptCount)
    {
        var exponent = Math.Clamp(attemptCount - 1, 0, 6);
        var ticks = _options.InitialReplayDelay.Ticks * (1L << exponent);
        return TimeSpan.FromTicks(Math.Min(ticks, TimeSpan.FromMinutes(2).Ticks));
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
        _signal.Dispose();
    }

    static string RequireName(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return value;
    }
}
