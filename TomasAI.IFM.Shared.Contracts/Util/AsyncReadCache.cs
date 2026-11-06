namespace TomasAI.IFM.Shared.Util;

/// <summary>Coalesces bounded reads of versioned data; failures and missing values are never retained.</summary>
public sealed class AsyncReadCache<TKey, TValue> where TKey : notnull where TValue : class
{
    readonly System.Threading.Lock gate = new();
    readonly Dictionary<TKey, Entry> entries = new();
    readonly int capacity;
    readonly TimeSpan lifetime;
    readonly TimeProvider clock;
    sealed record Entry(DateTimeOffset Expires, Lazy<Task<TValue?>> Value);

    /// <summary>Sets the maximum retained entries and absolute metadata lifetime.</summary>
    public AsyncReadCache(int capacity, TimeSpan lifetime, TimeProvider? clock = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(lifetime, TimeSpan.Zero);
        this.capacity = capacity;
        this.lifetime = lifetime;
        this.clock = clock ?? TimeProvider.System;
    }

    /// <summary>Reads a cached value. Cancelling one waiter does not cancel the shared, time-bounded read.</summary>
    public Task<TValue?> GetAsync(TKey key, Func<CancellationToken, Task<TValue?>> read,
        CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        Entry entry;
        lock (gate)
        {
            var now = clock.GetUtcNow();
            if (!entries.TryGetValue(key, out entry!) || entry.Expires <= now)
            {
                if (entries.Count >= capacity)
                    entries.Remove(entries.MinBy(x => x.Value.Expires).Key);
                Entry? created = null;
                created = new(now + lifetime, new(() => LoadAsync(key, created!, read),
                    LazyThreadSafetyMode.ExecutionAndPublication));
                entries[key] = entry = created;
            }
        }
        return entry.Value.Value.WaitAsync(token);
    }

    /// <summary>Reads a completed immutable value without starting or waiting for a load.</summary>
    public bool TryGetCompleted(TKey key, out TValue? value)
    {
        lock (gate)
        {
            if (entries.TryGetValue(key, out var entry) && entry.Expires > clock.GetUtcNow()
                && entry.Value.IsValueCreated && entry.Value.Value.IsCompletedSuccessfully)
            {
                value = entry.Value.Value.Result;
                return value is not null;
            }
        }
        value = null;
        return false;
    }

    /// <summary>Seeds a successful value from a set-based read without replacing an active load.</summary>
    public void RememberCompleted(TKey key, TValue value)
    {
        ArgumentNullException.ThrowIfNull(value);
        lock (gate)
        {
            var now = clock.GetUtcNow();
            if (entries.TryGetValue(key, out var existing) && existing.Expires > now)
                return;
            if (entries.Count >= capacity)
                entries.Remove(entries.MinBy(x => x.Value.Expires).Key);
            var loaded = new Lazy<Task<TValue?>>(() => Task.FromResult<TValue?>(value));
            _ = loaded.Value;
            entries[key] = new(now + lifetime, loaded);
        }
    }

    async Task<TValue?> LoadAsync(TKey key, Entry entry, Func<CancellationToken, Task<TValue?>> read)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            var value = await read(deadline.Token).ConfigureAwait(false);
            if (value is null) Remove(key, entry);
            return value;
        }
        catch { Remove(key, entry); throw; }
    }

    void Remove(TKey key, Entry entry)
    {
        lock (gate)
            if (entries.TryGetValue(key, out var current) && ReferenceEquals(current, entry)) entries.Remove(key);
    }
}
