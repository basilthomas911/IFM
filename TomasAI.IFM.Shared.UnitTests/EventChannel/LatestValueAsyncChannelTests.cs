using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.EventChannel;
using Xunit;

namespace TomasAI.IFM.Shared.UnitTests.EventChannel;

public class LatestValueAsyncChannelTests
{
    [Fact]
    public async Task BusyReader_ProcessesOnlyLatestPendingValue()
    {
        var processed = new ConcurrentQueue<int>();
        var firstStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var latestProcessed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var channel = new LatestValueAsyncChannel<int>(ReadAsync);

        Assert.True(channel.TryWrite(1));
        await firstStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(channel.TryWrite(2));
        Assert.True(channel.TryWrite(3));
        Assert.True(channel.TryWrite(4));
        releaseFirst.SetResult();
        await latestProcessed.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal([1, 4], processed);

        async ValueTask ReadAsync(int value, CancellationToken cancellationToken)
        {
            processed.Enqueue(value);
            if (value == 1)
            {
                firstStarted.SetResult();
                await releaseFirst.Task.WaitAsync(cancellationToken);
            }
            else if (value == 4)
            {
                latestProcessed.SetResult();
            }
        }
    }

    [Fact]
    public async Task ConcurrentWriters_AreSafeAndLatestValueWins()
    {
        var firstStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finalProcessed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var channel = new LatestValueAsyncChannel<int>(ReadAsync);

        Assert.True(channel.TryWrite(-1));
        await firstStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Task.WhenAll(Enumerable.Range(0, 8).Select(writer => Task.Run(() =>
        {
            for (var index = 0; index < 1_000; index++)
                Assert.True(channel.TryWrite((writer * 1_000) + index));
        })));
        Assert.True(channel.TryWrite(int.MaxValue));
        releaseFirst.SetResult();
        await finalProcessed.Task.WaitAsync(TimeSpan.FromSeconds(5));

        async ValueTask ReadAsync(int value, CancellationToken cancellationToken)
        {
            if (value == -1)
            {
                firstStarted.SetResult();
                await releaseFirst.Task.WaitAsync(cancellationToken);
            }
            else if (value == int.MaxValue)
            {
                finalProcessed.SetResult();
            }
        }
    }

    [Fact]
    public async Task ReaderCallbacks_AreSerialized()
    {
        var activeReaders = 0;
        var maximumActiveReaders = 0;
        var firstStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondProcessed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var channel = new LatestValueAsyncChannel<int>(ReadAsync);

        Assert.True(channel.TryWrite(1));
        await firstStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(channel.TryWrite(2));
        releaseFirst.SetResult();
        await secondProcessed.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(1, maximumActiveReaders);

        async ValueTask ReadAsync(int value, CancellationToken cancellationToken)
        {
            var active = Interlocked.Increment(ref activeReaders);
            InterlockedExtensions.Max(ref maximumActiveReaders, active);
            if (value == 1)
            {
                firstStarted.SetResult();
                await releaseFirst.Task.WaitAsync(cancellationToken);
            }
            Interlocked.Decrement(ref activeReaders);
            if (value == 2)
                secondProcessed.SetResult();
        }
    }

    [Fact]
    public async Task ReaderFailure_DoesNotStopProcessing()
    {
        var firstAttempted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondProcessed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var channel = new LatestValueAsyncChannel<int>(ReadAsync);

        Assert.True(channel.TryWrite(1));
        await firstAttempted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(channel.TryWrite(2));
        await secondProcessed.Task.WaitAsync(TimeSpan.FromSeconds(5));

        ValueTask ReadAsync(int value, CancellationToken _)
        {
            if (value == 1)
            {
                firstAttempted.SetResult();
                throw new InvalidOperationException("Expected test failure.");
            }

            secondProcessed.SetResult();
            return ValueTask.CompletedTask;
        }
    }

    [Fact]
    public async Task StopAsync_CancelsReaderAndRejectsSubsequentWrites()
    {
        var readerStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancellationObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var channel = new LatestValueAsyncChannel<int>(ReadAsync);

        Assert.True(channel.TryWrite(1));
        await readerStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await channel.StopAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(cancellationObserved.Task.IsCompletedSuccessfully);
        Assert.False(channel.IsOpen);
        Assert.False(channel.TryWrite(2));
        await channel.StopAsync();

        async ValueTask ReadAsync(int _, CancellationToken cancellationToken)
        {
            readerStarted.SetResult();
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                cancellationObserved.SetResult();
                throw;
            }
        }
    }

    [Fact]
    public async Task MinimumInterval_ThrottlesSuccessiveCallbacks()
    {
        var timestamps = new ConcurrentQueue<long>();
        var firstProcessed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondProcessed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var channel = new LatestValueAsyncChannel<int>(ReadAsync, TimeSpan.FromMilliseconds(100));

        Assert.True(channel.TryWrite(1));
        await firstProcessed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(channel.TryWrite(2));
        await secondProcessed.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var values = timestamps.ToArray();
        var elapsed = Stopwatch.GetElapsedTime(values[0], values[1]);
        Assert.True(elapsed >= TimeSpan.FromMilliseconds(75), $"Expected throttling, but callbacks were {elapsed.TotalMilliseconds:F1} ms apart.");

        ValueTask ReadAsync(int value, CancellationToken _)
        {
            timestamps.Enqueue(Stopwatch.GetTimestamp());
            if (value == 1)
                firstProcessed.SetResult();
            else if (value == 2)
                secondProcessed.SetResult();
            return ValueTask.CompletedTask;
        }
    }

    [Fact]
    public void Constructor_RejectsNegativeMinimumInterval()
        => Assert.Throws<ArgumentOutOfRangeException>(() =>
            new LatestValueAsyncChannel<int>(static (_, _) => ValueTask.CompletedTask, TimeSpan.FromMilliseconds(-1)));

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
