using System.Diagnostics;
using System.Text.Json;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Views.Reference.ParameterSets;
using TomasAI.IFM.UI.Rust.Bridge.Net;
using NativeApp=FlaUI.Core.Application;
public sealed class LiveParameterSetParityTests
{
 [Fact]public void Live_both_parameter_views_load_the_same_components_versions_and_readonly_tabs_without_writes()
 {
  var baseline=Task.Run(async()=>{await using var session=new ParameterSetSession();return await session.ExecuteAsync(new(1,Guid.NewGuid(),"parameter-sets","load",0,null,[]));}).GetAwaiter().GetResult();var components=JsonSerializer.Deserialize<ParameterComponentSummary[]>(MessagePack.MessagePackSerializer.ConvertToJson(baseline.Documents["components"]))!;Assert.NotEmpty(components);
  using var host=new DesktopViewTestHost(scope=>{var form=new System.Windows.Forms.Form{Width=1500,Height=950,Text="WinForms parameter-set parity"};var view=new ParameterSetsReferenceView(scope.Container.GetInstance<IParameterSetsApi>()){Dock=System.Windows.Forms.DockStyle.Fill};form.Controls.Add(view);form.Shown+=(_,_)=>((IControlCommand)view).Load(scope,_=>{});return(form,()=>{view.Unload();return Task.CompletedTask;});});
  using var wf=new UIA3Automation();var window=wf.FromHandle(host.Handle).AsWindow();var componentList=Wait(window,e=>e.Properties.Name.ValueOrDefault=="Parameter set components","WinForms component list").AsListBox();var watch=Stopwatch.StartNew();while(componentList.Items.Length==0&&watch.Elapsed<TimeSpan.FromSeconds(20))Thread.Sleep(50);Assert.Contains(componentList.Items,x=>x.Name==components[0].Name);
  var tabs=window.FindFirstDescendant(cf=>cf.ByControlType(FlaUI.Core.Definitions.ControlType.Tab)).AsTab();Assert.Contains(tabs.TabItems,x=>x.Name=="Signals");tabs.TabItems.Single(x=>x.Name=="Observations").Select();WaitName(window,"Regime Discovery observation requirements");tabs.TabItems.Single(x=>x.Name=="Calculation parameters").Select();WaitName(window,"Regime Discovery calculation parameters");tabs.TabItems.Single(x=>x.Name=="Validation").Select();
  var dir=Environment.GetEnvironmentVariable("IFM_RUST_TEST_ARTIFACTS")!;window.Capture().Save(Path.Combine(dir,"live-parameter-sets-winforms.png"));
  var old=Environment.GetEnvironmentVariable("IFM_RUST_UI_MODE");Environment.SetEnvironmentVariable("IFM_RUST_UI_MODE","live");try{using var app=NativeApp.Launch(Environment.GetEnvironmentVariable("IFM_RUST_UI_EXE")!);using var automation=new UIA3Automation();try{Window? native=null;watch.Restart();while(native is null&&watch.Elapsed<TimeSpan.FromSeconds(20)){var handle=NativeWindows.Find(app.ProcessId);if(handle!=IntPtr.Zero)native=automation.FromHandle(handle).AsWindow();Thread.Sleep(50);}Assert.NotNull(native);ReferenceManagerParityTests.OpenReference(native,"parameter sets");Wait(native,e=>e.Properties.Name.ValueOrDefault=="Refresh"&&e.IsEnabled,"enabled parameter refresh").AsButton().Invoke();WaitName(native,components[0].Name);
   if(baseline.Rows.Length>0){var selected=baseline.Rows[0];WaitName(native,$"{selected.Fields["name"]} - v{selected.Fields["version"]} ({selected.Fields["status"]})");}
   foreach(var name in new[]{"Signals","Observations","Calculation parameters","Validation"}){WaitName(native,name).AsButton().Invoke();}native.Capture().Save(Path.Combine(dir,"live-parameter-sets-rust.png"));
  }finally{if(!app.HasExited)app.Kill();}}finally{Environment.SetEnvironmentVariable("IFM_RUST_UI_MODE",old);}
 }
 static AutomationElement WaitName(Window w,string name)=>Wait(w,e=>e.Properties.Name.ValueOrDefault==name,name);
 static AutomationElement Wait(Window w,Func<AutomationElement,bool> predicate,string description){var watch=Stopwatch.StartNew();while(watch.Elapsed<TimeSpan.FromSeconds(20)){var found=w.FindAllDescendants().FirstOrDefault(predicate);if(found is not null)return found;Thread.Sleep(50);}var dir=Environment.GetEnvironmentVariable("IFM_RUST_TEST_ARTIFACTS")!;File.WriteAllLines(Path.Combine(dir,"parameter-failure-tree.txt"),w.FindAllDescendants().Select(e=>$"{e.Properties.ControlType.ValueOrDefault}: {e.Properties.Name.ValueOrDefault}"));w.Capture().Save(Path.Combine(dir,"parameter-failure.png"));throw new TimeoutException("Missing parameter control: "+description);}
}
