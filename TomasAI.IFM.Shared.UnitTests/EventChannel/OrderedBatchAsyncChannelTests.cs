using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.EventChannel;
using Xunit;

namespace TomasAI.IFM.Shared.UnitTests.EventChannel;

public class OrderedBatchAsyncChannelTests
{
    [Fact]
    public async Task Burst_PreservesArrivalOrderAndProcessesBatches()
    {
        var processed = new ConcurrentQueue<int>();
        var batchSizes = new ConcurrentQueue<int>();
        var channel = new OrderedBatchAsyncChannel<int>(
            ReadBatchAsync,
            capacity: 32,
            maximumBatchSize: 8);

        for (var value = 0; value < 24; value++)
            await channel.WriteAsync(value);
        await channel.StopAsync();

        Assert.Equal(Enumerable.Range(0, 24), processed);
        Assert.All(batchSizes, batchSize => Assert.InRange(batchSize, 1, 8));
        Assert.Equal(24, channel.Metrics.AcceptedCount);
        Assert.Equal(24, channel.Metrics.ProcessedCount);
        Assert.Equal(batchSizes.Count, channel.Metrics.BatchCount);
        Assert.Equal(32, channel.Metrics.Capacity);

        ValueTask ReadBatchAsync(IReadOnlyList<int> batch, CancellationToken _)
        {
            batchSizes.Enqueue(batch.Count);
            foreach (var value in batch)
                processed.Enqueue(value);
            return ValueTask.CompletedTask;
        }
    }

    [Fact]
    public async Task FullChannel_AwaitsCapacityWithoutDroppingOrReordering()
    {
        var processed = new ConcurrentQueue<int>();
        var firstStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var channel = new OrderedBatchAsyncChannel<int>(ReadBatchAsync, capacity: 1, maximumBatchSize: 1);

        await channel.WriteAsync(1);
        await firstStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await channel.WriteAsync(2);
        var blockedWrite = channel.WriteAsync(3).AsTask();
        await Task.Delay(50);

        Assert.False(blockedWrite.IsCompleted);
        releaseFirst.SetResult();
        await blockedWrite.WaitAsync(TimeSpan.FromSeconds(5));
        await channel.StopAsync();

        Assert.Equal([1, 2, 3], processed);
        Assert.True(channel.Metrics.BackpressuredWriteCount >= 1);
        Assert.Equal(3, channel.Metrics.ProcessedCount);

        async ValueTask ReadBatchAsync(IReadOnlyList<int> batch, CancellationToken cancellationToken)
        {
            foreach (var value in batch)
            {
                if (value == 1)
                {
                    firstStarted.SetResult();
                    await releaseFirst.Task.WaitAsync(cancellationToken);
                }
                processed.Enqueue(value);
            }
        }
    }

    [Fact]
    public async Task PeakBurst_DrainsTenThousandEventsInOrderWithinFixedCapacity()
    {
        const int eventCount = 10_000;
        var processed = new List<int>(eventCount);
        var channel = new OrderedBatchAsyncChannel<int>(
            (batch, _) =>
            {
                processed.AddRange(batch);
                return ValueTask.CompletedTask;
            },
            capacity: 256,
            maximumBatchSize: 32);

        for (var value = 0; value < eventCount; value++)
            await channel.WriteAsync(value);
        await channel.StopAsync();

        Assert.Equal(Enumerable.Range(0, eventCount), processed);
        Assert.Equal(eventCount, channel.Metrics.AcceptedCount);
        Assert.Equal(eventCount, channel.Metrics.ProcessedCount);
        Assert.Equal(256, channel.Metrics.Capacity);
        Assert.False(channel.Metrics.IsOpen);
    }

    [Fact]
    public async Task TransientReaderFailure_RetriesTheSameBatch()
    {
        var attempts = 0;
        var processed = new List<int>();
        var channel = new OrderedBatchAsyncChannel<int>(
            ReadBatchAsync,
            capacity: 4,
            maximumBatchSize: 4,
            readerRetryCount: 1);

        await channel.WriteAsync(7);
        await channel.StopAsync();

        Assert.Equal(2, attempts);
        Assert.Equal([7], processed);
        Assert.Equal(1, channel.Metrics.FailureCount);
        Assert.Equal(1, channel.Metrics.ProcessedCount);

        ValueTask ReadBatchAsync(IReadOnlyList<int> batch, CancellationToken _)
        {
            attempts++;
            if (attempts == 1)
                throw new InvalidOperationException("Expected transient test failure.");
            processed.AddRange(batch);
            return ValueTask.CompletedTask;
        }
    }

    [Fact]
    public async Task ExhaustedReaderRetries_FaultShutdownAndCloseTheChannel()
    {
        var channel = new OrderedBatchAsyncChannel<int>(
            static (_, _) => throw new InvalidOperationException("permanent test failure"),
            capacity: 1,
            maximumBatchSize: 1,
            readerRetryCount: 1);

        await channel.WriteAsync(1);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => channel.StopAsync().AsTask());

        Assert.Equal("permanent test failure", exception.Message);
        Assert.Equal(2, channel.Metrics.FailureCount);
        Assert.False(channel.IsOpen);
        await Assert.ThrowsAsync<ChannelClosedException>(() => channel.WriteAsync(2).AsTask());
    }

    [Fact]
    public async Task StopAsync_DrainsAcceptedValuesAndRejectsLaterWrites()
    {
        var processed = new ConcurrentQueue<int>();
        var channel = new OrderedBatchAsyncChannel<int>(
            (batch, _) =>
            {
                foreach (var value in batch)
                    processed.Enqueue(value);
                return ValueTask.CompletedTask;
            },
            capacity: 8,
            maximumBatchSize: 4);

        await channel.WriteAsync(1);
        await channel.WriteAsync(2);
        await channel.WriteAsync(3);
        await channel.StopAsync();

        Assert.Equal([1, 2, 3], processed);
        Assert.False(channel.IsOpen);
        await Assert.ThrowsAsync<ChannelClosedException>(() => channel.WriteAsync(4).AsTask());
        await channel.StopAsync();
    }

    [Fact]
    public void Constructor_RejectsInvalidBounds()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new OrderedBatchAsyncChannel<int>(static (_, _) => ValueTask.CompletedTask, capacity: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new OrderedBatchAsyncChannel<int>(static (_, _) => ValueTask.CompletedTask, capacity: 2, maximumBatchSize: 3));
    }
}
