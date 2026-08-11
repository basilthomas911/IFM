namespace TomasAI.IFM.Framework.MarketData.DataBento.UnitTests;

public sealed class DatabentoOperationRunnerTests
{
    [Fact]
    public async Task Bounded_runner_rejects_when_worker_and_queue_are_both_occupied()
    {
        var entered = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        await using var runner = new DatabentoOperationRunner([new FakeQueries()], 1);

        var running = runner.RunAsync<int>(_ =>
        {
            entered.TrySetResult();
            release.Task.GetAwaiter().GetResult();
            return 1;
        });
        await entered.Task;
        var queued = runner.RunAsync(_ => 2);

        Assert.Throws<InvalidOperationException>(() =>
        {
            _ = runner.RunAsync(_ => 3);
        });

        release.TrySetResult();
        Assert.Equal(1, await running);
        Assert.Equal(2, await queued);
    }

    [Fact]
    public async Task Provider_exception_is_observed_by_the_request()
    {
        await using var runner = new DatabentoOperationRunner([new FakeQueries()], 2);
        var error = await Assert.ThrowsAsync<TimeoutException>(() =>
            runner.RunAsync<int>(_ => throw new TimeoutException("bounded provider timeout")));
        Assert.Equal("bounded provider timeout", error.Message);
    }

    [Fact]
    public async Task Cancelled_queued_request_does_not_invoke_provider_operation()
    {
        var entered = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        await using var runner = new DatabentoOperationRunner([new FakeQueries()], 2);
        var blocker = runner.RunAsync<int>(_ =>
        {
            entered.TrySetResult();
            release.Task.GetAwaiter().GetResult();
            return 1;
        });
        await entered.Task;
        using var cancellation = new CancellationTokenSource();
        var invoked = false;
        var cancelled = runner.RunAsync(_ =>
        {
            invoked = true;
            return 2;
        }, cancellation.Token);
        cancellation.Cancel();
        release.TrySetResult();

        await blocker;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled);
        Assert.False(invoked);
    }

    private sealed class FakeQueries : IDatabentoMarketDataQueries
    {
        public OptionChainDefinitions GetChainDefinitions(
            OptionChainDefinitionRequest request,
            TimeSpan? timeout = null) => throw new NotSupportedException();
        public uint ContractIdToInstrumentId(string contractId, TimeSpan? timeout = null) =>
            throw new NotSupportedException();
        public string InstrumentIdToContractId(uint instrumentId, TimeSpan? timeout = null) =>
            throw new NotSupportedException();
        public ContractDetail? GetContractDetail(string contractName, TimeSpan? timeout = null) =>
            throw new NotSupportedException();
        public IReadOnlyList<ContractDetail> GetContractDetails(
            string ticker,
            TimeSpan? timeout = null) => throw new NotSupportedException();
        public IReadOnlyList<ContractDetail?> GetContractDetails(
            string[] contractNames,
            TimeSpan? timeout = null) => throw new NotSupportedException();
    }
}
