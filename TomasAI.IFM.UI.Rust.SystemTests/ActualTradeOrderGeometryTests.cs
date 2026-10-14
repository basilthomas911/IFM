using System.Diagnostics;
using System.Text.Json;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;
public sealed class ActualTradeOrderGeometryTests
{
    [Fact] public void Current_native_editor_matches_real_WinForms_menu_capture()
    {
        var root=Environment.GetEnvironmentVariable("IFM_RUST_REPO_ROOT")!;var output=Environment.GetEnvironmentVariable("IFM_RUST_TEST_ARTIFACTS")!;
        using var reference=JsonDocument.Parse(File.ReadAllText(Path.Combine(output,"actual-trade-editor-geometry-before.json")));
        using var state=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,".artifacts/rust-ui/development-ui-processes.json")));
        using var dpi=new DpiScope();using var automation=new UIA3Automation();
        var handle=Find(state.RootElement.GetProperty("RustProcessId").GetInt32(),"Investment Fund Manager",true);Assert.NotEqual(IntPtr.Zero,handle);
        var rust=automation.FromHandle(handle).AsWindow();ShowWindow(handle,3);rust.Focus();Thread.Sleep(500);
        var originalMain=System.Text.RegularExpressions.Regex.Matches(reference.RootElement.GetProperty("RustMain").GetString()!,@"-?\d+").Select(x=>double.Parse(x.Value,System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        Assert.Equal(originalMain[0],rust.BoundingRectangle.X);Assert.Equal(originalMain[1],rust.BoundingRectangle.Y);Assert.Equal(originalMain[2],rust.BoundingRectangle.Width);
        var parentHeightChange=rust.BoundingRectangle.Height-originalMain[3];
        WaitName(rust,"Trade Orders").AsButton().Invoke();var editor=WaitName(rust,"Trade Order Editor");Thread.Sleep(300);
        var bounds=editor.BoundingRectangle;var numbers=System.Text.RegularExpressions.Regex.Matches(reference.RootElement.GetProperty("WinFormsEditor").GetString()!,@"-?\d+").Select(x=>double.Parse(x.Value,System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        // CenterParent moves the editor by half a change in parent height.
        numbers[1]+=parentHeightChange/2;
        var actual=new[]{bounds.X,bounds.Y,bounds.Width,bounds.Height};
        File.WriteAllText(Path.Combine(output,"actual-trade-editor-geometry-after.json"),JsonSerializer.Serialize(new{WinForms=reference.RootElement.GetProperty("WinFormsEditor").GetString(),Rust=bounds.ToString(),Differences=actual.Select((v,i)=>v-numbers[i]).ToArray()},new JsonSerializerOptions{WriteIndented=true}));
        rust.Capture().Save(Path.Combine(output,"actual-trade-editor-rust-after.png"));
        for(var i=0;i<4;i++)Assert.InRange(Math.Abs(actual[i]-numbers[i]),0,3);
    }
    // Interactive diagnostic collector: opening WinForms modal dialogs through UIA can
    // block Invoke and OS focus can consume mouse activation. Keep reference acquisition
    // explicit; the automated regression above compares against the actual captured bounds.
    public void Measure_editor_opened_from_both_actual_main_menus()
    {
        var root=Environment.GetEnvironmentVariable("IFM_RUST_REPO_ROOT")!;
        var output=Environment.GetEnvironmentVariable("IFM_RUST_TEST_ARTIFACTS")!;
        using var state=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,".artifacts/rust-ui/development-ui-processes.json")));
        using var dpi=new DpiScope();
        using var wfProcess=Process.GetProcessById(state.RootElement.GetProperty("WinFormsProcessId").GetInt32());
        using var rustProcess=Process.GetProcessById(state.RootElement.GetProperty("RustProcessId").GetInt32());
        var previousEditor=Find(wfProcess.Id,"Trade Orders",false);
        if(previousEditor!=IntPtr.Zero){PostMessage(previousEditor,0x0010,IntPtr.Zero,IntPtr.Zero);var closeWatch=Stopwatch.StartNew();while(Find(wfProcess.Id,"Trade Orders",false)!=IntPtr.Zero && closeWatch.Elapsed<TimeSpan.FromSeconds(10))Thread.Sleep(100);}
        using var automation=new UIA3Automation();var wf=automation.FromHandle(Find(wfProcess.Id,"Investment Fund Manager",true)).AsWindow();var rust=automation.FromHandle(Find(rustProcess.Id,"Investment Fund Manager",true)).AsWindow();
        
        ShowWindow(wf.Properties.NativeWindowHandle.ValueOrDefault,3);ShowWindow(rust.Properties.NativeWindowHandle.ValueOrDefault,6);
        SetForegroundWindow(wf.Properties.NativeWindowHandle.ValueOrDefault);wf.Focus();Thread.Sleep(500);var button=WaitName(wf,"Trade Orders");File.WriteAllText(Path.Combine(output,"actual-trade-menu-bounds.txt"),button.BoundingRectangle.ToString());var click=button.BoundingRectangle;SetCursorPos((int)(click.X+click.Width/2),(int)(click.Y+click.Height/2));
        FlaUI.Core.Input.Mouse.Down(FlaUI.Core.Input.MouseButton.Left);FlaUI.Core.Input.Mouse.Up(FlaUI.Core.Input.MouseButton.Left);
        using var editorAutomation=new UIA3Automation();
        var watch=Stopwatch.StartNew();Window? editor=null;
        while(watch.Elapsed<TimeSpan.FromSeconds(30)){
            var handle=Find(wfProcess.Id,"Trade Orders",false);if(handle!=IntPtr.Zero){editor=editorAutomation.FromHandle(handle).AsWindow();break;}Thread.Sleep(100);
        }
        
        Assert.NotNull(editor);wf=editorAutomation.FromHandle(Find(wfProcess.Id,"Investment Fund Manager",true)).AsWindow();editor.Focus();Thread.Sleep(500);
        var wfBounds=editor.BoundingRectangle;var wfMain=wf.BoundingRectangle;
        var ids=new[]{"marketViewSplitter","tabTradeBlotter","operationViewSplitter"};
        var wfPanes=ids.Select(id=>new{id,bounds=wf.FindFirstDescendant(cf=>cf.ByAutomationId(id))?.BoundingRectangle.ToString()}).ToArray();
        editor.Capture().Save(Path.Combine(output,"actual-trade-editor-winforms.png"));
        var wfControls=editor.FindAllDescendants().Where(e=>e.Properties.Name.ValueOrDefault is "Trade Orders" or "Trade Blotter" || e.Properties.AutomationId.ValueOrDefault is "pnlContentFrame" or "pnlTradeOrders" or "pnlTrades" or "tradeOrderWorkspaceTabs").Select(e=>new{name=e.Properties.Name.ValueOrDefault,id=e.Properties.AutomationId.ValueOrDefault,bounds=e.BoundingRectangle.ToString()}).ToArray();
        PostMessage(editor.Properties.NativeWindowHandle.ValueOrDefault,0x0010,IntPtr.Zero,IntPtr.Zero);
        ShowWindow(rust.Properties.NativeWindowHandle.ValueOrDefault,3);SetForegroundWindow(rust.Properties.NativeWindowHandle.ValueOrDefault);Thread.Sleep(300);WaitName(rust,"Trade Orders").AsButton().Invoke();Thread.Sleep(600);
        var title=WaitName(rust,"Trade Order Editor");
        var nativeAncestors=new List<object>();var current=title;
        for(var i=0;i<8&&current is not null;i++){nativeAncestors.Add(new{name=current.Properties.Name.ValueOrDefault,type=current.ControlType.ToString(),bounds=current.BoundingRectangle.ToString()});current=current.Parent;}
        rust.Capture().Save(Path.Combine(output,"actual-trade-editor-rust.png"));
        File.WriteAllText(Path.Combine(output,"actual-trade-editor-geometry.json"),JsonSerializer.Serialize(new{WinFormsMain=wfMain.ToString(),WinFormsEditor=wfBounds.ToString(),WinFormsPanes=wfPanes,WinFormsControls=wfControls,RustMain=rust.BoundingRectangle.ToString(),RustEditorAncestors=nativeAncestors},new JsonSerializerOptions{WriteIndented=true}));
        var nativeBounds=title.BoundingRectangle;
        Assert.InRange(Math.Abs(nativeBounds.X-wfBounds.X),0,3);
        Assert.InRange(Math.Abs(nativeBounds.Y-wfBounds.Y),0,3);
        Assert.InRange(Math.Abs(nativeBounds.Width-wfBounds.Width),0,3);
        Assert.InRange(Math.Abs(nativeBounds.Height-wfBounds.Height),0,3);
        Assert.True(wfProcess.Responding&&rustProcess.Responding);
    }
    [System.Runtime.InteropServices.DllImport("user32.dll")]static extern bool SetCursorPos(int x,int y);
    delegate bool EnumProc(IntPtr h,IntPtr l);
    static IntPtr Find(int pid,string title,bool prefix){IntPtr found=IntPtr.Zero;EnumWindows((h,_)=>{GetWindowThreadProcessId(h,out var id);if(id==pid&&IsWindowVisible(h)){var s=new System.Text.StringBuilder(500);GetWindowText(h,s,500);if(prefix?s.ToString().StartsWith(title):s.ToString()==title){found=h;return false;}}return true;},IntPtr.Zero);return found;}
    [System.Runtime.InteropServices.DllImport("user32.dll")]static extern bool EnumWindows(EnumProc callback,IntPtr l);
    [System.Runtime.InteropServices.DllImport("user32.dll")]static extern uint GetWindowThreadProcessId(IntPtr h,out uint pid);
    [System.Runtime.InteropServices.DllImport("user32.dll",CharSet=System.Runtime.InteropServices.CharSet.Unicode)]static extern int GetWindowText(IntPtr h,System.Text.StringBuilder s,int n);
    [System.Runtime.InteropServices.DllImport("user32.dll")]static extern bool IsWindowVisible(IntPtr h);
    [System.Runtime.InteropServices.DllImport("user32.dll")]static extern bool ShowWindow(IntPtr h,int n);
    [System.Runtime.InteropServices.DllImport("user32.dll")]static extern bool SetForegroundWindow(IntPtr h);
    [System.Runtime.InteropServices.DllImport("user32.dll")]static extern bool PostMessage(IntPtr h,uint m,IntPtr w,IntPtr l);
    static Window WaitWindow(UIA3Automation a,Process p){var watch=Stopwatch.StartNew();while(watch.Elapsed<TimeSpan.FromSeconds(30)){var main=a.GetDesktop().FindAllChildren(cf=>cf.ByProcessId(p.Id)).FirstOrDefault(e=>e.Properties.Name.ValueOrDefault?.StartsWith("Investment Fund Manager")==true);if(main is not null)return main.AsWindow();Thread.Sleep(100);}throw new TimeoutException("Main window is missing");}
    static AutomationElement WaitName(Window w,string name){var watch=Stopwatch.StartNew();while(watch.Elapsed<TimeSpan.FromSeconds(30)){try{var element=w.FindFirstDescendant(cf=>cf.ByName(name));if(element is not null)return element;}catch(System.Runtime.InteropServices.COMException){}Thread.Sleep(100);}throw new TimeoutException("Missing "+name);}
    sealed class DpiScope:IDisposable{readonly IntPtr previous=SetThreadDpiAwarenessContext(new(-4));public void Dispose()=>SetThreadDpiAwarenessContext(previous);}
    [System.Runtime.InteropServices.DllImport("user32.dll")]static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
}
