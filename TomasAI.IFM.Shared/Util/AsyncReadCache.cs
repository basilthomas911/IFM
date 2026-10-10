namespace TomasAI.IFM.Shared.Util;

/// <summary>Coalesces bounded reads of versioned data; failures and missing values are never retained.</summary>
public sealed class AsyncReadCache<TKey, TValue> where TKey : notnull where TValue : class
{
    readonly object gate = new();
    readonly Dictionary<TKey, Entry> entries = new();
    readonly int capacity;
    readonly TimeSpan lifetime;
    readonly TimeProvider clock;
    sealed record Entry(DateTimeOffset Expires, Lazy<Task<TValue?>> Value);

    /// <summary>Sets the maximum retained entries and absolute metadata lifetime.</summary>
    public AsyncReadCache(int capacity, TimeSpan lifetime, TimeProvider? clock = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        if (lifetime <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(lifetime));
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
