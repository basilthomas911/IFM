using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using TomasAI.IFM.Shared.EventModelActor.Persistence;
using Xunit;

namespace TomasAI.IFM.Shared.UnitTests.EventModelActor;

public sealed class DurableWindowBufferTests
{
    [Fact]
    public async Task Item_limit_flushes_one_atomic_window()
    {
        var commits = new List<int[]>();
        await using var buffer = Create(3, 100, TimeSpan.FromSeconds(1), values => commits.Add(values.ToArray()));
        var writes = Enumerable.Range(1, 3).Select(value => buffer.SubmitAsync(value, 1).AsTask()).ToArray();
        await Task.WhenAll(writes);
        commits.Should().ContainSingle().Which.Should().Equal(1, 2, 3);
    }

    [Fact]
    public async Task Byte_limit_carries_the_first_item_of_the_next_window()
    {
        var commits = new List<int[]>();
        await using var buffer = Create(8, 4, TimeSpan.FromMilliseconds(10), values => commits.Add(values.ToArray()));
        await Task.WhenAll(buffer.SubmitAsync(1, 3).AsTask(), buffer.SubmitAsync(2, 3).AsTask());
        commits.Should().HaveCount(2);
        commits.SelectMany(x => x).Should().Equal(1, 2);
    }

    [Fact]
    public async Task Continuous_arrivals_do_not_extend_the_first_item_deadline()
    {
        var firstCommit = new TaskCompletionSource<long>(TaskCreationOptions.RunContinuationsAsynchronously);
        var stopwatch = Stopwatch.StartNew();
        await using var buffer = Create(256, 4096, TimeSpan.FromMilliseconds(40), _ => firstCommit.TrySetResult(stopwatch.ElapsedMilliseconds));
        var writes = new List<Task> { buffer.SubmitAsync(0, 1).AsTask() };
        for (var index = 1; index < 20; index++)
        {
            await Task.Delay(5);
            writes.Add(buffer.SubmitAsync(index, 1).AsTask());
        }
        var committedAt = await firstCommit.Task.WaitAsync(TimeSpan.FromSeconds(1));
        committedAt.Should().BeLessThan(90);
        await Task.WhenAll(writes);
    }

    [Fact]
    public async Task Barrier_flushes_partial_window_and_completes_after_commit()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var buffer = new DurableWindowBuffer<int>(8, 8, 100, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(1),
            async (_, _) => await release.Task);
        var write = buffer.SubmitAsync(1, 1).AsTask();
        var barrier = buffer.BarrierAsync().AsTask();
        await Task.Delay(20);
        barrier.IsCompleted.Should().BeFalse();
        release.TrySetResult();
        await Task.WhenAll(write, barrier);
    }

    [Fact]
    public async Task Commit_failure_completes_every_item_once_with_the_same_failure()
    {
        var failure = new InvalidOperationException("commit failed");
        await using var buffer = new DurableWindowBuffer<int>(8, 2, 100, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1),
            (_, _) => ValueTask.FromException(failure));
        var writes = new[] { buffer.SubmitAsync(1, 1).AsTask(), buffer.SubmitAsync(2, 1).AsTask() };
        foreach (var write in writes)
            (await FluentActions.Awaiting(() => write).Should().ThrowAsync<InvalidOperationException>())
                .Which.Should().BeSameAs(failure);
    }

    static DurableWindowBuffer<int> Create(
        int maximumItems, int maximumBytes, TimeSpan maximumAge, Action<IReadOnlyList<int>> commit)
        => new(512, maximumItems, maximumBytes, maximumAge, TimeSpan.FromSeconds(1), (items, _) =>
        {
            commit(items);
            return ValueTask.CompletedTask;
        });
}
