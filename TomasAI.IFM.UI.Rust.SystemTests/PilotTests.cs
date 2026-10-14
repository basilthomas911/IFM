using Application = FlaUI.Core.Application;
using FlaUI.Core;
using FlaUI.UIA3;
using FlaUI.Core.AutomationElements;
using Xunit;
using TomasAI.IFM.UI.Rust.Bridge.Net;
[assembly: CollectionBehavior(DisableTestParallelization=true)]
public class PilotTests
{
    [Fact] public void Projection_preserves_workflow_identity_and_separate_pipeline_roots()
    {
        using var adapter=new StrategyDetailsAdapter();var first=adapter.CreateFixture(0,1);var next=adapter.CreateFixture(0,2);var other=adapter.CreateFixture(1,3);
        Assert.Equal(first.WorkflowId,next.WorkflowId);Assert.NotEqual(first.WorkflowId,other.WorkflowId);Assert.Equal(2,next.Revision);
        Assert.Equal(new[]{"Workflow","ITI Signal","Workflow Pipeline"},first.Roots.Select(n=>n.Label));
        Assert.Equal(first.WorkflowId,first.Roots[0].Properties.Single(p=>p.Name=="WorkflowId").Value);
        Assert.Contains(first.Roots[2].Children,n=>n.Label=="Regime Discovery");
        Assert.DoesNotContain(first.Roots[0].Children,n=>n.Label=="RegimeDiscovery");
    }
    [Fact] public void Real_host_exposes_AccessKit_and_updates_from_managed_bridge()
    {
        var exe=Environment.GetEnvironmentVariable("IFM_RUST_UI_EXE")??Path.GetFullPath(".artifacts/rust-ui/app/ifm-ui-rust.exe");
        Assert.True(File.Exists(exe),"Build pilot and set IFM_RUST_UI_EXE before desktop testing: "+exe);
        var metrics=Environment.GetEnvironmentVariable("IFM_RUST_TEST_ARTIFACTS");if(metrics is not null){Directory.CreateDirectory(metrics);Environment.SetEnvironmentVariable("IFM_RUST_UI_METRICS",Path.Combine(metrics,"native-frame-metrics.json"));}
        using var app=Application.Launch(exe);using var nativeProcess=System.Diagnostics.Process.GetProcessById(app.ProcessId);using var automation=new UIA3Automation();
        try
        {
            Window? window=null;var deadline=DateTime.UtcNow.AddSeconds(30);while(window is null&&DateTime.UtcNow<deadline){nativeProcess.Refresh();if(app.HasExited)throw new InvalidOperationException("Rust host exited during startup");var handle=NativeWindows.Find(app.ProcessId);if(handle!=IntPtr.Zero)window=automation.FromHandle(handle).AsWindow();Thread.Sleep(50);}Assert.NotNull(window);window.Focus();
            var refresh=Wait(window,"Refresh fixture");Wait(window,"Ready");Wait(window,"Workflow Pipeline");
            var dump=string.Join(Environment.NewLine,window.FindAllDescendants().Select(e=>$"{e.ControlType}: {e.Properties.Name.ValueOrDefault}"));
            var artifacts=Environment.GetEnvironmentVariable("IFM_RUST_TEST_ARTIFACTS");if(artifacts is not null){Directory.CreateDirectory(artifacts);File.WriteAllText(Path.Combine(artifacts,"uia-tree.txt"),dump);}
            refresh.AsButton().Invoke();Wait(window,"Revision 2");
            Wait(window,"Workflow 2").AsButton().Invoke();Wait(window,"Revision 3");
            Wait(window,"ITI Signal").AsButton().Invoke();Wait(window,"Inspect ITI Signal");Wait(window,"ITI Signal").AsButton().Invoke();Absent(window,"Inspect ITI Signal");
            Wait(window,"Workflow").AsButton().Invoke();Wait(window,"Inspect Workflow");Wait(window,"Workflow").AsButton().Invoke();Absent(window,"Inspect Workflow");
            Wait(window,"Workflow Pipeline").AsButton().Invoke();Wait(window,"Regime Discovery");
            foreach(var name in new[]{"Regime Discovery","Market Condition","Trade Selection","Order Composition","Risk Management"})
            {
                Wait(window,name).AsButton().Invoke();Wait(window,"Inspect "+name);Wait(window,name).AsButton().Invoke();Absent(window,"Inspect "+name);
            }
            Wait(window,"Regime Discovery").AsButton().Invoke();Wait(window,"Inspect Regime Discovery").AsButton().Invoke();Wait(window,"ProcessingStatus");Wait(window,"Completed");
            if(artifacts is not null)window.Capture().Save(Path.Combine(artifacts,"strategy-details.png"));
            nativeProcess.Refresh();var before=new{WorkingSetBytes=nativeProcess.WorkingSet64,PrivateBytes=nativeProcess.PrivateMemorySize64};
            Wait(window,"Virtual Grid").AsButton().Invoke();Wait(window,"Rows: 100000 / 100000");Wait(window,"Row 1");
            Wait(window,"Last row").AsButton().Invoke();Wait(window,"Row 100000");
            Wait(window,"First row").AsButton().Invoke();Wait(window,"Row 1");
            if(artifacts is not null)window.Capture().Save(Path.Combine(artifacts,"virtual-grid.png"));
            nativeProcess.Refresh();if(artifacts is not null)File.WriteAllText(Path.Combine(artifacts,"native-memory.json"),System.Text.Json.JsonSerializer.Serialize(new{BeforeGrid=before,AfterGrid=new{WorkingSetBytes=nativeProcess.WorkingSet64,PrivateBytes=nativeProcess.PrivateMemorySize64}},new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
            for(var i=0;i<60;i++){Wait(window,"Last row").AsButton().Invoke();Wait(window,"Row 100000");Wait(window,"First row").AsButton().Invoke();Wait(window,"Row 1");}
            app.Close();var exitDeadline=DateTime.UtcNow.AddSeconds(10);while(!app.HasExited&&DateTime.UtcNow<exitDeadline)Thread.Sleep(50);Assert.True(app.HasExited);
        }
        finally{if(!app.HasExited)app.Kill();}
    }
    static void Absent(Window window,string name){Thread.Sleep(150);Assert.Null(window.FindFirstDescendant(cf=>cf.ByName(name)));}
    static AutomationElement Wait(Window window,string name)
    {
        var deadline=DateTime.UtcNow.AddSeconds(15);
        do{var found=window.FindFirstDescendant(cf=>cf.ByName(name));if(found is not null)return found;Thread.Sleep(50);}while(DateTime.UtcNow<deadline);
        var dump=string.Join(Environment.NewLine,window.FindAllDescendants().Select(e=>$"{e.ControlType}: {e.Properties.Name.ValueOrDefault}"));File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("IFM_RUST_TEST_ARTIFACTS")!,"failure-uia.txt"),dump);window.Capture().Save(Path.Combine(Environment.GetEnvironmentVariable("IFM_RUST_TEST_ARTIFACTS")!,"failure.png"));throw new TimeoutException("Missing UI Automation element: "+name+" Tree: "+dump);
    }
}


static class NativeWindows
{
 delegate bool Callback(IntPtr handle,IntPtr data);
 [System.Runtime.InteropServices.DllImport("user32.dll")]static extern bool EnumWindows(Callback callback,IntPtr data);
 [System.Runtime.InteropServices.DllImport("user32.dll")]static extern uint GetWindowThreadProcessId(IntPtr handle,out uint id);
 [System.Runtime.InteropServices.DllImport("user32.dll")]static extern bool IsWindowVisible(IntPtr handle);
 [System.Runtime.InteropServices.DllImport("user32.dll")]static extern bool GetWindowRect(IntPtr handle,out Rect rect);
 [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]struct Rect{public int Left,Top,Right,Bottom;}
 public static IntPtr Find(int process){IntPtr found=IntPtr.Zero;EnumWindows((handle,data)=>{GetWindowThreadProcessId(handle,out var id);if(id==(uint)process&&IsWindowVisible(handle)&&GetWindowRect(handle,out var r)&&r.Right-r.Left>500&&r.Bottom-r.Top>300){found=handle;return false;}return true;},IntPtr.Zero);return found;}
}
