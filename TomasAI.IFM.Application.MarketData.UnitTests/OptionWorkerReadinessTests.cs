using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Recovery;

namespace TomasAI.IFM.Application.MarketData.UnitTests;

public sealed class OptionWorkerReadinessTests
{
    static MarketDataDatasetStatus State(string lifecycle) => new("Options","GLBX.MDP3",Guid.NewGuid(),Guid.NewGuid(),123,
        lifecycle=="Healthy",lifecycle=="Healthy",lifecycle,null,new DateOnly(2026,10,22));

    [Fact]
    public async Task Acquisition_joins_running_recovery_and_succeeds_without_starting_a_competing_worker()
    {
        var state=State("Recovering");var observed=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);int restarts=0;
        using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var pending=OptionWorkerReadiness.EnsureAsync(_=>{observed.TrySetResult();return Task.FromResult<MarketDataDatasetStatus?>(Volatile.Read(ref state));},
            _=>{Interlocked.Increment(ref restarts);return Task.CompletedTask;},deadline.Token);
        await observed.Task;Assert.False(pending.IsCompleted);Assert.Equal(0,restarts);
        Volatile.Write(ref state,State("Healthy"));await pending;Assert.Equal(0,restarts);
    }

    [Fact]
    public async Task Recovery_that_confirms_cleanup_allows_one_fresh_worker_then_requires_healthy_state()
    {
        var state=State("Recovering");int observations=0,restarts=0;
        using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await OptionWorkerReadiness.EnsureAsync(_=>{
            if(++observations==2)state=State("StoppedError");return Task.FromResult<MarketDataDatasetStatus?>(state);
        },_=>{restarts++;state=State("Healthy");return Task.CompletedTask;},deadline.Token);
        Assert.Equal(1,restarts);Assert.True(observations>=3);
    }

    [Fact]
    public async Task Hung_recovery_ends_at_caller_deadline_without_discarding_it_or_restarting_any_worker()
    {
        int restarts=0;using var deadline=new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>OptionWorkerReadiness.EnsureAsync(_=>Task.FromResult<MarketDataDatasetStatus?>(State("Recovering")),
            _=>{restarts++;return Task.CompletedTask;},deadline.Token));Assert.Equal(0,restarts);
    }

    [Fact]
    public async Task Unconfirmed_cleanup_is_reported_explicitly_and_never_treated_as_a_stopped_or_healthy_worker()
    {
        int restarts=0;var error=await Assert.ThrowsAsync<InvalidOperationException>(()=>OptionWorkerReadiness.EnsureAsync(
            _=>Task.FromResult<MarketDataDatasetStatus?>(State("CleanupUnconfirmed")),_=>{restarts++;return Task.CompletedTask;},default));
        Assert.Contains("CleanupUnconfirmed",error.Message);Assert.Equal(0,restarts);
    }
}
