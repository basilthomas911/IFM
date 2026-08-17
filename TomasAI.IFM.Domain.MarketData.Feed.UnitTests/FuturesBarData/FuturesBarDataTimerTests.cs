using FluentAssertions;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesBarData.Command.Model;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;

namespace TomasAI.IFM.Domain.MarketData.Feed.UnitTests.FuturesBarData;

public sealed class FuturesBarDataTimerTests
{
    [Fact]
    public void DefaultPeriod_IsFifteenSeconds()
        => FuturesBarDataTimer.DefaultPeriod.Should().Be(TimeSpan.FromSeconds(15));

    [Fact]
    public async Task Start_DuplicateEntity_IsIdempotent()
    {
        var timer = new FuturesBarDataTimer(TimeSpan.FromMilliseconds(5));
        var entityId = new FuturesBarDataStreamingId(new DateOnly(2026, 8, 5));
        var firstEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondCalls = 0;

        timer.Start(entityId, () =>
        {
            firstEntered.TrySetResult();
            return ValueTask.CompletedTask;
        }).Should().BeTrue();
        timer.Start(entityId, () =>
        {
            Interlocked.Increment(ref secondCalls);
            return ValueTask.CompletedTask;
        }).Should().BeFalse();

        await firstEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await timer.StopAsync(entityId);

        secondCalls.Should().Be(0);
    }

    [Fact]
    public async Task Callback_DoesNotOverlap_AndStopDrainsInFlightWork()
    {
        var timer = new FuturesBarDataTimer(TimeSpan.FromMilliseconds(5));
        var entityId = new FuturesBarDataStreamingId(new DateOnly(2026, 8, 6));
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var active = 0;
        var maximumActive = 0;
        var calls = 0;

        timer.Start(entityId, async () =>
        {
            var current = Interlocked.Increment(ref active);
            InterlockedExtensions.Max(ref maximumActive, current);
            Interlocked.Increment(ref calls);
            entered.TrySetResult();
            await release.Task;
            Interlocked.Decrement(ref active);
        }).Should().BeTrue();

        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var stopTask = timer.StopAsync(entityId).AsTask();
        stopTask.IsCompleted.Should().BeFalse();
        release.TrySetResult();
        (await stopTask).Should().BeTrue();
        var callsAfterStop = calls;
        await Task.Delay(25);

        maximumActive.Should().Be(1);
        calls.Should().Be(callsAfterStop);
    }

    static class InterlockedExtensions
    {
        public static void Max(ref int location, int value)
        {
            var current = Volatile.Read(ref location);
            while (current < value)
            {
                var observed = Interlocked.CompareExchange(ref location, value, current);
                if (observed == current)
                    return;
                current = observed;
            }
        }
    }
}
