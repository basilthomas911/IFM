using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using TomasAI.IFM.Shared.EventModelActor;
using Xunit;

namespace TomasAI.IFM.Shared.UnitTests.EventModelActor;

public sealed class ActorReadyQueueTests
{
    [Fact]
    public async Task Reader_WakesInFifoOrder_AndDrainsBeforeCompletion()
    {
        var queue = new ActorReadyQueue();
        await using var reader = queue.ReadAllAsync(default).GetAsyncEnumerator();
        var pending = reader.MoveNextAsync().AsTask();
        pending.IsCompleted.Should().BeFalse();
        var first = new ActorThreadId(ActorType.Query, "ReadyTest", "1");
        var second = new ActorThreadId(ActorType.Query, "ReadyTest", "2");
        queue.Schedule(first).Should().BeTrue();
        queue.Schedule(second).Should().BeTrue();
        queue.Complete();
        queue.Schedule(first).Should().BeFalse();
        (await pending.WaitAsync(TimeSpan.FromSeconds(5))).Should().BeTrue();
        reader.Current.Should().Be(first);
        (await reader.MoveNextAsync()).Should().BeTrue();
        reader.Current.Should().Be(second);
        (await reader.MoveNextAsync()).Should().BeFalse();
        queue.ScheduledCount.Should().Be(0);
    }

    [Fact]
    public async Task CancelingWaitingReader_DoesNotLoseWorkForOtherReaders()
    {
        var queue = new ActorReadyQueue();
        using var cancellation = new CancellationTokenSource();
        await using var reader = queue.ReadAllAsync(cancellation.Token).GetAsyncEnumerator();
        var pending = reader.MoveNextAsync().AsTask();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        var id = new ActorThreadId(ActorType.Query, "ReadyTest", "1");
        queue.Schedule(id).Should().BeTrue();
        queue.Complete();
        await using var replacement = queue.ReadAllAsync(default).GetAsyncEnumerator();
        (await replacement.MoveNextAsync()).Should().BeTrue();
        replacement.Current.Should().Be(id);
        (await replacement.MoveNextAsync()).Should().BeFalse();
        queue.ScheduledCount.Should().Be(0);
    }

    [Fact]
    public async Task MultipleReadersAndWriters_ConsumeEveryEntryExactlyOnce()
    {
        var queue = new ActorReadyQueue();
        var counts = new ConcurrentDictionary<ActorThreadId, int>();
        var readers = Enumerable.Range(0, 8).Select(_ => Task.Run(async () =>
        {
            await foreach (var id in queue.ReadAllAsync(default))
                counts.AddOrUpdate(id, 1, (_, count) => count + 1);
        })).ToArray();
        Parallel.For(0, 4096, index => queue.Schedule(
            new ActorThreadId(ActorType.Query, "ReadyTest", index.ToString())).Should().BeTrue());
        queue.Complete();
        await Task.WhenAll(readers).WaitAsync(TimeSpan.FromSeconds(10));
        counts.Should().HaveCount(4096);
        counts.Values.Should().OnlyContain(count => count == 1);
        queue.ScheduledCount.Should().Be(0);
    }
}
