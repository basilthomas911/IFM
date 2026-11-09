namespace TomasAI.IFM.Framework.Caching;

/// <summary>Stores disposable typed values and coalesces bounded asynchronous loads per key.</summary>
/// <typeparam name="TKey">The complete identity within this cache owner's database scope.</typeparam>
/// <typeparam name="TValue">An immutable published value.</typeparam>
public sealed class BoundedAsyncCache<TKey, TValue> : IDisposable where TKey : notnull
{
    readonly System.Threading.Lock gate = new();
    readonly Dictionary<TKey, TValue> values = new();
    readonly Queue<TKey> insertionOrder = new();
    readonly Dictionary<TKey, TaskCompletionSource<TValue>> pending = new();
    readonly SemaphoreSlim loads;
    readonly CancellationTokenSource lifetime = new();
    readonly int capacity;
    long generation;
    long hits;
    long misses;
    long evictions;
    long failures;
    bool disposed;

    /// <summary>Creates a cache with explicit retained-value and concurrent-database-load bounds.</summary>
    public BoundedAsyncCache(int capacity = 32_768, int maximumConcurrentLoads = 16)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumConcurrentLoads);
        this.capacity = capacity;
        loads = new(maximumConcurrentLoads, maximumConcurrentLoads);
    }

    /// <summary>Gets the number of retained values, excluding pending loads.</summary>
    public int Count { get { lock (gate) return values.Count; } }

    /// <summary>Reads cache counters without exposing mutable entries or database identities.</summary>
    public (long Hits, long Misses, long Evictions, long Failures, int Entries, int Pending) Statistics
    {
        get { lock (gate) return (hits, misses, evictions, failures, values.Count, pending.Count); }
    }

    /// <summary>Returns a warm value or shares a load; caller cancellation does not cancel other waiters.</summary>
    public ValueTask<TValue> GetAsync(TKey key, Func<TKey, CancellationToken, Task<TValue>> loader,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(loader);
        cancellationToken.ThrowIfCancellationRequested();
        TaskCompletionSource<TValue> completion;
        long loadGeneration;
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (values.TryGetValue(key, out var value)) { hits++; return ValueTask.FromResult(value); }
            misses++;
            if (pending.TryGetValue(key, out completion!))
                return new(completion.Task.WaitAsync(cancellationToken));
            if (pending.Count >= capacity)
                throw new InvalidOperationException("Typed cache pending-load capacity is exhausted.");
            completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
            pending.Add(key, completion);
            loadGeneration = generation;
        }
        _ = LoadAsync(key, loader, completion, loadGeneration);
        return new(completion.Task.WaitAsync(cancellationToken));
    }

    /// <summary>Discards values and prevents in-flight loads from republishing into the new generation.</summary>
    public void Clear()
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            generation++;
            values.Clear();
            insertionOrder.Clear();
            pending.Clear();
        }
    }

    async Task LoadAsync(TKey key, Func<TKey, CancellationToken, Task<TValue>> loader,
        TaskCompletionSource<TValue> completion, long loadGeneration)
    {
        try
        {
            await loads.WaitAsync(lifetime.Token).ConfigureAwait(false);
            TValue value;
            try { value = await loader(key, lifetime.Token).ConfigureAwait(false); }
            finally { loads.Release(); }
            lock (gate)
            {
                if (!disposed && generation == loadGeneration)
                {
                    if (values.Count >= capacity && insertionOrder.TryDequeue(out var oldest)) { values.Remove(oldest); evictions++; }
                    values[key] = value;
                    insertionOrder.Enqueue(key);
                }
            }
            completion.TrySetResult(value);
        }
        catch (OperationCanceledException exception)
        {
            RemovePending(key, completion);
            completion.TrySetCanceled(exception.CancellationToken);
        }
        catch (Exception exception)
        {
            lock (gate) failures++;
            RemovePending(key, completion);
            completion.TrySetException(exception);
        }
        finally
        {
            RemovePending(key, completion);
        }
    }

    void RemovePending(TKey key, TaskCompletionSource<TValue> completion)
    {
        lock (gate)
            if (pending.TryGetValue(key, out var current) && ReferenceEquals(current, completion)) pending.Remove(key);
    }

    /// <summary>Cancels outstanding database work when this cache owner shuts down.</summary>
    public void Dispose()
    {
        lock (gate)
        {
            if (disposed) return;
            disposed = true;
            values.Clear();
            insertionOrder.Clear();
        }
        lifetime.Cancel();
        // Loads can still be unwinding; disposing their semaphore/token source here would race them.
    }
}
