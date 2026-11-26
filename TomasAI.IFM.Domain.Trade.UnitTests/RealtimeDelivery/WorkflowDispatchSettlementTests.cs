using TomasAI.IFM.TestSupport;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Commands;

namespace TomasAI.IFM.Domain.Trade.UnitTests.RealtimeDelivery;

public sealed class WorkflowDispatchSettlementTests
{
    [Theory][InlineData(false,"read")][InlineData(true,"read")][InlineData(false,"send")][InlineData(true,"send")][InlineData(false,"typed")][InlineData(true,"typed")]
    public async Task Required_failures_retry_with_original_terminal_identity(bool metrics,string failure)
    {
        var source=WorkflowDispatchDeliveryFixture.Input();
        await using var state=new WorkflowDispatchDeliveryFixture(metrics);
        state.ReadCurrent=_=>failure=="read"?Task.FromException<TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model.IntrinsicTimeStrategyWorkflowView?>(new IOException("Required read failed.")):Task.FromResult<TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model.IntrinsicTimeStrategyWorkflowView?>(source.State);
        if(failure=="send")state.Send=_=>throw new IOException("Required command send failed.");
        state.TypedRejected=failure=="typed";await state.Start();
        Assert.False(await state.Deliver(source));
        var original=state.Commands.LastOrDefault();
        state.ReadCurrent=_=>Task.FromResult<TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model.IntrinsicTimeStrategyWorkflowView?>(source.State);
        state.Send=_=>ValueTask.CompletedTask;state.TypedRejected=false;
        Assert.True(await state.Deliver(source));
        var accepted=Assert.IsType<FailRegimeDiscoveryCommand>(state.Commands.Last());
        if(original is not null)Assert.Equal(original,accepted);
        Assert.Equal(source.Id,accepted.SourceEventId);Assert.True(accepted.CheckTriggerFreshness);
        // Reconstructed actor has no volatile recovery dependency for this retained stale-trigger request.
        await using var recovered=new WorkflowDispatchDeliveryFixture();
        recovered.ReadCurrent=_=>Task.FromResult<TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model.IntrinsicTimeStrategyWorkflowView?>(source.State);
        await recovered.Start();Assert.True(await recovered.Deliver(source));Assert.Equal(accepted,Assert.Single(recovered.Commands));
    }
    [Fact]
    public async Task Same_mailbox_awaits_handoff_before_processing_successor()
    {
        var first=WorkflowDispatchDeliveryFixture.Input(1);var second=WorkflowDispatchDeliveryFixture.Input(2);
        await using var state=new WorkflowDispatchDeliveryFixture();var current=first.State;
        state.ReadCurrent=_=>Task.FromResult<TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model.IntrinsicTimeStrategyWorkflowView?>(current);
        var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        state.Send=async command=>{if(command.CommandId!=Guid.Empty&&state.Commands.Count==1){entered.TrySetResult();await release.Task;}};
        await state.Start();var a=state.Deliver(first);await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        current=second.State;var b=state.Deliver(second);Assert.Single(state.Reads);Assert.Single(state.Commands);
        release.TrySetResult();Assert.True(await a);Assert.True(await b);
        Assert.Equal(new[]{first.Id,second.Id},state.Commands.Cast<FailRegimeDiscoveryCommand>().Select(x=>x.SourceEventId));
    }
    [Fact]
    public async Task Superseded_and_terminal_notifications_succeed_without_handoff()
    {
        var source=WorkflowDispatchDeliveryFixture.Input();await using var state=new WorkflowDispatchDeliveryFixture();
        await state.Start();Assert.True(await state.Deliver(source));Assert.Empty(state.Commands);
        var terminal=source with{State=source.State with{Status=TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model.WorkflowStrategyMachineStatus.Completed}};
        Assert.True(await state.Deliver(terminal));Assert.Empty(state.Commands);
    }
}
