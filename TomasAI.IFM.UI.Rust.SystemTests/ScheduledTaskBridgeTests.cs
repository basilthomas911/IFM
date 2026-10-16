using NSubstitute;
using TomasAI.IFM.UI.Rust.Bridge.Net;
using TomasAI.IFM.UI.Net.Models.SystemAdmin;
using TomasAI.IFM.UI.Net.Services.SystemAdmin;
using TomasAI.IFM.UI.Net.Services.Subscriptions;
using TomasAI.IFM.UI.Net.Services.Operations;
public class ScheduledTaskBridgeTests
{
 static readonly Guid Id=Guid.Parse("a256f7a9-b41c-4fb9-9ec3-eab3497bc86a"), RunId=Guid.Parse("b256f7a9-b41c-4fb9-9ec3-eab3497bc86a");
 static readonly ScheduledTaskScheduleUiModel Schedule=new("test-task","Fixture task","Development","development",false,"0 1 17 ? * MON-FRI","America/New_York",null,null,1800,60,"");
 static readonly ScheduledTaskDefinitionUiModel Definition=new(Id,7,7,Schedule,false,false,"Applied",7,"Verified",null);
 static readonly ScheduledTaskRunUiModel Run=new(RunId,DateTimeOffset.Parse("2026-10-13T21:01:00Z"),"Succeeded","Complete","Finished",DateTimeOffset.Parse("2026-10-13T21:01:02Z"),null,0,null);
 static IScheduledTaskService Service()
 {
  var service=Substitute.For<IScheduledTaskService>();service.CreateNotificationSubscription(Arg.Any<Func<Guid,ValueTask>>()).Returns(Substitute.For<IUiEventSubscription>());
  service.LoadAsync(Arg.Any<string>(),Arg.Any<string>(),Arg.Any<Guid?>(),Arg.Any<CancellationToken>()).Returns(UiOperationResult<ScheduledTaskDashboardUiModel>.Success(new([Definition],[new("test-task","Fixture task",true,"Windows","1")],[],"Ready",null)));
  service.LoadRunHistoryAsync(Arg.Any<string>(),Arg.Any<string>(),Id,Arg.Any<byte[]?>(),Arg.Any<CancellationToken>()).Returns(UiOperationResult<ScheduledTaskRunPageUiModel>.Success(new([Run],null)));
  service.LoadOutputAsync(Arg.Any<string>(),Arg.Any<string>(),Id,Run,Arg.Any<long>(),Arg.Any<CancellationToken>()).Returns(call=>UiOperationResult<ScheduledTaskOutputPageUiModel>.Success(call.ArgAt<long>(4)==0?new("first line\n",11,false,true):new("last line",20,true,true)));
  service.PreviewAsync(Arg.Any<ScheduledTaskScheduleUiModel>(),Arg.Any<CancellationToken>()).Returns(UiOperationResult<ScheduledTaskPreviewUiModel>.Success(new(true,[],[DateTimeOffset.Parse("2026-10-14T21:01:00Z")])));
  service.SetEnabledAsync(Id,7,true,Arg.Any<CancellationToken>()).Returns(UiOperationResult.Success());return service;
 }
 static ContractEditorIntent Intent(string action)=>new(1,Guid.NewGuid(),"scheduled-tasks",action,1,Id.ToString("D"),new(){["environment"]="Development",["hostId"]="development",["runId"]=RunId.ToString("D")});
 [Fact] public async Task Shared_model_preserves_task_revision_and_full_output_paging()
 {
  var service=Service();using var session=new ScheduledTaskSession(service);
  await session.ExecuteAsync(Intent("load"));var history=await session.ExecuteAsync(Intent("history"));Assert.Contains("Succeeded",MessagePack.MessagePackSerializer.ConvertToJson(history.Documents["runs"]));
  var first=await session.ExecuteAsync(Intent("output"));Assert.Equal("first line\n",first.Context!["output"]);Assert.Equal("False",first.Context["outputEnd"]);
  var final=await session.ExecuteAsync(Intent("more-output"));Assert.Equal("first line\nlast line",final.Context!["output"]);Assert.Equal("True",final.Context["outputEnd"]);
  await session.ExecuteAsync(Intent("enable"));await service.Received(1).SetEnabledAsync(Id,7,true,Arg.Any<CancellationToken>());
  Assert.DoesNotContain(service.ReceivedCalls(),x=>x.GetMethodInfo().Name==nameof(IScheduledTaskService.RunNowAsync));
 }
 [Fact] public async Task Selection_host_and_output_continuations_are_fenced()
 {
  using var session=new ScheduledTaskSession(Service());await session.ExecuteAsync(Intent("load"));
  await Assert.ThrowsAsync<InvalidOperationException>(()=>session.ExecuteAsync(Intent("enable") with{Fields=new(){["environment"]="Development",["hostId"]="different"}}));
  await Assert.ThrowsAsync<InvalidOperationException>(()=>session.ExecuteAsync(Intent("more-output")));
  await Assert.ThrowsAsync<InvalidOperationException>(()=>session.ExecuteAsync(Intent("enable") with{OriginalId=Guid.NewGuid().ToString("D")}));
 }
 [Fact] public async Task Native_action_gateway_rejects_stale_schedule_edits_and_cross_screen_commands()
 {
  var calls=0;var gate=new ContractEditorGateway(i=>{calls++;return Task.FromResult(new ContractEditorResult("scheduled-tasks",[new(Id.ToString("D"),[])],[]));});
  Assert.Equal(-1,gate.Submit(Intent("run") with{View="futures-contract"}));Assert.Equal(0,gate.Submit(Intent("load")));await gate.DrainAsync();
  Assert.Equal(-5,gate.Submit(Intent("enable")));var current=gate.Snapshot();var input=Intent("enable") with{ExpectedGeneration=current.Generation};
  Assert.Equal(0,gate.Submit(input));await gate.DrainAsync();Assert.Equal(0,gate.Submit(input));Assert.Equal(2,calls);
 }
}
