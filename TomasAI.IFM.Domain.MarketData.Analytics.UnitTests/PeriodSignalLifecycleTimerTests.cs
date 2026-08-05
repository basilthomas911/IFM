using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Event.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Event.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesMacdSignal.Event.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests;

public class PeriodSignalLifecycleTimerTests
{
    [Fact]
    public Task MacdStartIsIdempotentAndStopDrainsCallback()
    {
        var id = new FuturesMacdSignalEntityId("ESU6", DateOnly.FromDateTime(DateTime.UtcNow), TimeFrameType.Daily, 12);
        return VerifyAsync(id,
            (callback, period) => new FuturesMacdSignalStartedEvent { EntityId = id }.StartTimer(callback, period),
            () => new FuturesMacdSignalStoppedEvent { EntityId = id }.StopTimerAsync(),
            FuturesMacdSignalTimer.StopAllAsync);
    }

    [Fact]
    public Task AdxStartIsIdempotentAndStopDrainsCallback()
    {
        var id = new FuturesAdxSignalEntityId("ESU6", DateOnly.FromDateTime(DateTime.UtcNow), TimeFrameType.Daily, 14);
        return VerifyAsync(id,
            (callback, period) => new FuturesAdxSignalStartedEvent { EntityId = id }.StartTimer(callback, period),
            () => new FuturesAdxSignalStoppedEvent { EntityId = id }.StopTimerAsync(),
            FuturesAdxSignalTimer.StopAllAsync);
    }

    [Fact]
    public Task AtrStartIsIdempotentAndStopDrainsCallback()
    {
        var id = new FuturesAtrSignalEntityId("ESU6", DateOnly.FromDateTime(DateTime.UtcNow), TimeFrameType.Daily, 14);
        return VerifyAsync(id,
            (callback, period) => new FuturesAtrSignalStartedEvent { EntityId = id }.StartTimer(callback, period),
            () => new FuturesAtrSignalStoppedEvent { EntityId = id }.StopTimerAsync(),
            FuturesAtrSignalTimer.StopAllAsync);
    }

    static async Task VerifyAsync<TEntityId>(
        TEntityId expectedId,
        Func<Func<TEntityId, ValueTask>, TimeSpan, bool> start,
        Func<ValueTask<bool>> stop,
        Func<ValueTask> stopAll)
    {
        var entered = new TaskCompletionSource<TEntityId>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        try
        {
            Assert.True(start(async entityId =>
            {
                Interlocked.Increment(ref calls);
                entered.TrySetResult(entityId);
                await release.Task;
            }, TimeSpan.FromHours(1)));
            Assert.False(start(_ => ValueTask.CompletedTask, TimeSpan.FromHours(1)));
            Assert.Equal(expectedId, await entered.Task.WaitAsync(TimeSpan.FromSeconds(2)));

            var stopping = stop().AsTask();
            await Task.Delay(25);
            Assert.False(stopping.IsCompleted);
            release.TrySetResult();

            Assert.True(await stopping);
            Assert.Equal(1, Volatile.Read(ref calls));
        }
        finally
        {
            release.TrySetResult();
            await stopAll();
        }
    }
}
