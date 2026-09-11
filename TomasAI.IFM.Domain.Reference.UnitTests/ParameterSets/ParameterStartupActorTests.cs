using System.Text.Json;
using FluentAssertions;
using MessagePack;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Domain.Reference.ParameterSets.Command.Actor;
using TomasAI.IFM.Domain.Reference.ParameterSets.Command.State;
using TomasAI.IFM.Domain.Reference.ParameterSets.Command.Extensions;
using TomasAI.IFM.Domain.Reference.ParameterSets.Model;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Reference.UnitTests.ParameterSets;
public sealed class ParameterStartupActorTests
{
 static (ParameterSetVersion Version,ParameterAssignmentCommandState Assignment) Assigned()
 {
  var value=RegimeDiscoveryParameterModel.CreateExplicitSeed(Guid.NewGuid());var json=ParameterCanonicalPayloadModel.Canonicalize(JsonSerializer.Serialize(value));
  var version=new ParameterSetVersion(new(value.ParameterSetId,1,RegimeDiscoveryParameterModel.ComponentCode,ParameterCanonicalPayloadModel.Hash(json)),"Daily","",value.SchemaVersion,ParameterVersionStatus.Published,json,DateTime.UtcNow,"test");
  var scope=WorkflowParameterScopeModel.Create(IntrinsicTimeStrategyWorkflowDefinition.Id,TimeFrameType.Daily);
  var assigned=ParameterAssignmentModel.Assign(scope,version,null,0,DateTime.UtcNow,"test");
  var command=new AssignParameterVersionCommand{CommandId=Guid.NewGuid(),EntityId=new(assigned.AssignmentId),Scope=scope,Reference=version.Reference};
  var state=new ParameterAssignmentCommandState();state.Update(new ParameterAssignmentChangedEvent{EntityId=command.EntityId,Revision=1,AssignmentJson=JsonSerializer.Serialize(assigned)},command).Should().BeTrue();
  return(version,state);
 }
 [Fact]public void Conflicting_startup_retry_is_rejected_before_reusing_the_audit_reservation()
 {
  var id=Guid.NewGuid();var command=new ApplySignalStartupPlanCommand{CommandId=id,RunId=id,EntityId=ParameterStartupEntityId.Registry};
  var json=JsonSerializer.Serialize(command);
  ParameterStartupOperationModel.ValidateDuplicate(command,command.CommandName,command.StreamId,json);
  Action changed=()=>ParameterStartupOperationModel.ValidateDuplicate(command with {ExpectedFingerprint="changed"},command.CommandName,command.StreamId,json);
  changed.Should().Throw<InvalidOperationException>().WithMessage("PARAM.OPERATION_IDENTITY_MISMATCH");
 }
 [Fact]public async Task Apply_freezes_exact_versions_and_release_removes_only_its_generation()
 {
  var data=Assigned();var sets=new ParameterSetCommandState();sets.Versions.Add(1,data.Version);
  var context=Substitute.For<IParameterStartupCommandContext>();var logger=Substitute.For<ILogger<ParameterStartupCommandActor>>();
  context.Assignments.LoadStateAsync(Arg.Any<ICommand>()).Returns(call=>new ValueTask<ParameterAssignmentCommandState>(
   ((AssignParameterVersionCommand)call.Arg<ICommand>()).Scope==data.Assignment.Assignment!.Scope?data.Assignment:new()));
  context.ParameterSets.LoadStateAsync(Arg.Any<ICommand>()).Returns(new ValueTask<ParameterSetCommandState>(sets));
  var state=new ParameterStartupCommandState();var runId=Guid.NewGuid();
  var command=new ApplySignalStartupPlanCommand{CommandId=runId,RunId=runId,EntityId=ParameterStartupEntityId.Registry};
  command=MessagePackSerializer.Deserialize<ApplySignalStartupPlanCommand>(MessagePackSerializer.Serialize(command));
  (await command.ExecuteAsync(context,state,logger)).Success.Should().BeTrue();state.Revision.Should().Be(1);
  var run=state.ActiveRuns[runId];
  var runtime=new ParameterRuntimeSnapshotModel(true);runtime.Apply(MessagePackSerializer.Deserialize<ParameterStartupRun>(MessagePackSerializer.Serialize(run)));
  runtime.Resolve(IntrinsicTimeStrategyWorkflowDefinition.Id,TimeFrameType.Daily).Applied!.Version.Reference.Should().Be(data.Version.Reference);
  sets.Versions[1]=data.Version with {Name="Changed after startup"};
  (await command.ExecuteAsync(context,state,logger)).Success.Should().BeTrue();state.Revision.Should().Be(1);
  state.ActiveRuns[runId].Versions.Single().Name.Should().Be("Daily");
  var release=new ReleaseSignalStartupPlanCommand{CommandId=Guid.NewGuid(),RunId=runId,EntityId=ParameterStartupEntityId.Registry};
  (await release.ExecuteAsync(context,state,logger)).Success.Should().BeTrue();state.ActiveRuns.Should().BeEmpty();state.Revision.Should().Be(2);
  (await release.ExecuteAsync(context,state,logger)).Success.Should().BeTrue();state.Revision.Should().Be(2);
 }
 [Fact]public async Task Retirement_after_assignment_removal_preserves_the_running_snapshot_without_shutdown()
 {
  var data=Assigned();var scope=data.Assignment.Assignment!;var runId=Guid.NewGuid();
  var snapshot=new ParameterStartupSnapshotModel(runId,[scope],new Dictionary<ParameterVersionRef,ParameterSetVersion>{{data.Version.Reference,data.Version}});
  var run=new ParameterStartupRun(runId,[scope],[data.Version],SignalStartupPlanModel.Create(snapshot,SignalStartupPlanModel.ExistingIntradayConsumers()),DateTime.UtcNow,"test");
  var startups=new ParameterStartupCommandState();startups.ActiveRuns.Add(runId,run);
  var context=Substitute.For<IParameterSetCommandContext>();
  context.Startups.LoadStateAsync(Arg.Any<ICommand>()).Returns(new ValueTask<ParameterStartupCommandState>(startups));
  context.Assignments.LoadStateAsync(Arg.Any<ICommand>()).Returns(new ValueTask<ParameterAssignmentCommandState>(new ParameterAssignmentCommandState()));
  var state=new ParameterSetCommandState();state.Versions.Add(1,data.Version);
  var retire=new RetireParameterVersionCommand{CommandId=Guid.NewGuid(),EntityId=new(data.Version.Reference.SetId),Version=1,ExpectedRevision=0};
  var runtime=new ParameterRuntimeSnapshotModel(true);runtime.Apply(run);
  (await retire.ExecuteAsync(context,state,Substitute.For<ILogger<ParameterSetCommandActor>>())).Success.Should().BeTrue();
  state.Versions[1].Status.Should().Be(ParameterVersionStatus.Retired);
  var captured=runtime.Resolve(IntrinsicTimeStrategyWorkflowDefinition.Id,TimeFrameType.Daily).Applied!.Version;
  captured.Should().Be(data.Version);
  captured.Status.Should().Be(ParameterVersionStatus.Published);
  await context.Startups.DidNotReceive().LoadStateAsync(Arg.Any<ICommand>());
 }
 [Fact]public async Task Repeated_startups_need_no_release_and_keep_a_bounded_recent_history()
 {
  var context=Substitute.For<IParameterStartupCommandContext>();
  context.Assignments.LoadStateAsync(Arg.Any<ICommand>()).Returns(_=>new ValueTask<ParameterAssignmentCommandState>(new ParameterAssignmentCommandState()));
  var state=new ParameterStartupCommandState();var first=Guid.Empty;var last=Guid.Empty;
  for(var index=0;index<105;index++)
  {
   var id=Guid.NewGuid();if(index==0)first=id;last=id;
   var command=new ApplySignalStartupPlanCommand{CommandId=id,RunId=id,EntityId=ParameterStartupEntityId.Registry};
   (await command.ExecuteAsync(context,state,Substitute.For<ILogger<ParameterStartupCommandActor>>())).Success.Should().BeTrue();
  }
  state.Revision.Should().Be(105);state.ActiveRuns.Count.Should().Be(100);
  state.ActiveRuns.Should().NotContainKey(first).And.ContainKey(last);
 }

 [Fact]public async Task Preparation_report_is_serializable_validated_and_replayed_without_changing_the_plan()
 {
  var id=Guid.NewGuid();var snapshot=new ParameterStartupSnapshotModel(id,[],new Dictionary<ParameterVersionRef,ParameterSetVersion>());
  var plan=SignalStartupPlanModel.Create(snapshot,SignalStartupPlanModel.ExistingIntradayConsumers());
  var run=new ParameterStartupRun(id,[],[],plan,DateTime.UtcNow,"test");
  var state=new ParameterStartupCommandState();state.ActiveRuns.Add(id,run);
  var report=new ParameterSignalStartupReport(id,plan.Fingerprint,new DateOnly(2026,9,10),"ES",DateTime.UtcNow,
   plan.Steps.Select(x=>new ParameterSignalPreparationOutcome(x.Key,ParameterSignalPreparationStatus.ExistingRoute,"Existing startup route; warmth unknown.")).ToArray());
  var command=new RecordSignalStartupReportCommand{CommandId=Guid.NewGuid(),RunId=id,Report=report,EntityId=ParameterStartupEntityId.Registry};
  command=MessagePackSerializer.Deserialize<RecordSignalStartupReportCommand>(MessagePackSerializer.Serialize(command));
  var context=Substitute.For<IParameterStartupCommandContext>();
  (await command.ExecuteAsync(context,state,Substitute.For<ILogger<ParameterStartupCommandActor>>())).Success.Should().BeTrue();
  state.Reports[id].Should().BeEquivalentTo(report);state.ActiveRuns[id].Should().Be(run);
  var changed=command with {Report=report with {ContractId="CHANGED"}};
  Action duplicate=()=>ParameterStartupOperationModel.ValidateDuplicate(changed,command.CommandName,command.StreamId,JsonSerializer.Serialize(command));
  duplicate.Should().Throw<InvalidOperationException>().WithMessage("PARAM.OPERATION_IDENTITY_MISMATCH");
  Action incomplete=()=>ParameterSignalStartupReportModel.Validate(run,report with {Outcomes=report.Outcomes.Skip(1).ToArray()});
  incomplete.Should().Throw<ArgumentException>().WithMessage("PARAM.STARTUP_REPORT_INVALID");
  Action wrongPlan=()=>ParameterSignalStartupReportModel.Validate(run,report with {PlanFingerprint="different"});
  wrongPlan.Should().Throw<ArgumentException>();
 }

}
