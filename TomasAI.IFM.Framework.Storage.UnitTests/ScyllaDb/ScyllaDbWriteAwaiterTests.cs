using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using TomasAI.IFM.Framework.Storage.ScyllaDb;
using Xunit;

namespace TomasAI.IFM.Framework.Storage.UnitTests.ScyllaDb;

public sealed class ScyllaDbWriteAwaiterTests
{
    [Fact]
    public void NonCancellableWait_ReturnsOriginalTask()
    {
        using var result = new DisposableResult();
        var pending = Task.FromResult(result);

        var awaited = ScyllaDbWriteAwaiter.AwaitAsync(pending, CancellationToken.None);

        awaited.Should().BeSameAs(pending);
    }

    [Fact]
    public async Task LinkedCancellation_StopsWaitAndDrainsLateResult()
    {
        var pending = new TaskCompletionSource<DisposableResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var callerCancellation = new CancellationTokenSource();
        using var siblingFailure = new CancellationTokenSource();
        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            callerCancellation.Token,
            siblingFailure.Token);

        var awaited = ScyllaDbWriteAwaiter.AwaitAsync(pending.Task, linkedCancellation.Token);
        siblingFailure.Cancel();

        await FluentActions.Awaiting(() => awaited)
            .Should().ThrowAsync<OperationCanceledException>();

        var lateResult = new DisposableResult();
        pending.SetResult(lateResult);
        await lateResult.Disposed.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task DrainAndDispose_WaitsForNonGenericDriverTask()
    {
        var pending = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var ownedResult = new DisposableResult();

        var drain = ScyllaDbWriteAwaiter.DrainAndDisposeAsync(pending.Task, ownedResult);
        ownedResult.Disposed.Task.IsCompleted.Should().BeFalse();

        pending.SetResult();
        await drain.WaitAsync(TimeSpan.FromSeconds(5));
        ownedResult.Disposed.Task.IsCompletedSuccessfully.Should().BeTrue();
    }

    sealed class DisposableResult : IDisposable
    {
        public TaskCompletionSource Disposed { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public void Dispose() => Disposed.TrySetResult();
    }
}
