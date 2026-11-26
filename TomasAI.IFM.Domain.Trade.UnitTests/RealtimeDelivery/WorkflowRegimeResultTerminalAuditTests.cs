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
using TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.MarketCondition;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Realtime.Actor;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.Trade.UnitTests.RealtimeDelivery;
public sealed class WorkflowRegimeResultTerminalAuditTests
{
    static string DirectoryForTest()=>Path.Combine(Path.GetTempPath(),"ifm-regime-function",Guid.NewGuid().ToString("N"));
    static WorkflowDispatchDeliveryFixture Create(WorkflowRegimeFunctionFailures journal,WorkflowStrategyStateUpdatedEvent source,bool metrics=false)
    {
        var state=new WorkflowDispatchDeliveryFixture(metrics,regimeFunctionFailures:journal);
        state.BindRegimeInputs(source);state.ReadCurrent=_=>Task.FromResult<IntrinsicTimeStrategyWorkflowView?>(source.State);return state;
    }
    static WorkflowStrategyStateUpdatedEvent RealSource()
    {
        var initial=WorkflowDispatchDeliveryFixture.RegimeFunctionInput(representative:true);
        var source=initial with{DispatchRegimeDiscovery=true,State=initial.State with{TriggerEvent=initial.State.TriggerEvent with{CreatedOn=initial.UpdatedAtUtc.AddSeconds(-1),ReceivedOn=initial.UpdatedAtUtc.AddSeconds(-1)}}};
        return MessageOrigins.Restore(source,new MessageOrigin(new DateTimeOffset(source.UpdatedAtUtc),"regime-test",Guid.NewGuid(),Guid.NewGuid()));
    }
    static Guid Key(WorkflowStrategyStateUpdatedEvent source)=>IntrinsicTimeStrategyWorkflowRealtimeActor.DeterministicPipelineCommandId(source.WorkflowId,source.State.CurrentStage,source.WorkflowRevision);
    [Theory][InlineData(false,"send")][InlineData(true,"send")][InlineData(false,"typed")][InlineData(true,"typed")][InlineData(false,"lost")][InlineData(true,"lost")]
    public async Task Real_request_function_failure_preserves_terminal_across_retry_and_cold_start(bool metrics,string mode)
    {
        var source=RealSource();var directory=DirectoryForTest();var request=source;
        var artifact="C:/repos/IFM/.artifacts/realtime-mailboxes/20261125-safety/workflow-regime-function-real-source.msgpack";
        Directory.CreateDirectory(Path.GetDirectoryName(artifact)!);File.WriteAllBytes(artifact,MessagePackSerializer.Serialize(source));
        FailRegimeDiscoveryCommand original;MessageOrigin? originalOrigin;byte[]? accepted=null;
        using(var journal=new WorkflowRegimeFunctionFailures(directory))
        {
            await using var state=Create(journal,source,metrics);state.TypedRejected=mode=="typed";
            state.Send=message=>
            {
                var bytes=MessagePackSerializer.Serialize((FailRegimeDiscoveryCommand)message);
                if(accepted is not null)Assert.Equal(accepted,bytes);else accepted=bytes;
                return mode=="typed"?ValueTask.CompletedTask:ValueTask.FromException(new IOException("Original terminal not confirmed."));
            };
            state.Time.At=source.UpdatedAtUtc;await state.Start();Assert.False(await state.Deliver(source));
            original=Assert.IsType<FailRegimeDiscoveryCommand>(Assert.Single(state.Commands));Assert.Equal(original,Assert.Single(journal.Pending()).Command);
            Assert.Equal(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(MessagePackSerializer.Serialize(request))),journal.Find(Key(request))!.InputHash);
            originalOrigin=MessageOrigins.Get(original);Assert.NotNull(originalOrigin);Assert.Equal(original.SourceEventId,originalOrigin.CausationId);
            Assert.Equal(MessageOrigins.Get(request)!.RunId,originalOrigin.RunId);Assert.Equal(MessageOrigins.Get(request)!.Generation,originalOrigin.Generation);
            Assert.NotEqual(source.Id,original.SourceEventId);Assert.Contains("FunctionRequest",original.Failure.ErrorData);
            Assert.Equal(source.State.RegimeDiscoveryParameterSet.ParameterSetId,original.ParameterSetId);Assert.Equal(source.State.RegimeDiscoveryParameterPayloadSha256,original.ParameterPayloadSha256);
            var preparations=state.RegimeConfigurationReads;var captures=state.RegimeCaptures;Assert.Equal(1,preparations);Assert.Equal(1,captures);
            var reads=state.Reads.Count;state.Time.At=state.Time.At.AddDays(1);
            Assert.False(await state.Deliver(source));Assert.Equal(reads,state.Reads.Count);Assert.Equal(original,state.Commands.Last());Assert.Equal(preparations,state.RegimeConfigurationReads);Assert.Equal(captures,state.RegimeCaptures);
        }
        using(var journal=new WorkflowRegimeFunctionFailures(directory))
        {
            await using var recovered=Create(journal,source);recovered.Time.At=recovered.Time.At.AddDays(2);
            await recovered.Start();Assert.Equal(original,Assert.Single(recovered.Commands));Assert.Empty(journal.Pending());Assert.Empty(recovered.Reads);Assert.Equal(0,recovered.RegimeCaptures);Assert.Equal(0,recovered.RegimeConfigurationReads);
            Assert.Equal(originalOrigin,MessageOrigins.Get(recovered.Commands[0]));
            Assert.True(await recovered.Deliver(source));Assert.Single(recovered.Commands);Assert.Empty(recovered.Reads);
            Assert.True(journal.Find(Key(request))!.Sent);
        }
    }
    [Fact]
    public async Task Timeout_terminal_is_frozen_and_stopped_notifications_are_not_suppressed()
    {
        var originalSource=WorkflowDispatchDeliveryFixture.RegimeFunctionInput();
        var source=originalSource with{State=originalSource.State with{DecisionRuntimeVersion=0}};var directory=DirectoryForTest();
        FailRegimeDiscoveryCommand original;
        using(var journal=new WorkflowRegimeFunctionFailures(directory))
        {
            await using var state=Create(journal,source);
            state.Context.RequestFunctionAsync<ExecuteRegimeDiscoveryPipelineCommand,TomasAI.IFM.Domain.Trade.Shared.RegimeDiscoveryExecutionEntityId,FunctionResult<RegimeDiscoveryPipelineCompletedEvent,RegimeDiscoveryPipelineFailedEvent>>(
                Arg.Any<ExecuteRegimeDiscoveryPipelineCommand>(),Arg.Any<CancellationToken>()).Returns(call=>
                {
                    var request=call.Arg<ExecuteRegimeDiscoveryPipelineCommand>();state.Time.At=request.ExpiresAtUtc;
                    return new ServiceOk<FunctionResult<RegimeDiscoveryPipelineCompletedEvent,RegimeDiscoveryPipelineFailedEvent>>(
                        TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Function.Actor.RegimeDiscoveryFunctionActor.MapEvent(new(typeof(RegimeDiscoveryPipelineFailedEvent),request,
                        new TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Function.RegimeDiscoveryExecutionFailed(state.Time.At,"Expired","Timeout",23103,[],Guid.Empty)),state.Time));
                });
            state.Send=_=>ValueTask.FromException(new IOException("Timeout reply lost."));
            await state.Start();Assert.False(await state.Deliver(source));original=Assert.IsType<FailRegimeDiscoveryCommand>(Assert.Single(state.Commands));
            Assert.Equal(23103,original.Failure.ErrorCode);Assert.Equal("Timeout",original.Failure.ErrorType);
            state.Time.At=state.Time.At.AddDays(1);Assert.False(await state.Deliver(source));Assert.Equal(original,state.Commands.Last());
            var count=state.Commands.Count;
            Assert.True(await state.Deliver(source with{Id=Guid.NewGuid(),State=source.State with{Status=WorkflowStrategyMachineStatus.Failed}}));Assert.Equal(count,state.Commands.Count);
        }
        using(var journal=new WorkflowRegimeFunctionFailures(directory))
        {
            await using var recovered=Create(journal,source);await recovered.Start();Assert.Equal(original,Assert.Single(recovered.Commands));
            Assert.True(await recovered.Deliver(source));Assert.Single(recovered.Commands);
        }
    }
    [Fact]
    public async Task Storage_and_request_integrity_fail_closed_and_confirmed_marker_resumes_without_send()
    {
        var source=WorkflowDispatchDeliveryFixture.RegimeFunctionInput();var request=source;var directory=DirectoryForTest();
        using(var journal=new WorkflowRegimeFunctionFailures(directory))
        {
            Assert.Throws<IOException>(()=>new WorkflowRegimeFunctionFailures(directory));
            await using var state=Create(journal,source);await state.Start();
            using(var locked=new FileStream(Path.Combine(directory,"pending",Key(request).ToString("N")+".wgf.tmp"),FileMode.Create,FileAccess.ReadWrite,FileShare.None))
            {Assert.False(await state.Deliver(source));Assert.Empty(state.Commands);}
            Assert.True(await state.Deliver(source));Assert.Single(state.Commands);
            var changed=request with{UpdatedAtUtc=request.UpdatedAtUtc.AddTicks(1)};
            Assert.False(await state.Deliver(changed));Assert.Single(state.Commands);
        }
        var sent=Directory.GetFiles(Path.Combine(directory,"sent"),"*.wgf",SearchOption.AllDirectories).Single();var pending=Path.Combine(directory,"pending",Path.GetFileName(sent));File.Move(sent,pending);
        using(var journal=new WorkflowRegimeFunctionFailures(directory))
        {
            await using var state=Create(journal,source);await state.Start();Assert.Empty(state.Commands);Assert.Empty(journal.Pending());
        }
        var bytes=File.ReadAllBytes(sent);bytes[^1]^=1;File.WriteAllBytes(sent,bytes);
        using var corrupt=new WorkflowRegimeFunctionFailures(directory);Assert.Throws<InvalidDataException>(()=>corrupt.Find(Key(request)));
    }
}
