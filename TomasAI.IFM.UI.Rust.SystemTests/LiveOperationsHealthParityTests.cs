using System.Diagnostics;
using FlaUI.UIA3;
using FlaUI.Core.AutomationElements;
using TomasAI.IFM.UI.Rust.Bridge.Net;
using TomasAI.IFM.UI.Net.Services.MarketData;
using TomasAI.IFM.UI.Net.Views.App;
using NativeApp=FlaUI.Core.Application;
public sealed class LiveOperationsHealthParityTests
{
 [Fact]public void Live_both_health_views_render_current_backend_observations_and_metric_panels()
 {
  using var host=new DesktopViewTestHost(scope=>{var form=new MarketDataOperationsHealthForm(scope.Container.GetInstance<IMarketDataOperationsHealthQueryService>());return(form,()=>Task.CompletedTask);});
  using var wf=new UIA3Automation();var window=wf.FromHandle(host.Handle).AsWindow();var watch=Stopwatch.StartNew();AutomationElement? summary=null;
  while(watch.Elapsed<TimeSpan.FromSeconds(15)){summary=window.FindFirstDescendant(cf=>cf.ByAutomationId("operationsHealthSummary"));if(summary is not null&&summary.Name.StartsWith("Operations health:")&&!summary.Name.Contains("not been queried"))break;Thread.Sleep(50);}Assert.NotNull(summary);
  Wait(window,"Central processing stage health");window.FindFirstDescendant(cf=>cf.ByAutomationId("operationsHealthTabs")).AsTab().TabItems[1].Select();Wait(window,"Dataset worker and recovery health");
  var directory=Environment.GetEnvironmentVariable("IFM_RUST_TEST_ARTIFACTS")??".artifacts/rust-ui/tests";Directory.CreateDirectory(directory);window.Capture().Save(Path.Combine(directory,"live-health-winforms.png"));
  var old=Environment.GetEnvironmentVariable("IFM_RUST_UI_MODE");Environment.SetEnvironmentVariable("IFM_RUST_UI_MODE","live");
  try {var exe=Environment.GetEnvironmentVariable("IFM_RUST_UI_EXE")??Path.GetFullPath(".artifacts/rust-ui/app/ifm-ui-rust.exe");using var app=NativeApp.Launch(exe);using var automation=new UIA3Automation();try{
   Window? native=null;watch.Restart();while(native is null&&watch.Elapsed<TimeSpan.FromSeconds(20)){var handle=NativeWindows.Find(app.ProcessId);if(handle!=IntPtr.Zero)native=automation.FromHandle(handle).AsWindow();Thread.Sleep(50);}Assert.NotNull(native);
   Wait(native,"Market Data Operations Health").AsButton().Invoke();WaitEnabled(native,"Refresh status").AsButton().Invoke();WaitPrefix(native,"Operations health:");Wait(native,"Processing stages");Wait(native,"Dataset workers").AsButton().Invoke();native.Capture().Save(Path.Combine(directory,"live-health-rust.png"));Wait(native,"Processing stages").AsButton().Invoke();
  }finally{if(!app.HasExited)app.Kill();}}finally{Environment.SetEnvironmentVariable("IFM_RUST_UI_MODE",old);}
 }
 static AutomationElement WaitEnabled(Window window,string name)=>WaitFor(window,e=>e.Properties.Name.ValueOrDefault==name&&e.IsEnabled,name);
 static AutomationElement Wait(Window window,string name)=>WaitFor(window,e=>e.Properties.Name.ValueOrDefault==name,name);
 static AutomationElement WaitPrefix(Window window,string prefix)=>WaitFor(window,e=>(e.Properties.Name.ValueOrDefault??"").StartsWith(prefix),prefix);
 static AutomationElement WaitFor(Window window,Func<AutomationElement,bool> match,string text){var watch=Stopwatch.StartNew();while(watch.Elapsed<TimeSpan.FromSeconds(20)){var found=window.FindAllDescendants().FirstOrDefault(match);if(found is not null)return found;Thread.Sleep(50);}var dir=Environment.GetEnvironmentVariable("IFM_RUST_TEST_ARTIFACTS")??".artifacts/rust-ui/tests";File.WriteAllLines(Path.Combine(dir,"health-failure-tree.txt"),window.FindAllDescendants().Select(x=>$"{x.ControlType} | {x.Properties.AutomationId.ValueOrDefault} | {x.Properties.Name.ValueOrDefault}"));window.Capture().Save(Path.Combine(dir,"health-failure.png"));throw new TimeoutException("Missing UI control: "+text);}
}
