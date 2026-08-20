using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using TomasAI.IFM.Application.Storage.CommandDeduplication;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.CommandDeduplication;

[Trait("Category", "Unit")]
public sealed class CommandDuplicateCoordinatorTests
{
    [Fact]
    public async Task Repeated_completed_id_shortcuts_without_another_durable_reservation()
    {
        var coordinator = new CommandDuplicateCoordinator(8);
        var commandId = Guid.NewGuid();
        var calls = 0;
        Task<bool> Reserve(CancellationToken _)
        {
            Interlocked.Increment(ref calls);
            return Task.FromResult(true);
        }

        (await coordinator.TryAcceptAsync(commandId, Reserve)).Should().BeTrue();
        (await coordinator.TryAcceptAsync(commandId, Reserve)).Should().BeFalse();

        calls.Should().Be(1);
        coordinator.CompletedCount.Should().Be(1);
    }

    [Fact]
    public async Task Concurrent_same_id_calls_share_one_reservation_and_have_one_owner()
    {
        var coordinator = new CommandDuplicateCoordinator(8);
        var commandId = Guid.NewGuid();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        async Task<bool> Reserve(CancellationToken _)
        {
            Interlocked.Increment(ref calls);
            entered.TrySetResult();
            await release.Task;
            return true;
        }

        var owner = coordinator.TryAcceptAsync(commandId, Reserve);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var followers = Enumerable.Range(0, 31)
            .Select(_ => coordinator.TryAcceptAsync(commandId, Reserve))
            .ToArray();
        release.TrySetResult();

        var results = await Task.WhenAll(
            followers.Prepend(owner).Select(operation => operation.AsTask()));

        results.Count(accepted => accepted).Should().Be(1);
        calls.Should().Be(1);
    }

    [Fact]
    public async Task Existing_durable_id_is_cached_as_a_duplicate()
    {
        var coordinator = new CommandDuplicateCoordinator(8);
        var commandId = Guid.NewGuid();
        var calls = 0;
        Task<bool> Reserve(CancellationToken _)
        {
            Interlocked.Increment(ref calls);
            return Task.FromResult(false);
        }

        (await coordinator.TryAcceptAsync(commandId, Reserve)).Should().BeFalse();
        (await coordinator.TryAcceptAsync(commandId, Reserve)).Should().BeFalse();

        calls.Should().Be(1);
    }

    [Fact]
    public async Task Failed_reservation_does_not_poison_the_id()
    {
        var coordinator = new CommandDuplicateCoordinator(8);
        var commandId = Guid.NewGuid();
        var calls = 0;
        Task<bool> Reserve(CancellationToken _)
        {
            if (Interlocked.Increment(ref calls) == 1)
                throw new InvalidOperationException("database unavailable");
            return Task.FromResult(true);
        }

        var failed = () => coordinator.TryAcceptAsync(commandId, Reserve).AsTask();
        await failed.Should().ThrowAsync<InvalidOperationException>();
        (await coordinator.TryAcceptAsync(commandId, Reserve)).Should().BeTrue();

        calls.Should().Be(2);
    }

    [Fact]
    public async Task Evicted_id_falls_back_to_the_durable_authority()
    {
        var coordinator = new CommandDuplicateCoordinator(2);
        var first = Guid.NewGuid();
        var calls = new Dictionary<Guid, int>();
        Task<bool> Reserve(Guid id, bool result)
        {
            calls[id] = calls.GetValueOrDefault(id) + 1;
            return Task.FromResult(result);
        }

        (await coordinator.TryAcceptAsync(first, _ => Reserve(first, true))).Should().BeTrue();
        foreach (var id in new[] { Guid.NewGuid(), Guid.NewGuid() })
            (await coordinator.TryAcceptAsync(id, _ => Reserve(id, true))).Should().BeTrue();

        (await coordinator.TryAcceptAsync(first, _ => Reserve(first, false))).Should().BeFalse();

        calls[first].Should().Be(2);
        coordinator.CompletedCount.Should().Be(2);
    }

    [Fact]
    public async Task Cancelled_follower_does_not_cancel_or_remove_the_owner()
    {
        var coordinator = new CommandDuplicateCoordinator(8);
        var commandId = Guid.NewGuid();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        async Task<bool> Reserve(CancellationToken _)
        {
            Interlocked.Increment(ref calls);
            entered.TrySetResult();
            await release.Task;
            return true;
        }

        var owner = coordinator.TryAcceptAsync(commandId, Reserve);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        using var cancellation = new CancellationTokenSource();
        var follower = coordinator.TryAcceptAsync(commandId, Reserve, cancellation.Token);
        cancellation.Cancel();

        await FluentActions.Awaiting(() => follower.AsTask()).Should().ThrowAsync<OperationCanceledException>();
        release.TrySetResult();
        (await owner).Should().BeTrue();
        (await coordinator.TryAcceptAsync(commandId, Reserve)).Should().BeFalse();
        calls.Should().Be(1);
    }
}
