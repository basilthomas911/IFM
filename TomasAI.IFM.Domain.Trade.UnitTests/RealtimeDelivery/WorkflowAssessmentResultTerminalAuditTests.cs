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
public sealed class WorkflowAssessmentResultTerminalAuditTests
{
    static string DirectoryForTest()=>Path.Combine(Path.GetTempPath(),"ifm-assessment-function",Guid.NewGuid().ToString("N"));
    static WorkflowDispatchDeliveryFixture Create(WorkflowAssessmentFunctionFailures journal,WorkflowStrategyStateUpdatedEvent source,bool metrics=false)
    {
        var state=new WorkflowDispatchDeliveryFixture(metrics,assessmentFunctionFailures:journal);
        state.ReadCurrent=_=>Task.FromResult<IntrinsicTimeStrategyWorkflowView?>(source.State);return state;
    }
    static WorkflowStrategyStateUpdatedEvent RealSource()
    {
        var execute=AssessmentFixture.Command();var initial=WorkflowDispatchDeliveryFixture.AssessmentFunctionInput();
        var view=execute.WorkflowView with{DecisionRuntimeVersion=1};
        return MessageOrigins.Restore(initial with{EntityId=view.EntityId,WorkflowId=view.WorkflowId,WorkflowRevision=view.WorkflowRevision,State=view},
            new MessageOrigin(new DateTimeOffset(execute.RequestedAtUtc),"assessment-test",Guid.NewGuid(),Guid.NewGuid()));
    }
    static Guid Key(WorkflowStrategyStateUpdatedEvent source)=>IntrinsicTimeStrategyWorkflowRealtimeActor.DeterministicPipelineCommandId(source.WorkflowId,source.State.CurrentStage,source.WorkflowRevision);
    [Theory][InlineData(false,"send")][InlineData(true,"send")][InlineData(false,"typed")][InlineData(true,"typed")][InlineData(false,"lost")][InlineData(true,"lost")]
    public async Task Real_request_function_failure_preserves_terminal_across_retry_and_cold_start(bool metrics,string mode)
    {
        var source=RealSource();var directory=DirectoryForTest();var request=source;
        var artifact="C:/repos/IFM/.artifacts/realtime-mailboxes/20261124-safety/workflow-assessment-function-real-source.msgpack";
        Directory.CreateDirectory(Path.GetDirectoryName(artifact)!);File.WriteAllBytes(artifact,MessagePackSerializer.Serialize(source));
        FailMarketConditionCommand original;MessageOrigin? originalOrigin;byte[]? accepted=null;
        using(var journal=new WorkflowAssessmentFunctionFailures(directory))
        {
            await using var state=Create(journal,source,metrics);state.TypedRejected=mode=="typed";
            state.Send=message=>
            {
                var bytes=MessagePackSerializer.Serialize((FailMarketConditionCommand)message);
                if(accepted is not null)Assert.Equal(accepted,bytes);else accepted=bytes;
                return mode=="typed"?ValueTask.CompletedTask:ValueTask.FromException(new IOException("Original terminal not confirmed."));
            };
            state.Time.At=source.UpdatedAtUtc;await state.Start();Assert.False(await state.Deliver(source));
            original=Assert.IsType<FailMarketConditionCommand>(Assert.Single(state.Commands));Assert.Equal(original,Assert.Single(journal.Pending()).Command);
            Assert.Equal(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(MessagePackSerializer.Serialize(request))),journal.Find(Key(request))!.InputHash);
            originalOrigin=MessageOrigins.Get(original);Assert.NotNull(originalOrigin);Assert.Equal(original.SourceEventId,originalOrigin.CausationId);
            Assert.Equal(MessageOrigins.Get(request)!.RunId,originalOrigin.RunId);Assert.Equal(MessageOrigins.Get(request)!.Generation,originalOrigin.Generation);
            var reads=state.Reads.Count;state.Time.At=state.Time.At.AddDays(1);
            Assert.False(await state.Deliver(source));Assert.Equal(reads,state.Reads.Count);Assert.Equal(original,state.Commands.Last());
        }
        using(var journal=new WorkflowAssessmentFunctionFailures(directory))
        {
            await using var recovered=Create(journal,source);recovered.Time.At=recovered.Time.At.AddDays(2);
            await recovered.Start();Assert.Equal(original,Assert.Single(recovered.Commands));Assert.Empty(journal.Pending());Assert.Empty(recovered.Reads);
            Assert.Equal(originalOrigin,MessageOrigins.Get(recovered.Commands[0]));
            Assert.True(await recovered.Deliver(source));Assert.Single(recovered.Commands);Assert.Empty(recovered.Reads);
            Assert.True(journal.Find(Key(request))!.Sent);
        }
    }
    [Fact]
    public async Task Timeout_terminal_is_frozen_and_stopped_notifications_are_not_suppressed()
    {
        var source=WorkflowDispatchDeliveryFixture.AssessmentFunctionInput();var directory=DirectoryForTest();
        FailMarketConditionCommand original;
        using(var journal=new WorkflowAssessmentFunctionFailures(directory))
        {
            await using var state=Create(journal,source);state.Time.At=source.State.ExpiresAtUtc.AddSeconds(1);
            state.Send=_=>ValueTask.FromException(new IOException("Timeout reply lost."));
            await state.Start();Assert.False(await state.Deliver(source));original=Assert.IsType<FailMarketConditionCommand>(Assert.Single(state.Commands));
            Assert.Equal(MarketConditionFailureCategory.Timeout,original.FailureCategory);
            state.Time.At=state.Time.At.AddDays(1);Assert.False(await state.Deliver(source));Assert.Equal(original,state.Commands.Last());
            var count=state.Commands.Count;
            Assert.True(await state.Deliver(source with{Id=Guid.NewGuid(),State=source.State with{Status=WorkflowStrategyMachineStatus.Failed}}));Assert.Equal(count,state.Commands.Count);
        }
        using(var journal=new WorkflowAssessmentFunctionFailures(directory))
        {
            await using var recovered=Create(journal,source);await recovered.Start();Assert.Equal(original,Assert.Single(recovered.Commands));
            Assert.True(await recovered.Deliver(source));Assert.Single(recovered.Commands);
        }
    }
    [Fact]
    public async Task Legacy_prepared_request_recovers_from_original_source_without_configuration_reload()
    {
        var prepared=RealSource();var source=MessageOrigins.Copy(prepared,prepared with{State=prepared.State with{AssessmentBinding=null}});
        var directory=DirectoryForTest();
        using(var journal=new WorkflowAssessmentFunctionFailures(directory))
        {
            await using var state=Create(journal,source);state.Time.At=source.UpdatedAtUtc;
            state.Send=_=>ValueTask.FromException(new IOException("Original outcome not confirmed."));await state.Start();
            await Assert.ThrowsAsync<IOException>(async()=>await IntrinsicTimeStrategyWorkflowRealtimeActor.ExecuteAssessmentAsync(state.Context,prepared,source));
            var original=Assert.IsType<FailMarketConditionCommand>(Assert.Single(state.Commands));
            Assert.Equal(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(MessagePackSerializer.Serialize(source))),journal.Find(Key(source))!.InputHash);
            state.Time.At=state.Time.At.AddDays(1);Assert.False(await state.Deliver(source));Assert.Equal(original,state.Commands.Last());Assert.Empty(state.Reads);
        }
        using(var journal=new WorkflowAssessmentFunctionFailures(directory))
        {
            await using var recovered=Create(journal,source);await recovered.Start();Assert.Single(recovered.Commands);Assert.True(await recovered.Deliver(source));Assert.Single(recovered.Commands);Assert.Empty(recovered.Reads);
        }
    }
    [Fact]
    public async Task Storage_and_request_integrity_fail_closed_and_confirmed_marker_resumes_without_send()
    {
        var source=WorkflowDispatchDeliveryFixture.AssessmentFunctionInput();var request=source;var directory=DirectoryForTest();
        using(var journal=new WorkflowAssessmentFunctionFailures(directory))
        {
            Assert.Throws<IOException>(()=>new WorkflowAssessmentFunctionFailures(directory));
            await using var state=Create(journal,source);await state.Start();
            using(var locked=new FileStream(Path.Combine(directory,"pending",Key(request).ToString("N")+".waf.tmp"),FileMode.Create,FileAccess.ReadWrite,FileShare.None))
            {Assert.False(await state.Deliver(source));Assert.Empty(state.Commands);}
            Assert.True(await state.Deliver(source));Assert.Single(state.Commands);
            var changed=request with{UpdatedAtUtc=request.UpdatedAtUtc.AddTicks(1)};
            Assert.False(await state.Deliver(changed));Assert.Single(state.Commands);
        }
        var sent=Directory.GetFiles(Path.Combine(directory,"sent"),"*.waf",SearchOption.AllDirectories).Single();var pending=Path.Combine(directory,"pending",Path.GetFileName(sent));File.Move(sent,pending);
        using(var journal=new WorkflowAssessmentFunctionFailures(directory))
        {
            await using var state=Create(journal,source);await state.Start();Assert.Empty(state.Commands);Assert.Empty(journal.Pending());
        }
        var bytes=File.ReadAllBytes(sent);bytes[^1]^=1;File.WriteAllBytes(sent,bytes);
        using var corrupt=new WorkflowAssessmentFunctionFailures(directory);Assert.Throws<InvalidDataException>(()=>corrupt.Find(Key(request)));
    }
}
