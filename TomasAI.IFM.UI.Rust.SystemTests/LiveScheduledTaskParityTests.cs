using System.Diagnostics;
using System.Text.Json;
using FlaUI.UIA3;
using FlaUI.Core.AutomationElements;
using TomasAI.IFM.UI.Rust.Bridge.Net;
using TomasAI.IFM.UI.Net.Services.SystemAdmin;
using TomasAI.IFM.UI.Net.ViewModels.SystemAdmin;
using TomasAI.IFM.UI.Net.Views.SystemAdmin;
using NativeApp=FlaUI.Core.Application;
public sealed class LiveScheduledTaskParityTests
{
 [Fact] public void Live_both_hosts_show_the_same_schedule_and_logs_setup_tabs_without_executing_tasks()
 {
  using var http=new HttpClient{Timeout=TimeSpan.FromSeconds(5)};Assert.True(http.GetAsync("http://localhost:22543/health/launch-ready").GetAwaiter().GetResult().IsSuccessStatusCode);
  var baseline=Task.Run(async()=>{using var backend=new ScheduledTaskSession();return await backend.ExecuteAsync(new(1,Guid.NewGuid(),"scheduled-tasks","load",0,null,new(){["environment"]="Development",["hostId"]="development"}));}).GetAwaiter().GetResult();
  var directory=Environment.GetEnvironmentVariable("IFM_RUST_TEST_ARTIFACTS")??".artifacts/rust-ui/tests";Directory.CreateDirectory(directory);
  File.WriteAllText(Path.Combine(directory,"live-scheduled-task-parity.json"),JsonSerializer.Serialize(baseline));

  using var host=new DesktopViewTestHost(scope=>{
   var form=new System.Windows.Forms.Form{Width=1400,Height=850,Text="WinForms scheduled-task live parity"};
   var view=new ScheduledTasksView(new ScheduledTasksViewModel(scope.Container.GetInstance<IScheduledTaskService>())){Dock=System.Windows.Forms.DockStyle.Fill};form.Controls.Add(view);
   return (form,()=>view.CloseAsync().AsTask());
  });
  var old=Environment.GetEnvironmentVariable("IFM_RUST_UI_MODE");Environment.SetEnvironmentVariable("IFM_RUST_UI_MODE","live");
  try {
   using var wfAutomation=new UIA3Automation();var win=wfAutomation.FromHandle(host.Handle).AsWindow();
   var tabs=win.FindFirstDescendant(cf=>cf.ByAutomationId("ScheduledTaskTabs")).AsTab();Assert.Equal(new[]{"Logs","Setup"},tabs.TabItems.Select(t=>t.Name));tabs.TabItems[1].Select();
   var field=win.FindFirstDescendant(cf=>cf.ByAutomationId("ScheduledTaskName")).AsTextBox();
   var timer=Stopwatch.StartNew();while(field.Text.Length==0&&baseline.Rows.Length>0&&timer.Elapsed<TimeSpan.FromSeconds(20))Thread.Sleep(25);
   var displayed=field.Text;if(baseline.Rows.Length>0)Assert.Contains(baseline.Rows,r=>r.Fields["name"]==displayed);else Assert.Empty(displayed);
   win.Capture().Save(Path.Combine(directory,"live-scheduled-winforms.png"));
   var exe=Environment.GetEnvironmentVariable("IFM_RUST_UI_EXE")??Path.GetFullPath(".artifacts/rust-ui/app/ifm-ui-rust.exe");using var app=NativeApp.Launch(exe);using var nativeAutomation=new UIA3Automation();
   try{Window? window=null;timer.Restart();while(window is null&&timer.Elapsed<TimeSpan.FromSeconds(20)){var native=NativeWindows.Find(app.ProcessId);if(native!=IntPtr.Zero)window=nativeAutomation.FromHandle(native).AsWindow();Thread.Sleep(50);}Assert.NotNull(window);
    Wait(window,"Scheduled Tasks").AsButton().Invoke();WaitEnabled(window,"Refresh schedules").AsButton().Invoke();Wait(window,baseline.Context!["hostStatus"]);if(baseline.Rows.Length>0)WaitRoot(window,displayed);else Wait(window,"Load a task to view its recorded runs.");
    Wait(window,"Setup").AsButton().Invoke();Wait(window,"Save schedule");Wait(window,"Cron expression");window.Capture().Save(Path.Combine(directory,"live-scheduled-rust-setup.png"));
    Wait(window,"Logs").AsButton().Invoke();if(baseline.Rows.Length>0){var root=WaitRoot(window,displayed).AsButton();root.Invoke();Wait(window,"Load run history");root.Invoke();}else{Wait(window,"Load a task to view its recorded runs.");}window.Capture().Save(Path.Combine(directory,"live-scheduled-rust-logs.png"));
   }finally{if(!app.HasExited)app.Kill();}
  }finally{Environment.SetEnvironmentVariable("IFM_RUST_UI_MODE",old);}
 }
 static AutomationElement WaitEnabled(Window window,string name){var timer=Stopwatch.StartNew();while(timer.Elapsed<TimeSpan.FromSeconds(20)){var item=window.FindFirstDescendant(cf=>cf.ByName(name));if(item is {IsEnabled:true})return item;Thread.Sleep(50);}throw new TimeoutException("Disabled native control: "+name);}
 static AutomationElement WaitRoot(Window window,string name){var timer=Stopwatch.StartNew();while(timer.Elapsed<TimeSpan.FromSeconds(20)){var item=window.FindAllDescendants().FirstOrDefault(e=>(e.Properties.Name.ValueOrDefault??"").StartsWith(name+" | Scheduled UTC"));if(item is not null)return item;Thread.Sleep(50);}throw new TimeoutException("Missing task root: "+name);}
 static AutomationElement Wait(Window window,string name){var timer=Stopwatch.StartNew();while(timer.Elapsed<TimeSpan.FromSeconds(20)){var item=window.FindFirstDescendant(cf=>cf.ByName(name));if(item is not null)return item;Thread.Sleep(50);}var directory=Environment.GetEnvironmentVariable("IFM_RUST_TEST_ARTIFACTS")??".artifacts/rust-ui/tests";File.WriteAllLines(Path.Combine(directory,"scheduler-native-failure-tree.txt"),window.FindAllDescendants().Select(x=>$"{x.ControlType}: {x.Properties.Name.ValueOrDefault}"));window.Capture().Save(Path.Combine(directory,"scheduler-native-failure.png"));throw new TimeoutException("Missing native control: "+name);}
}
