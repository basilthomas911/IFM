using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Shared.EventModelActor;

/// <summary>
/// Fixed asynchronous worker pool backed by a shared ready-mailbox queue.
/// </summary>
public sealed class ActorThreadPoolV2(
    IActorSupervisor supervisor,
    ILogger logger) : IActorThreadPool, IAsyncDisposable
{
    readonly IActorSupervisor _supervisor = IsArgumentNull.Set(supervisor);
    readonly ILogger _logger = IsArgumentNull.Set(logger);
    readonly ActorReadyQueue _readyQueue = new();
    readonly ActorThreadPoolMetricsState _metricsState = new();
    ActorThreadV2[] _workers = [];
    int _initialized;
    int _disposed;

    public IActorThreadPool Initialize(int initialThreadCount)
    {
        if (initialThreadCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(initialThreadCount));
        if (Interlocked.CompareExchange(ref _initialized, 1, 0) != 0)
            throw new InvalidOperationException("The actor thread pool has already been initialized.");

        var workers = new ActorThreadV2[initialThreadCount];
        for (var index = 0; index < workers.Length; index++)
        {
            var worker = new ActorThreadV2(_supervisor, _logger, _readyQueue, _metricsState);
            worker.Start();
            workers[index] = worker;
        }
        Volatile.Write(ref _workers, workers);
        ActorRuntimeMetrics.RegisterWorkerPool(workers.Length);
        return this;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IActorThread GetThread(ActorThreadId threadId)
    {
        IsArgumentNull.Check(threadId);
        ThrowIfUnavailable();
        if (!_supervisor.Children.ContainsKey(threadId.MailboxId))
            throw new KeyNotFoundException($"Actor with mailbox id '{threadId.MailboxId}' not found in context.");

        var workers = Volatile.Read(ref _workers);
        var hash = (uint)threadId.GetHashCode();
        return workers[hash % (uint)workers.Length];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<IActorThread> GetThreadAsync(ActorThreadId threadId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return ValueTask.FromResult(GetThread(threadId));
    }

    // Mailbox execution is released by its atomic scheduling state; workers themselves are never leased.
    public void ReleaseThread(ActorThreadId threadId) => IsArgumentNull.Check(threadId);

    public int Count => Volatile.Read(ref _workers).Length;

    public void BeginDrainMeasurement() => _metricsState.BeginDrainMeasurement();

    public long DrainedMessageCount => _metricsState.DrainedMessageCount;

    /// <summary>
    /// Waits until no mailbox is scheduled or executing for a continuous quiet period.
    /// The timeout is definitive so shutdown cannot wait forever for derivative actor work.
    /// </summary>
    public async ValueTask<bool> WaitForIdleAsync(
        TimeSpan quietPeriod,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        if (quietPeriod < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(quietPeriod));
        if (timeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeout));

        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        long? idleStarted = null;
        while (System.Diagnostics.Stopwatch.GetElapsedTime(started) < timeout)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_readyQueue.ScheduledCount == 0 && _metricsState.ActiveMailboxCount == 0)
            {
                idleStarted ??= System.Diagnostics.Stopwatch.GetTimestamp();
                if (System.Diagnostics.Stopwatch.GetElapsedTime(idleStarted.Value) >= quietPeriod)
                    return true;
            }
            else
            {
                idleStarted = null;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(10), cancellationToken).ConfigureAwait(false);
        }

        return false;
    }

    /// <summary>Immediately cancels workers after a bounded graceful-drain attempt has expired.</summary>
    public async ValueTask StopAsync()
    {
        _readyQueue.Complete();
        var workers = Volatile.Read(ref _workers);
        foreach (var worker in workers)
            worker.Stop();
        foreach (var worker in workers)
            await worker.Completion.ConfigureAwait(false);
    }

    void ThrowIfUnavailable()
    {
        if (Volatile.Read(ref _initialized) == 0)
            throw new InvalidOperationException("The actor thread pool has not been initialized.");
        if (Volatile.Read(ref _disposed) != 0)
            throw new ObjectDisposedException(nameof(ActorThreadPoolV2));
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        _readyQueue.Complete();
        var workers = Volatile.Read(ref _workers);
        foreach (var worker in workers)
            await worker.Completion.ConfigureAwait(false);
        foreach (var worker in workers)
            await worker.DisposeAsync().ConfigureAwait(false);

        if (workers.Length != 0)
            ActorRuntimeMetrics.UnregisterWorkerPool(workers.Length);
        Volatile.Write(ref _workers, []);
        GC.SuppressFinalize(this);
    }
}

sealed class ActorThreadPoolMetricsState
{
    long _drainedMessageCount;
    int _activeMailboxCount;
    int _measureDrain;

    public long DrainedMessageCount => Volatile.Read(ref _drainedMessageCount);
    public int ActiveMailboxCount => Math.Max(0, Volatile.Read(ref _activeMailboxCount));

    public void RecordMailboxStarted() => Interlocked.Increment(ref _activeMailboxCount);

    public void RecordMailboxCompleted() => Interlocked.Decrement(ref _activeMailboxCount);

    public void BeginDrainMeasurement()
    {
        Interlocked.Exchange(ref _drainedMessageCount, 0);
        Volatile.Write(ref _measureDrain, 1);
    }

    public void RecordMessageCompleted()
    {
        if (Volatile.Read(ref _measureDrain) != 0)
            Interlocked.Increment(ref _drainedMessageCount);
    }
}
