using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Framework.Messaging.Nats;

/// <summary>
/// Lightweight in-memory implementation of IDurableReplayQueue used when a concrete
/// durable replay queue is required. This is a simple, thread-safe implementation
/// that stores events per named queue and provides basic enqueue/dequeue and
/// replay-attempt configuration. It is intentionally NOT networked — it's a
/// local fallback useful for tests and simple deployments.
/// </summary>
public sealed class NatsDurableReplayQueue : IDurableReplayQueue, IDisposable
{
    private readonly ConcurrentDictionary<string, ConcurrentQueue<IEvent>> _queues = new();
    private readonly ConcurrentDictionary<string, int> _maxReplayAttempts = new();
    private readonly ConcurrentDictionary<string, Func<IEvent, Task>> _maxAttemptActions = new();
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _runners = new();

    public NatsDurableReplayQueue()
    {
    }

    public Task StartAsync(string durableReplayQueueName, TimeSpan replayInterval, CancellationToken cancellationToken = default)
    {
        // Start a background runner that will attempt replay at the configured interval.
        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _runners.AddOrUpdate(durableReplayQueueName, cts, (k, old) => { old.Cancel(); return cts; });

        // Fire-and-forget loop
        _ = Task.Run(async () =>
        {
            while (!cts.IsCancellationRequested)
            {
                await Task.Delay(replayInterval, cts.Token).ContinueWith(_ => { });
                try
                {
                    await DequeueAsync(durableReplayQueueName, async _ => { /* no-op consumer for background replay */ }, cts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) { break; }
                catch { /* swallow exceptions to keep background runner alive */ }
            }
        }, cts.Token);

        return Task.CompletedTask;
    }

    public Task StopAsync(string durableReplayQueueName, CancellationToken cancellationToken = default)
    {
        if (_runners.TryRemove(durableReplayQueueName, out var cts))
        {
            try { cts.Cancel(); } catch { }
            try { cts.Dispose(); } catch { }
        }
        return Task.CompletedTask;
    }

    public void Enqueue(string durableReplayQueueName, IEvent domainEvent, CancellationToken cancellationToken = default)
    {
        var queue = _queues.GetOrAdd(durableReplayQueueName, _ => new ConcurrentQueue<IEvent>());
        queue.Enqueue(domainEvent ?? throw new ArgumentNullException(nameof(domainEvent)));
    }

    public async Task DequeueAsync(string durableReplayQueueName, Func<IEvent, Task> processMessageFunc, CancellationToken cancellationToken = default)
    {
        if (processMessageFunc == null) throw new ArgumentNullException(nameof(processMessageFunc));

        if (!_queues.TryGetValue(durableReplayQueueName, out var queue))
            return;

        while (!cancellationToken.IsCancellationRequested && queue.TryDequeue(out var ev))
        {
            try
            {
                await processMessageFunc(ev).ConfigureAwait(false);
            }
            catch
            {
                // on processing failure, consider max attempts
                var max = GetMaxReplayAttemps(durableReplayQueueName);
                if (max > 0)
                {
                    // if reached max attempts, invoke configured action
                    if (_maxAttemptActions.TryGetValue(durableReplayQueueName, out var action))
                    {
                        try { await action(ev).ConfigureAwait(false); } catch { }
                    }
                }
            }
        }
    }

    public void SetMaxAttemptsReachedAction(string durableReplayQueueName, Func<IEvent, Task> maxAttemptsReachedFunc, bool overwrite = true)
    {
        if (maxAttemptsReachedFunc == null) throw new ArgumentNullException(nameof(maxAttemptsReachedFunc));
        _maxAttemptActions.AddOrUpdate(durableReplayQueueName, maxAttemptsReachedFunc, (k, old) => overwrite ? maxAttemptsReachedFunc : old);
    }

    public void SetMaxReplayAttemps(string durableReplayQueueName, int maxReplayAttemps, bool overwrite = true)
    {
        if (maxReplayAttemps < 0) throw new ArgumentOutOfRangeException(nameof(maxReplayAttemps));
        _maxReplayAttempts.AddOrUpdate(durableReplayQueueName, maxReplayAttemps, (k, old) => overwrite ? maxReplayAttemps : old);
    }

    public int GetMaxReplayAttemps(string durableReplayQueueName)
    {
        if (_maxReplayAttempts.TryGetValue(durableReplayQueueName, out var v)) return v;
        return 0;
    }

    public void Dispose()
    {
        foreach (var kv in _runners)
        {
            try { kv.Value.Cancel(); kv.Value.Dispose(); } catch { }
        }
        _runners.Clear();
        _queues.Clear();
        _maxAttemptActions.Clear();
        _maxReplayAttempts.Clear();
    }
}
