using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace TomasAI.IFM.Application.Api.Server.Core.Startup.Application;

/// <summary>Shares only active work. A waiter's cancellation never owns the shared operation's lifetime.</summary>
internal sealed class BoundedInFlight<TKey, TValue>(int maximumKeys, TimeSpan budget, CancellationToken lifetime = default)
    where TKey : notnull
{
    readonly object gate = new();
    readonly Dictionary<TKey, TaskCompletionSource<TValue>> pending = [];
    static readonly Meter FlightMeter = new("TomasAI.IFM.Startup.InFlight", "1.0");
    static readonly Histogram<double> Wait = FlightMeter.CreateHistogram<double>("startup.inflight.wait", "ms");

    public Task<TValue> RunAsync(TKey key, Func<CancellationToken, Task<TValue>> operation, CancellationToken waiter)
    {
        waiter.ThrowIfCancellationRequested();
        TaskCompletionSource<TValue> entry;
        var owner = false;
        lock (gate)
        {
            if (!pending.TryGetValue(key, out entry!))
            {
                if (pending.Count >= maximumKeys) throw new InvalidOperationException("Independent initialization capacity is exhausted.");
                entry = new(TaskCreationOptions.RunContinuationsAsynchronously);
                pending.Add(key, entry);
                owner = true;
            }
        }
        if (owner) _ = ExecuteAsync(key, entry, operation);
        return AwaitAsync(entry.Task, waiter);
    }

    static async Task<TValue> AwaitAsync(Task<TValue> task, CancellationToken token)
    {
        var started = Stopwatch.GetTimestamp();
        try { return await task.WaitAsync(token).ConfigureAwait(false); }
        finally { Wait.Record(Stopwatch.GetElapsedTime(started).TotalMilliseconds,
            new KeyValuePair<string, object?>("kind", typeof(TValue).Name)); }
    }

    async Task ExecuteAsync(TKey key, TaskCompletionSource<TValue> entry, Func<CancellationToken, Task<TValue>> operation)
    {
        TValue result = default!;
        Exception? failure = null;
        try
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(lifetime);
            deadline.CancelAfter(budget);
            // Retain ownership until the operation actually finishes; abandoning it on a timeout
            // would allow a retry to overlap an in-progress mutation that ignores cancellation.
            result = await operation(deadline.Token).ConfigureAwait(false);
        }
        catch (Exception error) { failure = error; }
        finally { lock (gate) { pending.Remove(key); } }
        if (failure is OperationCanceledException cancelled) entry.TrySetCanceled(cancelled.CancellationToken);
        else if (failure is not null) { entry.TrySetException(failure); _ = entry.Task.Exception; }
        else entry.TrySetResult(result);
    }
}
