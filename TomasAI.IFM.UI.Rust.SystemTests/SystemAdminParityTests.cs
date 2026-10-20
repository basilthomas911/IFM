using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;
using NSubstitute;
using TomasAI.IFM.UI.Net.Models.Reference;
using TomasAI.IFM.UI.Net.Services.Operations;
using TomasAI.IFM.UI.Net.Services.Reference;
using TomasAI.IFM.UI.Net.Services.SystemAdmin;
using TomasAI.IFM.UI.Net.ViewModels.SystemAdmin;
using TomasAI.IFM.UI.Net.Views.SystemAdmin;

/// <summary>Read-only live FlaUI parity of every view reachable from the original WinForms System manager.</summary>
public sealed class SystemAdminParityTests
{
    [Fact]
    public void System_manager_preserves_backup_and_scheduler_tabs_controls_and_modal_navigation()
    {
        var root=Environment.GetEnvironmentVariable("IFM_RUST_REPO_ROOT")??throw new InvalidOperationException("Set IFM_RUST_REPO_ROOT");
        var executable=Environment.GetEnvironmentVariable("IFM_RUST_UI_EXE")??throw new InvalidOperationException("Set IFM_RUST_UI_EXE");
        var output=Path.Combine(root,".artifacts/system-admin-parity/flaui");Directory.CreateDirectory(output);
        var previous=SetThreadDpiAwarenessContext(new IntPtr(-4));
        try {
            using var host=new DesktopViewTestHost(scope=>{
                var form=new SystemAdminForm(scope.Container.GetInstance<IDatabaseBackupService>(),scope.Container.GetInstance<IScheduledTaskService>());
                var selectors=Substitute.For<IReferenceDataService>();
                selectors.GetSystemAdminFunctionTypesAsync(Arg.Any<CancellationToken>()).Returns(ValueTask.FromResult(UiOperationResult<IReadOnlyList<LookupTypeUiModel>>.Success([
                    new("SystemAdminFunctionTypes","BackupDatabases",0,"Backup Databases",DateTime.UtcNow,"Parity catalogue"),new("SystemAdminFunctionTypes","ScheduledTasks",1,"Scheduled Tasks",DateTime.UtcNow,"Parity catalogue")])));
                form.LoadViewModel(new SystemAdminViewModel(selectors));return(form,()=>Task.CompletedTask);
            });
            using var automation=new UIA3Automation{TransactionTimeout=TimeSpan.FromSeconds(3),ConnectionTimeout=TimeSpan.FromSeconds(3)};
            var original=automation.FromHandle(host.Handle).AsWindow();
            var start=new ProcessStartInfo(executable){WorkingDirectory=Path.GetDirectoryName(executable)!};start.Environment["IFM_RUST_UI_MODE"]="live";
            using var app=Process.Start(start)!;
            try {
                var watch=Stopwatch.StartNew();IntPtr handle;
                while((handle=NativeWindows.Find(app.Id))==IntPtr.Zero&&watch.Elapsed<TimeSpan.FromSeconds(45))Thread.Sleep(100);
                Assert.NotEqual(IntPtr.Zero,handle);var rust=automation.FromHandle(handle).AsWindow();rust.SetForeground();
                Click(Wait(rust,e=>e.Properties.Name.ValueOrDefault=="System"&&e.Properties.ControlType.ValueOrDefault==ControlType.Button,"System button"));
                var manager=Wait(rust,e=>e.Properties.Name.ValueOrDefault=="System Admin Manager","System manager");
                var initial=manager.BoundingRectangle;
                foreach(var (index,name)in new[]{(0,"Backup Databases"),(1,"Scheduled Tasks")}) {
                    host.Inspect(form=>{var selector=form.Controls.Find("ddlFunctionSelector",true).OfType<System.Windows.Forms.ComboBox>().Single();selector.SelectedIndex=index;return true;});
                    if(index>0){var selector=Wait(manager,e=>e.Properties.ControlType.ValueOrDefault==ControlType.ComboBox&&e.IsEnabled,"System selector");if(selector.Patterns.ExpandCollapse.IsSupported)selector.Patterns.ExpandCollapse.Pattern.Expand();else Click(selector);Click(Wait(rust,e=>e.Properties.Name.ValueOrDefault==name&&e.Properties.ControlType.ValueOrDefault!=ControlType.ComboBox,name));}
                    Wait(manager,e=>e.Properties.Name.ValueOrDefault=="Logs",name+" logs");
                    Capture(host,original,rust,manager,output,name+"-Logs");
                    host.Inspect(form=>{var tab=form.Controls.Find(index==0?"BackupTabs":"ScheduledTaskTabs",true).OfType<System.Windows.Forms.TabControl>().Single();tab.SelectedIndex=1;return true;});
                    Click(Wait(manager,e=>e.Properties.Name.ValueOrDefault=="Setup",name+" Setup"));
                    foreach(var label in index==0?new[]{"AWS Backup","Local Workstation","Request Backup","Load policy","Save policy","Restore Backup (fresh copy)"}:new[]{"Refresh","New","Save","Enable","Disable","Remove","Run Now","Preview","Resolve as Failed","Task:","Cron:","Time zone:","Review reason:"})
                        Wait(manager,e=>e.Properties.Name.ValueOrDefault==label,name+" "+label);
                    if(index==0){
                        var protection=Wait(manager,e=>e.Properties.Name.ValueOrDefault=="core-postgresql","loaded protection sets");
                        Wait(manager,e=>e.Properties.Name.ValueOrDefault=="Load policy"&&e.IsEnabled,"ready backup source");
                        Click(protection);Wait(manager,e=>e.Properties.Name.ValueOrDefault=="Request Backup"&&e.IsEnabled,"checked protection enables backup");
                        Click(Wait(manager,e=>e.Properties.Name.ValueOrDefault=="core-postgresql"&&e.IsEnabled,"clear checked protection"));
                        Wait(manager,e=>e.Properties.Name.ValueOrDefault=="Request Backup"&&!e.IsEnabled,"unchecked protection disables backup");
                    }
                    Assert.InRange(Math.Abs(manager.BoundingRectangle.Width-initial.Width),0,2);
                    Assert.InRange(Math.Abs(manager.BoundingRectangle.Height-initial.Height),0,2);
                    Capture(host,original,rust,manager,output,name+"-Setup");
                    if(index==0){host.Inspect(form=>{form.Controls.Find("BackupSourceSetupTabs",true).OfType<System.Windows.Forms.TabControl>().Single().SelectedIndex=0;return true;});Click(Wait(manager,e=>e.Properties.Name.ValueOrDefault=="AWS Backup"&&e.Properties.ControlType.ValueOrDefault==ControlType.Button&&e.IsEnabled,"AWS source"));Thread.Sleep(1000);Capture(host,original,rust,manager,output,"Backup-AWS-Setup");}
                    else {
                        Click(Wait(manager,e=>e.Properties.Name.ValueOrDefault=="Preview"&&e.IsEnabled,"Preview"));
                        Thread.Sleep(1500);Capture(host,original,rust,manager,output,"Scheduled-Preview");
                    }
                }
                Click(Wait(manager,e=>e.Properties.Name.ValueOrDefault=="Close window","close System"));
                Click(Wait(rust,e=>e.Properties.Name.ValueOrDefault=="System"&&e.Properties.ControlType.ValueOrDefault==ControlType.Button,"reopen System"));
                var reopened=Wait(rust,e=>e.Properties.Name.ValueOrDefault=="System Admin Manager","reopened manager");
                Click(Wait(reopened,e=>e.Properties.Name.ValueOrDefault=="Setup","reopened backup Setup"));
                Wait(reopened,e=>e.Properties.Name.ValueOrDefault=="Load policy"&&e.IsEnabled,"reopened backup remains functional");
            } finally {if(!app.HasExited){app.Kill(entireProcessTree:true);app.WaitForExit(10000);}}
        } finally {SetThreadDpiAwarenessContext(previous);}
    }
    [Fact]
    public void Every_system_action_button_dispatches_the_correct_intent_in_an_isolated_native_fixture()
    {
        var root=Environment.GetEnvironmentVariable("IFM_RUST_REPO_ROOT")??throw new InvalidOperationException("Set IFM_RUST_REPO_ROOT");
        var executable=Path.Combine(root,"TomasAI.IFM.UI.Rust/target/debug/examples/system_admin_actions.exe");
        var output=Path.Combine(root,".artifacts/system-admin-parity/flaui");Directory.CreateDirectory(output);
        var log=Path.Combine(output,"action-fixture.jsonl");File.WriteAllText(log,"");
        var start=new ProcessStartInfo(executable){WorkingDirectory=Path.GetDirectoryName(executable)!};start.Environment["IFM_SYSTEM_ACTION_LOG"]=log;
        using var app=Process.Start(start)!;using var automation=new UIA3Automation {TransactionTimeout=TimeSpan.FromSeconds(3),ConnectionTimeout=TimeSpan.FromSeconds(3)};
        try {
            var watch=Stopwatch.StartNew();IntPtr handle;while((handle=NativeWindows.Find(app.Id))==IntPtr.Zero&&watch.Elapsed<TimeSpan.FromSeconds(30))Thread.Sleep(100);
            Assert.NotEqual(IntPtr.Zero,handle);var window=automation.FromHandle(handle).AsWindow();window.SetForeground();
            var manager=Wait(window,e=>e.Properties.Name.ValueOrDefault=="System Admin Manager","fixture manager");
            void Button(string caption)=>Click(Wait(manager,e=>e.Properties.Name.ValueOrDefault==caption&&e.IsEnabled,caption));
            void Enter(string caption,string value){var field=Wait(manager,e=>e.Properties.Name.ValueOrDefault==caption&&e.Properties.ControlType.ValueOrDefault==ControlType.Edit&&e.IsEnabled,caption);field.Focus();Thread.Sleep(100);FlaUI.Core.Input.Keyboard.TypeSimultaneously(FlaUI.Core.WindowsAPI.VirtualKeyShort.CONTROL,FlaUI.Core.WindowsAPI.VirtualKeyShort.KEY_A);FlaUI.Core.Input.Keyboard.Type(value);Thread.Sleep(150);}
            Button("Setup");Button("core-postgresql");Button("Request Backup");Enter("Policy identifier","fixture-policy");Button("Load policy");Button("Save policy");Enter("Verified restore point","fixture-verified");Button("Restore Backup (fresh copy)");
            Button("Logs");Button("Load older runs");Button("2026");Button("10 October");Button("19 Monday");
            Click(Wait(manager,e=>(e.Properties.Name.ValueOrDefault??"").Contains("fixture-run"),"fixture backup run"));
            Click(Wait(manager,e=>(e.Properties.Name.ValueOrDefault??"").StartsWith("PostgreSQL |"),"PostgreSQL leaf"));
            Button("Cancel selected backup");Button("Refresh logs / output");Button("ScyllaDB | no identified operation");
            var selector=Wait(manager,e=>e.Properties.ControlType.ValueOrDefault==ControlType.ComboBox&&e.IsEnabled,"fixture System selector");
            if(selector.Patterns.ExpandCollapse.IsSupported)selector.Patterns.ExpandCollapse.Pattern.Expand();else Click(selector);
            Click(Wait(window,e=>e.Properties.Name.ValueOrDefault=="Scheduled Tasks"&&e.Properties.ControlType.ValueOrDefault!=ControlType.ComboBox,"fixture scheduler"));
            Button("Setup");Button("Save");Button("Enable");Button("Disable");Button("Run Now");
            Enter("Review reason:","Reviewed fixture interruption");Button("Resolve as Failed");
            Button("Remove");Button("New");Enter("Name:","New fixture schedule");
            var project=Wait(manager,e=>e.Properties.ControlType.ValueOrDefault==ControlType.ComboBox&&e.IsEnabled&&e.BoundingRectangle.Top>manager.BoundingRectangle.Top+120,"new task selector");
            if(project.Patterns.ExpandCollapse.IsSupported)project.Patterns.ExpandCollapse.Pattern.Expand();else Click(project);
            Click(Wait(window,e=>e.Properties.Name.ValueOrDefault=="Fixture task"&&e.Properties.ControlType.ValueOrDefault!=ControlType.ComboBox,"fixture task project"));
            Button("Save");Button("Preview");Button("Set cron from days/time");Button("Logs");Button("Refresh logs");
            Button("2026");Button("10");Button("19");
            Click(Wait(manager,e=>(e.Properties.Name.ValueOrDefault??"").EndsWith("| Uncertain"),"fixture task occurrence"));
            Button("Load more output");
            window.Capture().Save(Path.Combine(output,"action-fixture.png"));
            var actions=File.ReadAllLines(log).Select(line=>JsonDocument.Parse(line).RootElement.Clone()).ToArray();
            foreach(var expected in new[]{"backup-select","backup-run","backup-policy-load","backup-policy-save","backup-restore","backup-older","backup-output","backup-cancel","backup-refresh","backup-clear-output","change","enable","disable","run","resolve","remove","add","preview","output","more-output"})
                Assert.Contains(actions,a=>a.GetProperty("intent").GetString()==expected);
            Assert.Contains(actions,a=>a.GetProperty("intent").GetString()=="add"&&a.GetProperty("fields").GetProperty("name").GetString()=="New fixture schedule"&&a.GetProperty("fields").GetProperty("taskKey").GetString()=="fixture-task");
            Assert.Contains(actions,a=>a.GetProperty("intent").GetString()=="backup-restore"&&a.GetProperty("fields").GetProperty("restorePoint").GetString()=="fixture-verified");
            Assert.Contains(actions,a=>a.GetProperty("intent").GetString()=="backup-run"&&a.GetProperty("fields").GetProperty("mode").GetString()=="Full");
            Assert.Contains(actions,a=>a.GetProperty("intent").GetString()=="resolve"&&a.GetProperty("fields").GetProperty("reason").GetString()=="Reviewed fixture interruption");
        } finally {if(!app.HasExited){app.Kill(entireProcessTree:true);app.WaitForExit(10000);}}
    }
    static void Capture(DesktopViewTestHost host,Window original,Window rust,AutomationElement manager,string output,string name)
    {
        File.AppendAllText(Path.Combine(output,"progress.log"),name+"\n");
        original.SetForeground();Thread.Sleep(200);host.Inspect(form=>{using var bitmap=new System.Drawing.Bitmap(form.Width,form.Height);form.DrawToBitmap(bitmap,new(0,0,form.Width,form.Height));bitmap.Save(Path.Combine(output,"winforms-"+name+".png"));return true;});
        rust.SetForeground();Thread.Sleep(200);manager.Capture().Save(Path.Combine(output,"rust-"+name+".png"));
        var nodes=manager.FindAllDescendants().Select(e=>new{Name=e.Properties.Name.ValueOrDefault,Type=e.Properties.ControlType.ValueOrDefault.ToString(),e.IsEnabled,Bounds=e.BoundingRectangle.ToString()}).ToArray();
        File.WriteAllText(Path.Combine(output,"rust-"+name+".json"),JsonSerializer.Serialize(nodes,new JsonSerializerOptions{WriteIndented=true}));
    }
    static void Click(AutomationElement e){if(e.Patterns.Invoke.IsSupported)e.Patterns.Invoke.Pattern.Invoke();else e.Click();Thread.Sleep(200);}
    static AutomationElement Wait(AutomationElement root,Func<AutomationElement,bool> predicate,string description)
    {var deadline=DateTime.UtcNow.AddSeconds(35);while(DateTime.UtcNow<deadline){try{foreach(var e in root.FindAllDescendants()){try{if(predicate(e))return e;}catch(COMException){}}}catch(COMException){}Thread.Sleep(100);}throw new TimeoutException("Missing "+description);}
    [DllImport("user32.dll")]static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
}
