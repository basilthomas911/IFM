using MessagePack;
using NSubstitute;
using TomasAI.IFM.TestSupport;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Realtime;
using TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.TradeSelection;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.Trade.UnitTests.RealtimeDelivery;
public sealed class WorkflowSelectionResultTerminalAuditTests
{
    static string DirectoryForTest()=>Path.Combine(Path.GetTempPath(),"ifm-selection-function",Guid.NewGuid().ToString("N"));
    static WorkflowDispatchDeliveryFixture Create(WorkflowSelectionFunctionFailures journal,WorkflowStrategyStateUpdatedEvent source,bool metrics=false)
    {
        var state=new WorkflowDispatchDeliveryFixture(metrics,selectionFunctionFailures:journal);
        state.ReadCurrent=_=>Task.FromResult<IntrinsicTimeStrategyWorkflowView?>(source.State);return state;
    }
    static async Task<WorkflowStrategyStateUpdatedEvent> RealSource()
    {
        var execute=await TradeSelectionFixture.Command();var initial=WorkflowDispatchDeliveryFixture.SelectionFunctionInput();
        MessageOrigins.Restore(execute,new MessageOrigin(new DateTimeOffset(execute.RequestedAtUtc),"selection-test",Guid.NewGuid(),Guid.NewGuid()));
        var view=execute.WorkflowView with{EntityId=execute.WorkflowEntityId,WorkflowId=execute.WorkflowId,WorkflowRevision=execute.InputWorkflowRevision,
            Status=WorkflowStrategyMachineStatus.Started,CurrentStage=StrategyWorkflowStage.TradeSelection,SelectionDispatch=execute,DecisionRuntimeVersion=1};
        return initial with{EntityId=view.EntityId,WorkflowId=view.WorkflowId,WorkflowRevision=view.WorkflowRevision,State=view};
    }
    [Theory][InlineData(false,"send")][InlineData(true,"send")][InlineData(false,"typed")][InlineData(true,"typed")][InlineData(false,"lost")][InlineData(true,"lost")]
    public async Task Real_request_function_failure_preserves_terminal_across_retry_and_cold_start(bool metrics,string mode)
    {
        var source=await RealSource();var directory=DirectoryForTest();var request=source.State.SelectionDispatch!;
        var artifact="C:/repos/IFM/.artifacts/realtime-mailboxes/20261124-safety/workflow-selection-function-real-source.msgpack";
        Directory.CreateDirectory(Path.GetDirectoryName(artifact)!);File.WriteAllBytes(artifact,MessagePackSerializer.Serialize(source));
        FailTradeSelectionCommand original;MessageOrigin? originalOrigin;byte[]? accepted=null;
        using(var journal=new WorkflowSelectionFunctionFailures(directory))
        {
            await using var state=Create(journal,source,metrics);state.TypedRejected=mode=="typed";
            state.Send=message=>
            {
                var bytes=MessagePackSerializer.Serialize((FailTradeSelectionCommand)message);
                if(accepted is not null)Assert.Equal(accepted,bytes);else accepted=bytes;
                return mode=="typed"?ValueTask.CompletedTask:ValueTask.FromException(new IOException("Original terminal not confirmed."));
            };
            await state.Start();Assert.False(await state.Deliver(source));
            original=Assert.IsType<FailTradeSelectionCommand>(Assert.Single(state.Commands));Assert.Equal(original,Assert.Single(journal.Pending()).Command);
            Assert.Equal(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(MessagePackSerializer.Serialize(request))),journal.Find(request.CommandId)!.InputHash);
            originalOrigin=MessageOrigins.Get(original);Assert.NotNull(originalOrigin);Assert.Equal(request.CommandId,originalOrigin.CausationId);
            Assert.Equal(MessageOrigins.Get(request)!.RunId,originalOrigin.RunId);Assert.Equal(MessageOrigins.Get(request)!.Generation,originalOrigin.Generation);
            var reads=state.Reads.Count;state.Time.At=state.Time.At.AddDays(1);
            Assert.False(await state.Deliver(source));Assert.Equal(reads,state.Reads.Count);Assert.Equal(original,state.Commands.Last());
        }
        using(var journal=new WorkflowSelectionFunctionFailures(directory))
        {
            await using var recovered=Create(journal,source);recovered.Time.At=recovered.Time.At.AddDays(2);
            await recovered.Start();Assert.Equal(original,Assert.Single(recovered.Commands));Assert.Empty(journal.Pending());Assert.Empty(recovered.Reads);
            Assert.Equal(originalOrigin,MessageOrigins.Get(recovered.Commands[0]));
            Assert.True(await recovered.Deliver(source));Assert.Single(recovered.Commands);Assert.Empty(recovered.Reads);
            Assert.True(journal.Find(request.CommandId)!.Sent);
        }
    }
    [Fact]
    public async Task Timeout_terminal_is_frozen_and_stopped_notifications_are_not_suppressed()
    {
        var initial=WorkflowDispatchDeliveryFixture.SelectionFunctionInput();var source=initial with{State=initial.State with{DecisionRuntimeVersion=0}};
        var execute=source.State.SelectionDispatch!;var directory=DirectoryForTest();
        using(var journal=new WorkflowSelectionFunctionFailures(directory))
        {
            await using var state=Create(journal,source);state.Send=_=>ValueTask.FromException(new IOException("Timeout reply lost."));
            var failed=new TradeSelectionFunctionFailedEvent{CommandId=execute.CommandId,EntityId=execute.WorkflowEntityId,WorkflowId=execute.WorkflowId,
                InputWorkflowRevision=execute.InputWorkflowRevision,ReasonCode="TS.TIME.EXPIRED",ErrorDate=state.Time.At,InputPayloadSha256=execute.SelectionBinding.PayloadSha256};
            state.Context.RequestFunctionAsync<ExecuteTradeSelectionPipelineCommand,TradeSelectionExecutionId,FunctionResult<TradeSelectionFunctionCompletedEvent,TradeSelectionFunctionFailedEvent>>(
                Arg.Any<ExecuteTradeSelectionPipelineCommand>(),Arg.Any<CancellationToken>()).Returns(new ServiceOk<FunctionResult<TradeSelectionFunctionCompletedEvent,TradeSelectionFunctionFailedEvent>>(
                    FunctionResult<TradeSelectionFunctionCompletedEvent,TradeSelectionFunctionFailedEvent>.Fail(failed)));
            await state.Start();Assert.False(await state.Deliver(source));var original=Assert.IsType<FailTradeSelectionCommand>(Assert.Single(state.Commands));Assert.Equal("TradeSelectionTimedOut",original.Failure.ErrorType);
            state.Time.At=state.Time.At.AddDays(1);Assert.False(await state.Deliver(source));Assert.Equal(original,state.Commands.Last());
            var count=state.Commands.Count;
            Assert.True(await state.Deliver(source with{Id=Guid.NewGuid(),State=source.State with{Status=WorkflowStrategyMachineStatus.Failed}}));Assert.Equal(count,state.Commands.Count);
            var reservation=source with{Id=Guid.NewGuid(),State=source.State with{CompositionHandoff=new(){Status=TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection.CompositionHandoffStatus.ReservationPending}}};
            Assert.True(await state.Deliver(reservation));Assert.Equal(count,state.Commands.Count);
        }
        using(var journal=new WorkflowSelectionFunctionFailures(directory))
        {
            await using var recovered=Create(journal,source);await recovered.Start();Assert.IsType<FailTradeSelectionCommand>(Assert.Single(recovered.Commands));
            Assert.True(await recovered.Deliver(source));Assert.Single(recovered.Commands);
        }
    }
    [Fact]
    public async Task Storage_and_request_integrity_fail_closed_and_confirmed_marker_resumes_without_send()
    {
        var source=WorkflowDispatchDeliveryFixture.SelectionFunctionInput();var request=source.State.SelectionDispatch!;var directory=DirectoryForTest();
        using(var journal=new WorkflowSelectionFunctionFailures(directory))
        {
            Assert.Throws<IOException>(()=>new WorkflowSelectionFunctionFailures(directory));
            await using var state=Create(journal,source);await state.Start();
            using(var locked=new FileStream(Path.Combine(directory,"pending",request.CommandId.ToString("N")+".wsf.tmp"),FileMode.Create,FileAccess.ReadWrite,FileShare.None))
            {Assert.False(await state.Deliver(source));Assert.Empty(state.Commands);}
            Assert.True(await state.Deliver(source));Assert.Single(state.Commands);
            var changed=request with{RequestedAtUtc=request.RequestedAtUtc.AddTicks(1)};
            Assert.False(await state.Deliver(source with{State=source.State with{SelectionDispatch=changed}}));Assert.Single(state.Commands);
        }
        var sent=Directory.GetFiles(Path.Combine(directory,"sent"),"*.wsf",SearchOption.AllDirectories).Single();var pending=Path.Combine(directory,"pending",Path.GetFileName(sent));File.Move(sent,pending);
        using(var journal=new WorkflowSelectionFunctionFailures(directory))
        {
            await using var state=Create(journal,source);await state.Start();Assert.Empty(state.Commands);Assert.Empty(journal.Pending());
        }
        var bytes=File.ReadAllBytes(sent);bytes[^1]^=1;File.WriteAllBytes(sent,bytes);
        using var corrupt=new WorkflowSelectionFunctionFailures(directory);Assert.Throws<InvalidDataException>(()=>corrupt.Find(request.CommandId));
    }
}
