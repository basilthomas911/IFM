using System.Diagnostics;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;
using TomasAI.IFM.UI.Rust.Bridge.Net;
using TomasAI.IFM.UI.Net.Services.Operations;
using TomasAI.IFM.Domain.Supervisor.Shared.ServiceApi;
using TomasAI.IFM.UI.Net.Views.App;
using NativeApp=FlaUI.Core.Application;
public sealed class LiveActorHealthParityTests
{
 [Fact]public void Live_both_actor_explorers_expand_select_and_collapse_without_lifecycle_mutation()
 {
  var baseline=Task.Run(async()=>{await using var session=new ActorHealthSession();return await session.ExecuteAsync(new(1,Guid.NewGuid(),"actor-health","load",0,null,[]));}).GetAwaiter().GetResult();Assert.NotEmpty(baseline.Rows);
  using var host=new DesktopViewTestHost(scope=>{var form=new ActorHealthForm(scope.Container.GetInstance<IActorHealthQueryService>(),scope.Container.GetInstance<ISupervisorCommandApi>(),scope.Container.GetInstance<SupervisorControlOptions>());return(form,()=>Task.CompletedTask);});
  using var wf=new UIA3Automation();var window=wf.FromHandle(host.Handle).AsWindow();Wait(window,e=>e.Properties.AutomationId.ValueOrDefault=="actorHealthSummary"&&(e.Properties.Name.ValueOrDefault??"").Contains("Actors"),"WinForms health observation");
  var tree=window.FindFirstDescendant(cf=>cf.ByAutomationId("actorHealthTree")).AsTree();var domain=tree.Items.First(x=>x.Name.StartsWith("TomasAI.IFM.Domain."));domain.Expand();Assert.NotEmpty(domain.Items);var actor=domain.Items[0];actor.Expand();Assert.NotEmpty(actor.Items);var mailboxText=actor.Items[0].Name;actor.Items[0].Select();Assert.NotNull(window.FindFirstDescendant(cf=>cf.ByAutomationId("actorHealthDetails")));actor.Collapse();domain.Collapse();
  var dir=Environment.GetEnvironmentVariable("IFM_RUST_TEST_ARTIFACTS")!;window.Capture().Save(Path.Combine(dir,"live-actor-health-winforms.png"));
  var old=Environment.GetEnvironmentVariable("IFM_RUST_UI_MODE");Environment.SetEnvironmentVariable("IFM_RUST_UI_MODE","live");
  try{using var app=NativeApp.Launch(Environment.GetEnvironmentVariable("IFM_RUST_UI_EXE")!);using var nativeAutomation=new UIA3Automation();try{
   var watch=Stopwatch.StartNew();Window? native=null;while(native is null&&watch.Elapsed<TimeSpan.FromSeconds(20)){var handle=NativeWindows.Find(app.ProcessId);if(handle!=IntPtr.Zero)native=nativeAutomation.FromHandle(handle).AsWindow();Thread.Sleep(50);}Assert.NotNull(native);
   WaitName(native,"Actor Health").AsButton().Invoke();Wait(native,e=>e.Properties.Name.ValueOrDefault=="Refresh actor health"&&e.IsEnabled,"enabled refresh").AsButton().Invoke();Wait(native,e=>(e.Properties.Name.ValueOrDefault??"").Contains("Actors")&&(e.Properties.Name.ValueOrDefault??"").Contains("Observed"),"native health observation");
   var root=WaitName(native,domain.Name).AsButton();root.Invoke();var actorName=actor.Name;var start=actorName.IndexOf(' ');var bracket=actorName.LastIndexOf(" [");var name=actorName[(start+1)..bracket];var nativeActor=WaitName(native,name).AsButton();nativeActor.Invoke();var mailbox=mailboxText;var mailboxName=mailbox[(mailbox.IndexOf(' ')+1)..mailbox.LastIndexOf(" (")];WaitName(native,mailboxName).AsButton().Invoke();WaitName(native,"Generation");native.Capture().Save(Path.Combine(dir,"live-actor-health-rust.png"));nativeActor.Invoke();root.Invoke();
  }finally{if(!app.HasExited)app.Kill();}}finally{Environment.SetEnvironmentVariable("IFM_RUST_UI_MODE",old);}
 }
 static AutomationElement WaitName(Window w,string name)=>Wait(w,e=>e.Properties.Name.ValueOrDefault==name,name);
 static AutomationElement Wait(Window w,Func<AutomationElement,bool> predicate,string description){var watch=Stopwatch.StartNew();while(watch.Elapsed<TimeSpan.FromSeconds(20)){var found=w.FindAllDescendants().FirstOrDefault(predicate);if(found is not null)return found;Thread.Sleep(50);}var dir=Environment.GetEnvironmentVariable("IFM_RUST_TEST_ARTIFACTS")!;File.WriteAllLines(Path.Combine(dir,"actor-health-failure-tree.txt"),w.FindAllDescendants().Select(e=>$"{e.ControlType}: {e.Properties.Name.ValueOrDefault}"));w.Capture().Save(Path.Combine(dir,"actor-health-failure.png"));throw new TimeoutException("Missing Actor Health control: "+description);}
}
