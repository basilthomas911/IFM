using System.Collections.Concurrent;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Order.Broker;
using TomasAI.IFM.TestSupport;

namespace TomasAI.IFM.Domain.Trade.UnitTests.RealtimeDelivery;

public sealed class BrokerObservationPublicationAuditTests
{
    static string PathForTest()=>Path.Combine(Path.GetTempPath(),"ifm-observation-publication",Guid.NewGuid().ToString("N"),"ledger.json");

    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(4)]
    public async Task Failed_publication_retries_original_fact_before_successors(int legs)
    {
        await using var state=new BrokerObservationDeliveryFixture(PathForTest(),legs);
        var attempts=new ConcurrentQueue<BrokerOrderObservationReceivedEvent>();var failed=0;
        state.Publish=(fact,_)=>
        {
            attempts.Enqueue(fact);
            if(fact.Observation.Kind==BrokerOrderObservationKind.Execution&&Interlocked.Increment(ref failed)==1)
                throw new IOException("Before broker acceptance.");
            return ValueTask.CompletedTask;
        };
        await using var bridge=state.Bridge();await bridge.StartAsync();await state.WaitForCursor();await bridge.StopAsync();
        var all=attempts.ToArray();var index=Array.FindIndex(all,f=>f.Observation.Kind==BrokerOrderObservationKind.Execution);
        Assert.Equal(all[index],all[index+1]);
        var expected=state.Real.Checkpoints.Load()!.Observations.Where(f=>f.Kind!=TomasAI.IFM.Framework.TradeBroker.Contracts.FrameworkObservationKind.AccountSnapshot).Select(f=>f.ObservationId);
        Assert.Equal(expected,all.Where((_,i)=>i!=index).Select(f=>f.Observation.ObservationId));
        var count=all.Length;await bridge.StartAsync();await bridge.StopAsync();Assert.Equal(count,attempts.Count);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Failed_or_uncertain_cursor_save_replays_same_event_without_financial_change(bool afterWrite)
    {
        await using var state=new BrokerObservationDeliveryFixture(PathForTest());
        var before=state.Real.Checkpoints.Load()!;var attempts=new ConcurrentQueue<BrokerOrderObservationReceivedEvent>();
        var failed=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        state.Publish=async (fact,token)=>
        {
            attempts.Enqueue(fact);
            if(fact.Observation.Kind!=BrokerOrderObservationKind.Execution)return;
            if(attempts.Count(f=>f.Observation.Kind==BrokerOrderObservationKind.Execution)==1)
            {state.Real.Checkpoints.FailBeforeSave=!afterWrite;state.Real.Checkpoints.FailAfterSave=afterWrite;return;}
            failed.TrySetResult();await Task.Delay(Timeout.Infinite,token);
        };
        await using(var bridge=state.Bridge())
        {await bridge.StartAsync();await failed.Task.WaitAsync(TimeSpan.FromSeconds(5));await bridge.StopAsync();}
        var saved=state.Real.Checkpoints.Load()!;
        Assert.Equal(before.Cash,saved.Cash);Assert.Equal(before.Observations,saved.Observations);
        state.Real.Checkpoints.FailBeforeSave=false;state.Real.Checkpoints.FailAfterSave=false;state.Real.Restart();
        var recovered=new ConcurrentQueue<BrokerOrderObservationReceivedEvent>();
        state.Publish=(fact,_)=>{recovered.Enqueue(fact);return ValueTask.CompletedTask;};
        await using(var bridge=state.Bridge()){await bridge.StartAsync();await state.WaitForCursor();await bridge.StopAsync();}
        var first=attempts.First(f=>f.Observation.Kind==BrokerOrderObservationKind.Execution);
        if(!afterWrite)Assert.Equal(first,recovered.First());
        else Assert.DoesNotContain(recovered,f=>f.Observation.ObservationId==first.Observation.ObservationId);
        Assert.Equal(before.Cash,state.Real.Checkpoints.Load()!.Cash);
    }

    [Theory]
    [InlineData(1)] [InlineData(4)]
    public async Task Bridge_and_ledger_restart_resume_blocked_execution_with_original_ids(int legs)
    {
        await using var state=new BrokerObservationDeliveryFixture(PathForTest(),legs);
        var blocked=new TaskCompletionSource<BrokerOrderObservationReceivedEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
        state.Publish=async(fact,token)=>
        {
            if(fact.Observation.Kind!=BrokerOrderObservationKind.Execution)return;
            blocked.TrySetResult(fact);await Task.Delay(Timeout.Infinite,token);
        };
        await using(var bridge=state.Bridge()){await bridge.StartAsync();await blocked.Task.WaitAsync(TimeSpan.FromSeconds(5));await bridge.StopAsync();}
        state.Real.Restart();var accepted=new ConcurrentQueue<BrokerOrderObservationReceivedEvent>();
        state.Publish=(fact,_)=>{accepted.Enqueue(fact);return ValueTask.CompletedTask;};
        await using(var bridge=state.Bridge()){await bridge.StartAsync();await state.WaitForCursor();await bridge.StopAsync();}
        Assert.Equal(await blocked.Task,accepted.First());Assert.Equal(legs,accepted.Count(f=>f.Observation.Kind==BrokerOrderObservationKind.Execution));
    }

    [Fact]
    public async Task Lost_publish_reply_is_retried_with_identical_event_and_content()
    {
        await using var state=new BrokerObservationDeliveryFixture(PathForTest());
        var accepted=new ConcurrentQueue<BrokerOrderObservationReceivedEvent>();var failed=0;
        state.Publish=(fact,_)=>
        {accepted.Enqueue(fact);if(fact.Observation.Kind==BrokerOrderObservationKind.Execution&&Interlocked.Increment(ref failed)==1)throw new IOException("Accepted, reply lost.");return ValueTask.CompletedTask;};
        await using var bridge=state.Bridge();await bridge.StartAsync();await state.WaitForCursor();await bridge.StopAsync();
        var executions=accepted.Where(f=>f.Observation.Kind==BrokerOrderObservationKind.Execution).ToArray();Assert.Equal(2,executions.Length);Assert.Equal(executions[0],executions[1]);
        Assert.Single(executions.Select(f=>f.Id).Distinct());
    }
}
