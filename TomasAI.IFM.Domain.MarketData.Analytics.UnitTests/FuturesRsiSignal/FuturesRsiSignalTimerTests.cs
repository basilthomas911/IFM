using FluentAssertions;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Event.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.FuturesRsiSignal;

public class FuturesRsiSignalTimerTests
{
    [Fact]
    public async Task StartTimer_DuplicateStart_IsIdempotent()
    {
        var (started, stopped) = CreateEvents();
        var firstTick = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;

        started.StartTimer(_ =>
        {
            Interlocked.Increment(ref calls);
            firstTick.TrySetResult();
            return ValueTask.CompletedTask;
        }, TimeSpan.FromMilliseconds(5)).Should().BeTrue();

        started.StartTimer(_ => ValueTask.CompletedTask, TimeSpan.FromMilliseconds(5)).Should().BeFalse();
        await firstTick.Task.WaitAsync(TimeSpan.FromSeconds(1));
        await stopped.StopTimerAsync();
        var callsAfterStop = Volatile.Read(ref calls);
        await Task.Delay(25);

        calls.Should().Be(callsAfterStop);
    }

    [Fact]
    public async Task StartTimer_SlowCallback_NeverOverlapsForEntity()
    {
        var (started, stopped) = CreateEvents();
        var thirdTick = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var active = 0;
        var maximumActive = 0;
        var calls = 0;

        started.StartTimer(async _ =>
        {
            var current = Interlocked.Increment(ref active);
            UpdateMaximum(ref maximumActive, current);
            if (Interlocked.Increment(ref calls) >= 3)
                thirdTick.TrySetResult();
            await Task.Delay(10);
            Interlocked.Decrement(ref active);
        }, TimeSpan.FromMilliseconds(1)).Should().BeTrue();

        await thirdTick.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await stopped.StopTimerAsync();

        maximumActive.Should().Be(1);

        static void UpdateMaximum(ref int maximum, int candidate)
        {
            var observed = Volatile.Read(ref maximum);
            while (candidate > observed)
            {
                var prior = Interlocked.CompareExchange(ref maximum, candidate, observed);
                if (prior == observed)
                    return;
                observed = prior;
            }
        }
    }

    [Fact]
    public async Task StopTimerAsync_WaitsForInFlightCallback()
    {
        var (started, stopped) = CreateEvents();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        started.StartTimer(async _ =>
        {
            entered.TrySetResult();
            await release.Task;
        }, TimeSpan.FromMilliseconds(1)).Should().BeTrue();

        await entered.Task.WaitAsync(TimeSpan.FromSeconds(1));
        var stopping = stopped.StopTimerAsync().AsTask();
        stopping.IsCompleted.Should().BeFalse();
        release.TrySetResult();

        (await stopping).Should().BeTrue();
    }

    [Fact]
    public async Task TryAcceptSourceSequence_AcceptsOnlyIncreasingValuesForActiveTimer()
    {
        var (started, stopped) = CreateEvents();
        var observed = new TaskCompletionSource<(bool First, bool Duplicate, bool Newer)>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        started.StartTimer(_ =>
        {
            observed.TrySetResult((
                started.TryAcceptSourceSequence(100),
                started.TryAcceptSourceSequence(100),
                started.TryAcceptSourceSequence(101)));
            return ValueTask.CompletedTask;
        }, TimeSpan.FromMilliseconds(5)).Should().BeTrue();

        var result = await observed.Task.WaitAsync(TimeSpan.FromSeconds(1));
        await stopped.StopTimerAsync();

        result.First.Should().BeTrue();
        result.Duplicate.Should().BeFalse();
        result.Newer.Should().BeTrue();
    }

    static (FuturesRsiSignalStartedEvent Started, FuturesRsiSignalStoppedEvent Stopped) CreateEvents()
    {
        var entityId = new FuturesRsiSignalEntityId(
            $"TEST-{Guid.NewGuid():N}",
            new DateOnly(2026, 8, 5),
            TimeFrameType.Daily,
            14);
        return (
            new FuturesRsiSignalStartedEvent { EntityId = entityId, ValueDate = entityId.ValueDate },
            new FuturesRsiSignalStoppedEvent { EntityId = entityId, ValueDate = entityId.ValueDate });
    }
}
