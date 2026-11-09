using Xunit;

namespace TomasAI.IFM.Framework.Caching.UnitTests;

public sealed class BoundedAsyncCacheTests
{
    [Fact]
    public async Task ConcurrentMissesShareOneLoadAndWarmValuesAvoidDatabase()
    {
        using var cache = new BoundedAsyncCache<string, long>();
        var release = new TaskCompletionSource<long>(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        Task<long> Load(string _, CancellationToken token) { Interlocked.Increment(ref calls); return release.Task.WaitAsync(token); }
        var tasks = Enumerable.Range(0, 32).Select(_ => cache.GetAsync("stream", Load).AsTask()).ToArray();
        Assert.Equal(1, calls);
        release.SetResult(42);
        Assert.All(await Task.WhenAll(tasks), value => Assert.Equal(42, value));
        Assert.Equal(42, await cache.GetAsync("stream", Load));
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task CancellingOneWaiterDoesNotCancelTheSharedLoad()
    {
        using var cache = new BoundedAsyncCache<string, long>();
        using var cancelled = new CancellationTokenSource();
        var release = new TaskCompletionSource<long>(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = cache.GetAsync("stream", (_, token) => release.Task.WaitAsync(token), cancelled.Token).AsTask();
        var second = cache.GetAsync("stream", (_, _) => throw new InvalidOperationException("Duplicate load")).AsTask();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
        release.SetResult(7);
        Assert.Equal(7, await second);
    }

    [Fact]
    public async Task FailedLoadCanRetryAndEvictionDoesNotLoseTheAuthoritativeIdentity()
    {
        using var cache = new BoundedAsyncCache<string, long>(capacity: 1);
        await Assert.ThrowsAsync<InvalidOperationException>(() => cache.GetAsync("a",
            (_, _) => Task.FromException<long>(new InvalidOperationException("Database unavailable"))).AsTask());
        Assert.Equal(11, await cache.GetAsync("a", (_, _) => Task.FromResult(11L)));
        Assert.Equal(22, await cache.GetAsync("b", (_, _) => Task.FromResult(22L)));
        Assert.Equal(1, cache.Count);
        Assert.Equal(11, await cache.GetAsync("a", (_, _) => Task.FromResult(11L)));
    }

    [Fact]
    public async Task InvalidationPreventsAnOldDatabaseLoadFromRepopulatingTheCache()
    {
        using var cache = new BoundedAsyncCache<string, long>();
        var oldDatabase = new TaskCompletionSource<long>(TaskCreationOptions.RunContinuationsAsynchronously);
        var old = cache.GetAsync("stream", (_, token) => oldDatabase.Task.WaitAsync(token)).AsTask();
        cache.Clear();
        Assert.Equal(200, await cache.GetAsync("stream", (_, _) => Task.FromResult(200L)));
        oldDatabase.SetResult(100);
        Assert.Equal(100, await old);
        Assert.Equal(200, await cache.GetAsync("stream", (_, _) => throw new InvalidOperationException("Unexpected reload")));
    }

    [Fact]
    public async Task DistinctKeysHaveBoundedDatabaseConcurrency()
    {
        using var cache = new BoundedAsyncCache<int, int>(capacity: 32, maximumConcurrentLoads: 2);
        var entered = 0;
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var bothEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var requests = Enumerable.Range(0, 8).Select(key => cache.GetAsync(key, async (id, token) =>
        {
            if (Interlocked.Increment(ref entered) == 2) bothEntered.TrySetResult();
            await release.Task.WaitAsync(token);
            return id;
        }).AsTask()).ToArray();
        await bothEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(2, entered);
        release.SetResult();
        Assert.Equal(Enumerable.Range(0, 8), await Task.WhenAll(requests));
    }

    [Fact]
    public async Task ShutdownCancelsLoadsAndRejectsFurtherUse()
    {
        var cache = new BoundedAsyncCache<string, int>();
        var request = cache.GetAsync("a", async (_, token) => { await Task.Delay(Timeout.Infinite, token); return 1; }).AsTask();
        cache.Dispose();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => request);
        Assert.Throws<ObjectDisposedException>(() => cache.GetAsync("a", (_, _) => Task.FromResult(1)));
    }
}
