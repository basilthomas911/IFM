using TomasAI.IFM.Application.Api.Server.Core.Startup.Application;

namespace TomasAI.IFM.Domain.Application.UnitTests;

public sealed class BoundedInFlightTests
{
    [Fact]
    public async Task Same_key_shares_work_different_keys_progress_and_cancelled_waiter_does_not_cancel_owner()
    {
        var work = new BoundedInFlight<string, int>(2, TimeSpan.FromSeconds(10));
        var pending = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        using var cancellation = new CancellationTokenSource();
        var first = work.RunAsync("ES", _ => { Interlocked.Increment(ref calls); return pending.Task; }, cancellation.Token);
        var second = work.RunAsync("ES", _ => throw new InvalidOperationException("must coalesce"), default);
        Assert.Equal(2, await work.RunAsync("VX", _ => Task.FromResult(2), default));
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
        Assert.False(second.IsCompleted);
        pending.SetResult(1);
        Assert.Equal(1, await second);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task Capacity_is_bounded_and_faulted_keys_can_retry()
    {
        var work = new BoundedInFlight<string, int>(1, TimeSpan.FromSeconds(10));
        var pending = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = work.RunAsync("ES", _ => pending.Task, default);
        Assert.Throws<InvalidOperationException>(() => { _ = work.RunAsync("VX", _ => Task.FromResult(2), default); });
        pending.SetException(new IOException("fixture"));
        await Assert.ThrowsAsync<IOException>(() => first);
        Assert.Equal(3, await work.RunAsync("ES", _ => Task.FromResult(3), default));
    }

    [Fact]
    public async Task Timeout_does_not_release_a_mutating_key_before_its_actual_completion()
    {
        var work = new BoundedInFlight<string, int>(1, TimeSpan.FromMilliseconds(10));
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = work.RunAsync("ES", token => { token.Register(() => cancelled.TrySetResult()); return pending.Task; }, default);
        await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var second = work.RunAsync("ES", _ => throw new InvalidOperationException("overlapping mutation"), default);
        pending.SetResult(1);
        Assert.Equal(1, await first); Assert.Equal(1, await second);
    }
}
