using FluentAssertions;
using TomasAI.IFM.UI.Net.ViewModels.Operations;

namespace TomasAI.IFM.UI.Presentation.UnitTests.Operations;

public class AsyncOperationTests
{
    [Fact]
    public async Task ExecuteAsync_ExposesRunningStateUntilCompletion()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var operation = new AsyncOperation(_ => release.Task);

        var execution = operation.ExecuteAsync();

        operation.IsRunning.Should().BeTrue();
        release.SetResult();
        await execution;
        operation.IsRunning.Should().BeFalse();
        operation.CanExecute.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_WhileRunning_ReturnsSameExecution()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var operation = new AsyncOperation(_ => release.Task);

        var first = operation.ExecuteAsync();
        var second = operation.ExecuteAsync();

        second.Should().BeSameAs(first);
        release.SetResult();
        await first;
    }

    [Fact]
    public async Task Cancel_CancelsCurrentExecution()
    {
        await using var operation = new AsyncOperation(token =>
            Task.Delay(Timeout.InfiniteTimeSpan, token));
        var execution = operation.ExecuteAsync();

        operation.Cancel();

        await FluentActions.Awaiting(() => execution).Should().ThrowAsync<OperationCanceledException>();
        operation.IsRunning.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_PropagatesFailureAndCanRunAgain()
    {
        var attempts = 0;
        await using var operation = new AsyncOperation(_ =>
        {
            attempts++;
            return attempts == 1
                ? Task.FromException(new InvalidOperationException("failed"))
                : Task.CompletedTask;
        });

        await FluentActions.Awaiting(() => operation.ExecuteAsync())
            .Should().ThrowAsync<InvalidOperationException>();
        operation.LastFailure.Should().BeOfType<InvalidOperationException>();
        await operation.ExecuteAsync();

        attempts.Should().Be(2);
        operation.LastFailure.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_RaisesObservableBusyStateChanges()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var operation = new AsyncOperation(_ => release.Task);
        var changes = new List<string?>();
        operation.PropertyChanged += (_, args) => changes.Add(args.PropertyName);

        var execution = operation.ExecuteAsync();
        release.SetResult();
        await execution;

        changes.Should().ContainInOrder(
            nameof(operation.IsRunning),
            nameof(operation.CanExecute),
            nameof(operation.IsRunning),
            nameof(operation.CanExecute));
    }

    [Fact]
    public async Task ExecuteAsync_WhenExternalConditionDisallowsExecution_DoesNotRun()
    {
        var enabled = false;
        var executions = 0;
        await using var operation = new AsyncOperation(
            _ =>
            {
                executions++;
                return Task.CompletedTask;
            },
            () => enabled);

        operation.CanExecute.Should().BeFalse();
        await operation.ExecuteAsync();
        executions.Should().Be(0);

        enabled = true;
        operation.NotifyCanExecuteChanged();
        await operation.ExecuteAsync();
        executions.Should().Be(1);
    }
}
