using Microsoft.Extensions.Logging.Abstractions;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Realtime;
using TomasAI.IFM.TestSupport;

namespace TomasAI.IFM.Domain.MarketData.Feed.UnitTests.RealtimeDelivery;

[Collection("MarketPriceDeliverySafety")]
public sealed class EodCompletionRecoveryTests
{
    public static IEnumerable<object[]> Cases => new[] { false, true }.SelectMany(workers => new[] { "refused", "throw", "lost-reply" }.Select(mode => new object[] { workers, mode }));
    [Theory, MemberData(nameof(Cases))]
    public async Task Retries_unblock_the_original_source_before_later_contract_work(bool workers, string mode)
    {
        await using var fixture = new EodDeliveryFixture(workers)
        { Fail = mode == "refused", Throw = mode == "throw", ThrowAfterCommit = mode == "lost-reply" };
        await fixture.Start();
        Assert.False(await fixture.Deliver(EodDeliveryFixture.Source(1)));
        Assert.False(await fixture.Deliver(EodDeliveryFixture.Source(2), "other-thread"));
        Assert.Single(fixture.Attempts);
        fixture.Fail = fixture.Throw = fixture.ThrowAfterCommit = false;
        Assert.True(await fixture.Deliver(EodDeliveryFixture.Source(1)));
        Assert.True(await fixture.Deliver(EodDeliveryFixture.Source(2)));
        Assert.Equal(new long[] { 20, 30 }, fixture.Committed.Select(row => row.Volume));
        Assert.Equal(30, fixture.Current.Volume);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task No_successful_settlement_while_projection_is_pending(bool workers)
    {
        await using var fixture = new EodDeliveryFixture(workers) { PauseProjection = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        await fixture.Start(); var work = fixture.Deliver(EodDeliveryFixture.Source(1));
        await fixture.ProjectionStarted.Task.WaitAsync(TimeSpan.FromSeconds(5)); Assert.False(work.IsCompleted);
        fixture.PauseProjection.TrySetResult(); Assert.True(await work);
    }
    [Fact]
    public async Task Worker_retirement_joins_running_effects_and_fails_queued_sources()
    {
        var generation = new TomasAI.IFM.Shared.EventModelActor.RealtimeActorGeneration();
        using var scope = generation.Enter();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var worker = new FuturesEodTradeWorker(8, _ => ValueTask.FromResult(true), NullLogger.Instance);
        var first = worker.EnqueueOperationAsync(async () => { entered.TrySetResult(); await release.Task; calls++; }).AsTask();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var second = worker.EnqueueOperationAsync(() => { calls++; return ValueTask.CompletedTask; }).AsTask();
        generation.Retire();
        Assert.NotSame(first, await Task.WhenAny(first, Task.Delay(100)));
        release.TrySetResult();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => second);
        await worker.StopAsync(); Assert.Equal(1, calls); Assert.Equal(0, worker.Pending);
    }
    [Fact]
    public async Task Shared_lane_refuses_later_work_after_failure_but_allows_the_original_retry()
    {
        var lane = new EodDeliveryLane(); var values = new List<int>();
        await Assert.ThrowsAsync<IOException>(() => lane.ExecuteAsync("A", () => throw new IOException("refusal")).AsTask());
        await Assert.ThrowsAsync<InvalidOperationException>(() => lane.ExecuteAsync("B", () => { values.Add(2); return ValueTask.CompletedTask; }).AsTask());
        await lane.ExecuteAsync("A", () => { values.Add(1); return ValueTask.CompletedTask; });
        await lane.ExecuteAsync("B", () => { values.Add(2); return ValueTask.CompletedTask; });
        Assert.Equal(new[] { 1, 2 }, values); await lane.CloseAsync();
    }
}
