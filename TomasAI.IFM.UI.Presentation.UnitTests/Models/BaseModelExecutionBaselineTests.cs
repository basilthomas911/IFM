using FluentAssertions;
using TomasAI.IFM.Shared.EventSourcing;
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

    sealed class ProbeModel : BaseModel<ProbeModel>
    {
        public Task QueryAsync<TResult>(
            Func<Task<ServiceResult<TResult>>> query,
            Action<TResult> onResult)
            => ExecuteAsync(query, onResult);

        public Task<Guid> CommandAsync(
            Func<Task<ServiceResult<Guid>>> command,
            Action onCompleted)
            => ExecuteCommandAsync(command, onCompleted);
    }
}
