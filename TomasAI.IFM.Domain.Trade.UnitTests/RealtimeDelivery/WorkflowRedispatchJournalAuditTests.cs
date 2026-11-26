using MessagePack;
using NSubstitute;
using TomasAI.IFM.TestSupport;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Commands;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Realtime;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Realtime;

namespace TomasAI.IFM.Domain.Trade.UnitTests.RealtimeDelivery;
public sealed class WorkflowRedispatchJournalAuditTests
{
    [Fact]
    public async Task Audit_legitimate_same_view_redispatch_conflicts_with_full_snapshot_journal_hash()
    {
        var source=WorkflowDispatchDeliveryFixture.RegimeFunctionInput(representative:true);
        using var journal=new WorkflowRegimeFunctionFailures(Path.Combine(Path.GetTempPath(),"ifm-full-audit",Guid.NewGuid().ToString("N")));
        await using var state=new WorkflowDispatchDeliveryFixture(regimeFunctionFailures:journal);state.BindRegimeInputs(source);
        state.Send=_=>ValueTask.FromException(new IOException("Original terminal not accepted yet."));
        await state.Start();Assert.False(await state.Deliver(source));Assert.Single(journal.Pending());Assert.Single(state.Commands);
        var redispatch=new RedispatchCurrentStrategyPipelineCommand{CommandId=Guid.NewGuid(),EntityId=source.EntityId,WorkflowId=source.WorkflowId,
            ExpectedWorkflowRevision=source.WorkflowRevision,ExpectedStage=source.State.CurrentStage,RequestedAtUtc=state.Time.At,RequestedBy="FullAudit.Operator"};
        var owner=Substitute.For<IIntrinsicTimeStrategyWorkflowCommandContext>();owner.TimeProvider.Returns(state.Time);
        var preparation=new WorkflowSnapshotPreparation(source.State);Assert.True(redispatch.PrepareWorkflow(owner,preparation).Success);
        var change=Assert.Single(preparation.Freeze());
        var emitted=source with{Id=change.SnapshotEventId,CommandId=redispatch.CommandId,CausationId=change.CausationId,
            UpdatedAtUtc=change.UpdatedAtUtc,ReceivedOn=change.UpdatedAtUtc,State=change.WorkflowDefinition};
        Assert.Equal(MessagePackSerializer.Serialize(source.State),MessagePackSerializer.Serialize(emitted.State));Assert.NotEqual(source.Id,emitted.Id);
        await Assert.ThrowsAsync<InvalidDataException>(()=>RegimeFunctionTerminalDelivery.RecoverAsync(state.Context,emitted).AsTask());
        Assert.Single(state.Commands);Assert.Single(journal.Pending());
    }
}
