using System;
using System.Threading;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.Util;
using Xunit;

namespace TomasAI.IFM.Shared.UnitTests.Util;

public sealed class AsyncReadCacheTests
{
    [Fact]
    public async Task Concurrent_waiters_share_read_and_one_cancellation_does_not_cancel_others()
    {
        var cache = new AsyncReadCache<string, string>(4, TimeSpan.FromMinutes(1));
        var completion = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var reads = 0;
        Task<string?> Read(CancellationToken _) { Interlocked.Increment(ref reads); return completion.Task; }
        using var cancellation = new CancellationTokenSource();
        var first = cache.GetAsync("v1", Read, cancellation.Token);
        var second = cache.GetAsync("v1", Read);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
        completion.SetResult("published");
        Assert.Equal("published", await second);
        Assert.Equal("published", await cache.GetAsync("v1", Read));
        Assert.Equal(1, reads);
    }

    [Fact]
    public async Task Failure_and_missing_values_are_evicted()
    {
        var cache = new AsyncReadCache<string, string>(2, TimeSpan.FromMinutes(1));
        await Assert.ThrowsAsync<InvalidOperationException>(() => cache.GetAsync("v1", _ => Task.FromException<string?>(new InvalidOperationException())));
        Assert.Null(await cache.GetAsync("v1", _ => Task.FromResult<string?>(null)));
        Assert.Equal("ready", await cache.GetAsync("v1", _ => Task.FromResult<string?>("ready")));
    }

    [Fact]
    public async Task Capacity_expiry_and_version_changes_require_new_reads()
    {
        var time = new Clock();
        var cache = new AsyncReadCache<string, string>(1, TimeSpan.FromMinutes(1), time);
        var reads = 0;
        Task<string?> Read(CancellationToken _) => Task.FromResult<string?>((++reads).ToString());
        Assert.Equal("1", await cache.GetAsync("v1", Read));
        Assert.Equal("2", await cache.GetAsync("v2", Read));
        Assert.Equal("3", await cache.GetAsync("v1", Read));
        time.Now = time.Now.AddMinutes(2);
        Assert.Equal("4", await cache.GetAsync("v1", Read));
    }
    sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
