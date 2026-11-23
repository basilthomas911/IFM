using TomasAI.IFM.Application.MarketData.Rust.Host;
using Xunit;

namespace TomasAI.IFM.Application.MarketData.Rust.Host.Tests;

public sealed class NativeSessionStartRequestsTests
{
    static readonly DateOnly Date = new(2026,11,19);

    [Fact]
    public async Task Concurrent_and_completed_retries_keep_the_same_owner()
    {
        var starts = new NativeSessionStartRequests();
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        Task Start() { calls++; return ready.Task; }
        var first = starts.StartAsync(Date,Start,default);
        var second = starts.StartAsync(Date,Start,default);
        ready.SetResult();
        await Task.WhenAll(first,second);
        await starts.StartAsync(Date,Start,default);
        Assert.Equal(1,calls);
    }

    [Fact]
    public async Task Cancelled_observer_does_not_cancel_the_accepted_start()
    {
        var starts = new NativeSessionStartRequests();
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var observer = new CancellationTokenSource();
        var first = starts.StartAsync(Date,()=>ready.Task,observer.Token);
        observer.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>first);
        var joined = starts.StartAsync(Date,()=>throw new Exception("Duplicate start"),default);
        ready.SetResult();
        await joined;
    }

    [Fact]
    public async Task Failed_start_is_not_replaced_by_a_retry_with_a_fresh_budget()
    {
        var starts = new NativeSessionStartRequests();
        var calls = 0;
        Task Start() { calls++; return Task.FromException(new InvalidOperationException("Native recovery expired")); }
        await Assert.ThrowsAsync<InvalidOperationException>(()=>starts.StartAsync(Date,Start,default));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>starts.StartAsync(Date,Start,default));
        Assert.Equal(1,calls);
    }

    [Fact]
    public async Task Completed_stop_or_new_value_date_allows_a_fresh_owner()
    {
        var starts = new NativeSessionStartRequests();
        var calls = 0;
        Task Start() { calls++; return Task.CompletedTask; }
        await starts.StartAsync(Date,Start,default);
        starts.Stopped();
        await starts.StartAsync(Date,Start,default);
        await starts.StartAsync(Date.AddDays(1),Start,default);
        Assert.Equal(3,calls);
    }
}
