using System.Reflection;
using System.Text.Json;
using MessagePack;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;
using TomasAI.IFM.UI.Net.Models.SystemAdmin;
using TomasAI.IFM.UI.Net.Services.SystemAdmin;
using TomasAI.IFM.UI.Net.Services.Operations;
using TomasAI.IFM.UI.Net.Services.Subscriptions;
using TomasAI.IFM.UI.Rust.Bridge.Net;
/// <summary>Stable backend data and unchanged hybrid projection oracles. No scheduler/host starts.</summary>
internal static class ScheduledFixtures {
 public static readonly Guid Id=Guid.Parse("a256f7a9-b41c-4fb9-9ec3-eab3497bc86a"),RunId=Guid.Parse("b256f7a9-b41c-4fb9-9ec3-eab3497bc86a"),UncertainId=Guid.Parse("c256f7a9-b41c-4fb9-9ec3-eab3497bc86a");
 public static readonly ScheduledTaskSchedule Schedule=new(){TaskKey="test-task",Name="Fixture task",Environment="Development",HostId="development",Timing=ScheduledTaskTiming.Cron,Expression="0 1 17 ? * MON-FRI",TimeZoneId="America/New_York",StartsAtUtc=new(2026,10,13,8,30,0,TimeSpan.FromHours(-4)),MaximumRuntimeSeconds=1800,DispatchToleranceSeconds=60,Description="Offline native scheduler"};
 public static readonly ScheduledTaskDefinition Definition=new(){Id=new(Id),Revision=7,DesiredRevision=7,Schedule=Schedule,Enabled=false,InstallationStatus=ScheduledTaskInstallationStatus.Applied,AppliedRevision=7,InstallationDetail="Verified"};
 public static readonly ScheduledTaskRun Run=new(){Id=new(RunId),ScheduleId=new(Id),Environment="Development",HostId="development",TaskKey="test-task",IntendedFireTimeUtc=new(2026,10,13,17,1,0,TimeSpan.FromHours(-4)),Status=ScheduledTaskRunStatus.Succeeded,Stage="Complete",Detail="Finished",StartedAtUtc=new(2026,10,13,17,1,2,TimeSpan.FromHours(-4)),ExitCode=0,Revision=3,StandardOutputTail="tail",StandardErrorTail="error tail",OutputDirectory="fixture/run"};
 public static readonly ScheduledTaskRun Uncertain=Run with{Id=new(UncertainId),Status=ScheduledTaskRunStatus.Uncertain,Revision=4,IntendedFireTimeUtc=Run.IntendedFireTimeUtc.AddDays(1)};
 public static readonly DateTimeOffset Fire=new(2026,10,26,21,1,0,TimeSpan.Zero);
 public static readonly ScheduledTasksDashboard Dashboard=new(){Schedules=[Definition],Catalog=new(){Environment="Development",HostId="development",Projects=[new(){TaskKey="test-task",DisplayName="Fixture task",Available=true,Platform="Windows",ManifestVersion="1"}],HostCapability=new(){Environment="Development",HostId="development",Platform="Windows",Ready=true,Detail="Verified",ObservedAtUtc=new(2026,10,26,12,30,0,TimeSpan.Zero)}},ReadAtUtc=new(2026,10,26,12,30,0,TimeSpan.Zero)};
 static ScheduledTaskRunUiModel MapRun(ScheduledTaskRun run)=>(ScheduledTaskRunUiModel)typeof(ScheduledTaskService).GetMethod("MapRun",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,[run])!;
 static readonly ScheduledTaskScheduleUiModel UiSchedule=new(Schedule.TaskKey,Schedule.Name,Schedule.Environment,Schedule.HostId,false,Schedule.Expression,Schedule.TimeZoneId,Schedule.StartsAtUtc,Schedule.EndsAtUtc,Schedule.MaximumRuntimeSeconds,Schedule.DispatchToleranceSeconds,Schedule.Description);
 public static async Task Export(string output){
  FixtureFiles.WriteAllText(Path.Combine(output,"scheduled-run-display.json"),JsonSerializer.Serialize(BridgeDocument.Project(MapRun(Run),true)));
  using var session=new ScheduledTaskSession(new Service());
  var result=await session.ExecuteAsync(new(1,Guid.Parse("9654d874-2ba9-4612-a14d-32f054352c3b"),"scheduled-tasks","load",0,null,new(){["environment"]="Development",["hostId"]="development"}));
  FixtureFiles.WriteAllText(Path.Combine(output,"scheduled-dashboard-display.json"),JsonSerializer.Serialize(new{rows=result.Rows,choices=result.Choices,context=result.Context}));
  foreach(var (name,bytes) in result.Documents)FixtureFiles.WriteAllBytes(Path.Combine(output,$"scheduled-document-{name}.msgpack"),bytes);
 }
 sealed class Subscription:IUiEventSubscription{public bool IsStarted{get;private set;}public ValueTask StartAsync(CancellationToken cancellationToken=default){IsStarted=true;return ValueTask.CompletedTask;}public ValueTask StopAsync(CancellationToken cancellationToken=default){IsStarted=false;return ValueTask.CompletedTask;}public ValueTask DisposeAsync()=>StopAsync();}
 sealed class Service:IScheduledTaskService{
  public ValueTask<UiOperationResult<ScheduledTaskDashboardUiModel>> LoadAsync(string environment,string hostId,Guid? selected,CancellationToken cancellationToken=default)=>ValueTask.FromResult(UiOperationResult<ScheduledTaskDashboardUiModel>.Success(new([new(Id,7,7,UiSchedule,false,false,"Applied",7,"Verified",null)],[new("test-task","Fixture task",true,"Windows","1")],[],"Windows: Ready - Verified",null)));
  public ValueTask<UiOperationResult<ScheduledTaskRunPageUiModel>> LoadRunHistoryAsync(string environment,string hostId,Guid scheduleId,byte[]? pagingState,CancellationToken cancellationToken=default)=>ValueTask.FromResult(UiOperationResult<ScheduledTaskRunPageUiModel>.Success(new([MapRun(Run)],[1])));
  public ValueTask<UiOperationResult<ScheduledTaskOutputPageUiModel>> LoadOutputAsync(string environment,string hostId,Guid scheduleId,ScheduledTaskRunUiModel run,long offset,CancellationToken cancellationToken=default)=>throw new NotSupportedException();
  public ValueTask<UiOperationResult> SaveAsync(Guid? id,long revision,ScheduledTaskScheduleUiModel schedule,CancellationToken cancellationToken=default)=>throw new NotSupportedException();
  public ValueTask<UiOperationResult> SetEnabledAsync(Guid id,long revision,bool enabled,CancellationToken cancellationToken=default)=>throw new NotSupportedException();
  public ValueTask<UiOperationResult> RemoveAsync(Guid id,long revision,CancellationToken cancellationToken=default)=>throw new NotSupportedException();
  public ValueTask<UiOperationResult> RunNowAsync(ScheduledTaskDefinitionUiModel definition,CancellationToken cancellationToken=default)=>throw new NotSupportedException();
  public ValueTask<UiOperationResult> ResolveUncertainAsync(ScheduledTaskRunUiModel run,string reason,CancellationToken cancellationToken=default)=>throw new NotSupportedException();
  public ValueTask<UiOperationResult<ScheduledTaskPreviewUiModel>> PreviewAsync(ScheduledTaskScheduleUiModel schedule,CancellationToken cancellationToken=default)=>ValueTask.FromResult(UiOperationResult<ScheduledTaskPreviewUiModel>.Success(new(true,[],[Fire])));
  public IUiEventSubscription CreateNotificationSubscription(Func<Guid,ValueTask> handler)=>new Subscription();
 }
}
