using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.EventChannel;
using Xunit;

namespace TomasAI.IFM.Shared.UnitTests.EventChannel;

public class KeyedLatestValueAsyncChannelTests
{
    [Fact]
    public async Task BusyKey_CoalescesIndependentlyWithoutBlockingAnotherKey()
    {
        var processed = new ConcurrentDictionary<string, ConcurrentQueue<int>>();
        var firstAStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirstA = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var latestAProcessed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var bProcessed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var channel = new KeyedLatestValueAsyncChannel<string, int>(ReadAsync);

        Assert.True(channel.TryWrite("A", 1));
        await firstAStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(channel.TryWrite("A", 2));
        Assert.True(channel.TryWrite("A", 3));
        Assert.True(channel.TryWrite("B", 10));
        await bProcessed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        releaseFirstA.SetResult();
        await latestAProcessed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await channel.StopAsync();

        Assert.Equal([1, 3], processed["A"]);
        Assert.Equal([10], processed["B"]);
        Assert.Equal(1, channel.Metrics["A"].CoalescedCount);
        Assert.Equal(0, channel.Metrics["B"].CoalescedCount);

        async ValueTask ReadAsync(string key, int value, CancellationToken cancellationToken)
        {
            processed.GetOrAdd(key, _ => new ConcurrentQueue<int>()).Enqueue(value);
            if (key == "A" && value == 1)
            {
                firstAStarted.SetResult();
                await releaseFirstA.Task.WaitAsync(cancellationToken);
            }
            else if (key == "A" && value == 3)
            {
                latestAProcessed.SetResult();
            }
            else if (key == "B")
            {
                bProcessed.SetResult();
            }
        }
    }

    [Fact]
    public async Task StopAsync_ClosesEveryPartitionAndRejectsNewKeys()
    {
        var channel = new KeyedLatestValueAsyncChannel<string, int>(
            static (_, _, _) => ValueTask.CompletedTask);
        Assert.True(channel.TryWrite("ES", 1));
        Assert.True(channel.TryWrite("VX", 2));

        await channel.StopAsync();
        await channel.StopAsync();

        Assert.False(channel.IsOpen);
        Assert.False(channel.TryWrite("NQ", 3));
        Assert.All(channel.Metrics.Values, metrics => Assert.False(metrics.IsOpen));
    }

    [Fact]
    public void Constructor_RejectsNegativeMinimumInterval()
        => Assert.Throws<ArgumentOutOfRangeException>(() =>
            new KeyedLatestValueAsyncChannel<string, int>(
                static (_, _, _) => ValueTask.CompletedTask,
                TimeSpan.FromMilliseconds(-1)));
}
