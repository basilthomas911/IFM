using NSubstitute;
using TomasAI.IFM.UI.Rust.Bridge.Net;
using TomasAI.IFM.UI.Net.Models.Operations;
using TomasAI.IFM.UI.Net.Services.Operations;
using TomasAI.IFM.UI.Net.ViewModels.App;
using TomasAI.IFM.Domain.Supervisor.Shared.Enums;
using TomasAI.IFM.Domain.Supervisor.Shared.ServiceApi;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
public sealed class ActorHealthBridgeTests
{
 static ActorHealthSnapshot Snapshot()=>new(){ObservedUtc=DateTime.UtcNow,ManualMutationEnabled=true,SupervisorAuthorityState="Available",Actors=[new(){ActorId=new(){Name="FixtureActor",ActorType=ActorType.Event},Domain="FixtureDomain",Mailboxes=[new(){ThreadId=new(){Name="FixtureActor",ActorType=ActorType.Event,EntityId="fixture"},Generation=7,QueueDepth=2,IsAdmissionOpen=true}]}]};
 static SupervisorControlOptions Options()=>new(){Enabled=true,Requester="Fixture.Ui",Timeout=TimeSpan.FromSeconds(1)};
 [Fact]public async Task Exact_mailbox_generation_and_reason_are_required_and_shared_dispatch_preserves_identity()
 {
  var calls=0;var api=Substitute.For<ISupervisorCommandApi>();api.ExecuteActorOperationAsync(Arg.Any<ActorThreadId>(),7,SupervisorActorOperationKind.Pause,"Fixture.Ui","fixture reason",TimeSpan.FromSeconds(1),Arg.Any<CancellationToken>()).Returns(_=>{calls++;return ValueTask.FromResult<ServiceResult<GuidResult>>(new ServiceOk<GuidResult>(new(Guid.NewGuid())));});
  var model=new ActorHealthControlModel(api,Options());var snapshot=Snapshot();var target=new ActorHealthThreadId(){Name="FixtureActor",ActorType=ActorType.Event,EntityId="fixture"};
  Assert.False(model.CanExecute(snapshot,target,""));await Assert.ThrowsAsync<InvalidOperationException>(()=>model.ExecuteAsync(snapshot,target,6,SupervisorActorOperationKind.Pause,"fixture reason"));
  Assert.Contains("accepted",await model.ExecuteAsync(snapshot,target,7,SupervisorActorOperationKind.Pause,"fixture reason"));Assert.Equal(1,calls);
  Assert.False(model.CanExecute(snapshot with{SupervisorAuthorityState="HostRecoveryRequired"},target,"fixture reason"));
 }
 [Fact]public async Task Explorer_preserves_nested_metrics_and_clears_targets_after_failed_refresh()
 {
  var query=Substitute.For<IActorHealthQueryService>();query.GetAsync(Arg.Any<DateTime>(),Arg.Any<DateTime>(),Arg.Any<CancellationToken>()).Returns(UiOperationResult<ActorHealthSnapshot>.Success(Snapshot()),UiOperationResult<ActorHealthSnapshot>.Failure(1,"Fixture query failed"));
  await using var session=new ActorHealthSession(query,Substitute.For<ISupervisorCommandApi>(),Options());var ready=await session.RefreshAsync();var row=Assert.Single(ready.Rows,x=>x.Fields["kind"]=="mailbox");Assert.Equal("7",row.Fields["Generation"]);Assert.Equal("fixture",row.Fields["ThreadId.EntityId"]);Assert.Equal("true",row.Fields["canExecute"]);
  var failed=await session.RefreshAsync();Assert.Empty(failed.Rows);Assert.Equal("false",failed.Context!["controls"]);await Assert.ThrowsAsync<InvalidOperationException>(()=>session.ExecuteAsync(new(1,Guid.NewGuid(),"actor-health","supervise",0,row.Id,new(){["operation"]="Pause",["reason"]="fixture reason"})));
 }
 [Fact]public async Task Gateway_fences_supervisor_actions_to_current_view_generation_and_exact_row()
 {
  var gate=new ContractEditorGateway(_=>Task.FromResult(new ContractEditorResult("actor-health",[new("fixture",[])],[])));Assert.Equal(0,gate.Submit(new(1,Guid.NewGuid(),"actor-health","load",0,null,[])));await gate.DrainAsync();var current=gate.Snapshot();var intent=new ContractEditorIntent(1,Guid.NewGuid(),"actor-health","supervise",current.Generation,"fixture",[]);Assert.Equal(-5,gate.Submit(intent with{ExpectedGeneration=current.Generation-1}));Assert.Equal(-5,gate.Submit(intent with{OriginalId="missing"}));Assert.Equal(-1,gate.Submit(intent with{View="operations-health"}));
 }
}
