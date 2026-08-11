using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
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
    public async Task PeakBurst_IsBoundedPerKeyAndConvergesToEveryLatestValue()
    {
        const int keyCount = 8;
        const int valuesPerKey = 10_000;
        var processed = new ConcurrentDictionary<string, ConcurrentQueue<int>>();
        var firstReadersStarted = new CountdownEvent(keyCount);
        var latestReadersCompleted = new CountdownEvent(keyCount);
        var releaseFirstReaders = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var channel = new KeyedLatestValueAsyncChannel<string, int>(ReadAsync);
        var keys = Enumerable.Range(0, keyCount).Select(index => $"contract-{index}").ToArray();

        foreach (var key in keys)
            Assert.True(channel.TryWrite(key, 0));
        Assert.True(firstReadersStarted.Wait(TimeSpan.FromSeconds(5)));
        foreach (var key in keys)
            for (var value = 1; value < valuesPerKey; value++)
                Assert.True(channel.TryWrite(key, value));

        releaseFirstReaders.SetResult();
        Assert.True(latestReadersCompleted.Wait(TimeSpan.FromSeconds(5)));
        await channel.StopAsync();

        Assert.Equal(keyCount, channel.Metrics.Count);
        foreach (var key in keys)
        {
            Assert.Equal([0, valuesPerKey - 1], processed[key]);
            Assert.Equal(valuesPerKey, channel.Metrics[key].AcceptedCount);
            Assert.Equal(valuesPerKey - 2, channel.Metrics[key].CoalescedCount);
            Assert.Equal(2, channel.Metrics[key].ProcessedCount);
            Assert.False(channel.Metrics[key].IsOpen);
        }

        async ValueTask ReadAsync(string key, int value, CancellationToken cancellationToken)
        {
            processed.GetOrAdd(key, _ => new ConcurrentQueue<int>()).Enqueue(value);
            if (value == 0)
            {
                firstReadersStarted.Signal();
                await releaseFirstReaders.Task.WaitAsync(cancellationToken);
            }
            else if (value == valuesPerKey - 1)
            {
                latestReadersCompleted.Signal();
            }
        }
    }

    [Fact]
    public void Constructor_RejectsNegativeMinimumInterval()
        => Assert.Throws<ArgumentOutOfRangeException>(() =>
            new KeyedLatestValueAsyncChannel<string, int>(
                static (_, _, _) => ValueTask.CompletedTask,
                TimeSpan.FromMilliseconds(-1)));
}
