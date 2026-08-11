using FluentAssertions;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Models;

namespace TomasAI.IFM.UI.Presentation.UnitTests.Models;

public class BaseModelExecutionBaselineTests
{
    [Fact]
    public async Task QuerySuccess_InvokesResultCallbackExactlyOnce()
    {
        var model = new ProbeModel();
        var observed = new List<int>();

        await model.QueryAsync(
            () => Task.FromResult<ServiceResult<int>>(new ServiceOk<int>(42)),
            observed.Add);

        observed.Should().Equal(42);
    }

    [Fact]
    public async Task QueryFailure_PreservesServiceErrorAndDoesNotPublishResult()
    {
        var model = new ProbeModel();
        var observed = new List<int>();
        (int Code, string Message)? error = null;
        model.OnError((code, message) => error = (code, message));

        await model.QueryAsync(
            () => Task.FromResult<ServiceResult<int>>(
                new ServiceFailed<int>(7101, "Query failed.")),
            observed.Add);

        observed.Should().BeEmpty();
        error.Should().Be((7101, "Query failed."));
    }

    [Fact]
    public async Task CommandSuccess_ReturnsCommandIdAndInvokesCompletionOnce()
    {
        var model = new ProbeModel();
        var commandId = Guid.NewGuid();
        var completionCount = 0;

        var result = await model.CommandAsync(
            () => Task.FromResult<ServiceResult<Guid>>(new ServiceOk<Guid>(commandId)),
            () => completionCount++);

        result.Should().Be(commandId);
        completionCount.Should().Be(1);
    }

    [Fact]
    public async Task CommandFailure_PreservesServiceErrorAndDoesNotInvokeCompletion()
    {
        var model = new ProbeModel();
        var completionCount = 0;
        (int Code, string Message)? error = null;
        model.OnError((code, message) => error = (code, message));

        var result = await model.CommandAsync(
            () => Task.FromResult<ServiceResult<Guid>>(
                new ServiceFailed<Guid>(7201, "Command failed.")),
            () => completionCount++);

        result.Should().Be(Guid.Empty);
        completionCount.Should().Be(0);
        error.Should().Be((7201, "Command failed."));
    }

    [Fact]
    public async Task ExecuteAsync_DoesNotCompleteBeforeOperationCompletes()
    {
        var model = new ProbeModel();
        IModel<ProbeModel> modelContract = model;
        var operationCompletion = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var execution = modelContract.ExecuteAsync(
            (_, _) => operationCompletion.Task);

        execution.IsCompleted.Should().BeFalse();
        operationCompletion.SetResult();
        await execution;
        execution.IsCompletedSuccessfully.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_PropagatesOperationFailure()
    {
        var model = new ProbeModel();
        IModel<ProbeModel> modelContract = model;
        var expected = new InvalidOperationException("Operation failed.");

        var act = () => modelContract.ExecuteAsync(
            (_, _) => Task.FromException(expected));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Operation failed.");
    }

    [Fact]
    public async Task ExecuteAsync_CancelledBeforeStart_DoesNotInvokeOperation()
    {
        var model = new ProbeModel();
        IModel<ProbeModel> modelContract = model;
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var invoked = false;

        var act = () => modelContract.ExecuteAsync(
            (_, _) =>
            {
                invoked = true;
                return Task.CompletedTask;
            },
            cancellation.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        invoked.Should().BeFalse();
    }

    [Fact]
    public async Task QueryException_PropagatesWithoutPublishingOrTranslatingError()
    {
        var model = new ProbeModel();
        var published = false;
        var errorNotified = false;
        model.OnError((_, _) => errorNotified = true);

        var act = () => model.QueryAsync<int>(
            () => Task.FromException<ServiceResult<int>>(
                new InvalidOperationException("Query transport failed.")),
            _ => published = true);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Query transport failed.");
        published.Should().BeFalse();
        errorNotified.Should().BeFalse();
    }

    [Fact]
    public async Task AsyncQueryContinuation_IsAwaitedBeforeQueryCompletes()
    {
        var model = new ProbeModel();
        var continuation = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var query = model.QueryAsync(
            () => Task.FromResult<ServiceResult<int>>(new ServiceOk<int>(42)),
            _ => continuation.Task);

        query.IsCompleted.Should().BeFalse();
        continuation.SetResult();
        await query;
        query.IsCompletedSuccessfully.Should().BeTrue();
    }

    sealed class ProbeModel : BaseModel<ProbeModel>
    {
        public Task QueryAsync<TResult>(
            Func<Task<ServiceResult<TResult>>> query,
            Action<TResult> onResult)
            => ExecuteAsync(query, onResult);

        public Task QueryAsync<TResult>(
            Func<Task<ServiceResult<TResult>>> query,
            Func<TResult, Task> onResult)
            => ExecuteAsync(query, onResult);

        public Task<Guid> CommandAsync(
            Func<Task<ServiceResult<Guid>>> command,
            Action onCompleted)
            => ExecuteCommandAsync(command, onCompleted);
    }
}
