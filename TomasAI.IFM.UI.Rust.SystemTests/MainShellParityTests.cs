using System.Diagnostics;
using System.Drawing;
using System.Text.Json;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.UIA3;

public sealed class MainShellParityTests
{
    [Fact]
    public async Task Actual_main_windows_match_shell_regions_and_native_splitters_resize()
    {
        var root = Environment.GetEnvironmentVariable("IFM_RUST_REPO_ROOT") ?? throw new InvalidOperationException("Set IFM_RUST_REPO_ROOT");
        var output = Environment.GetEnvironmentVariable("IFM_RUST_TEST_ARTIFACTS") ?? Path.Combine(root,".artifacts/rust-ui/tests");
        Directory.CreateDirectory(output);
        using var state = JsonDocument.Parse(File.ReadAllText(Path.Combine(root,".artifacts/rust-ui/development-ui-processes.json")));
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        Assert.True((await http.GetAsync("http://localhost:22543/health/launch-ready")).IsSuccessStatusCode);
        using (var dpi = new DesktopDpiScope())
        {
        using var automation = new UIA3Automation();
        using var wfProcess = Process.GetProcessById(state.RootElement.GetProperty("WinFormsProcessId").GetInt32());
        using var rustProcess = Process.GetProcessById(state.RootElement.GetProperty("RustProcessId").GetInt32());
        var wf = WindowFor(automation,wfProcess); var rust = WindowFor(automation,rustProcess);
        wf.Patterns.Window.Pattern.SetWindowVisualState(FlaUI.Core.Definitions.WindowVisualState.Maximized);
        rust.Patterns.Window.Pattern.SetWindowVisualState(FlaUI.Core.Definitions.WindowVisualState.Maximized);
        var sizeWatch=Stopwatch.StartNew();
        while ((Math.Abs(wf.BoundingRectangle.Width-rust.BoundingRectangle.Width)>2 || Math.Abs(wf.BoundingRectangle.Height-rust.BoundingRectangle.Height)>2) && sizeWatch.Elapsed<TimeSpan.FromSeconds(20)) Thread.Sleep(100);
        Assert.InRange(Math.Abs(wf.BoundingRectangle.Width-rust.BoundingRectangle.Width),0,2);
        Assert.InRange(Math.Abs(wf.BoundingRectangle.Height-rust.BoundingRectangle.Height),0,2);
        Assert.StartsWith("Investment Fund Manager",wf.Title);
        Assert.Equal(wf.Title,rust.Title);
        rust.Patterns.Window.Pattern.SetWindowVisualState(FlaUI.Core.Definitions.WindowVisualState.Minimized);
        SetForegroundWindow(wfProcess.MainWindowHandle); wf.Focus(); Thread.Sleep(1500);
        wf.Capture().Save(Path.Combine(output,"main-shell-winforms-diagnostic.png"));
        foreach (var window in new[]{wf,rust})
        {
            foreach (var label in new[]{"Trade Orders","Market Data","Portfolio","Reference","System","Strategy","Latency","Traffic","Errors","Saturation","Time Frame:"})
                Assert.NotNull(Find(window, label));
        }
        foreach (var window in new[] { wf, rust }) Assert.Null(window.FindFirstDescendant(cf => cf.ByName("Operations")));
        Assert.NotNull(FindId(wf,"marketOutlookView1"));
        Assert.NotNull(Find(rust,"Market Direction"));
        File.WriteAllText(Path.Combine(output,"main-shell-window-bounds.json"),JsonSerializer.Serialize(new{WinForms=wf.BoundingRectangle.ToString(),Rust=rust.BoundingRectangle.ToString()}));
        var wfBody = FindId(wf,"operationViewSplitter").BoundingRectangle;
        var wfLeft = FindId(wf,"pnlOperationView").BoundingRectangle;
        var wfRight = FindId(wf,"pnlAppView").BoundingRectangle;
        var menu = FindId(wf,"toolStrip1").BoundingRectangle;
        var status = FindId(wf,"statusBar").BoundingRectangle;
        Assert.True(menu.Bottom <= wfBody.Top+5);
        Assert.True(status.Top >= wfBody.Bottom-5);
        File.WriteAllText(Path.Combine(output,"main-shell-winforms-regions.json"),JsonSerializer.Serialize(new{Body=wfBody.ToString(),Left=wfLeft.ToString(),Right=wfRight.ToString(),Menu=menu.ToString(),Status=status.ToString()}));
        Assert.InRange((double)wfLeft.Width/wfBody.Width,0.20,0.24);
        Assert.InRange((double)wfRight.Width/wfBody.Width,0.20,0.24);
        rust.Patterns.Window.Pattern.SetWindowVisualState(FlaUI.Core.Definitions.WindowVisualState.Maximized);
        SetForegroundWindow(rustProcess.MainWindowHandle); rust.Focus(); Thread.Sleep(1000);
        File.WriteAllText(Path.Combine(output,"main-shell-window-bounds.json"),JsonSerializer.Serialize(new{WinForms=wf.BoundingRectangle.ToString(),Rust=rust.BoundingRectangle.ToString()}));
        var metrics = Path.Combine(root,".artifacts/rust-ui/tests/shell-geometry.json");
        var before = Geometry(metrics);
        Assert.InRange(before.PixelsPerPoint,0.5f,4f);
        var viewportWidth=before.Viewport[0];
        Assert.InRange(before.Left[2]/viewportWidth,0.20,0.24);
        Assert.InRange(before.Right[2]/viewportWidth,0.20,0.24);
        Assert.True(before.Left[0]+before.Left[2] < before.Right[0]);
        Assert.InRange(before.Left[1],26,28);
        Assert.InRange(before.Viewport[1]-before.Left[1]-before.Left[3],23,25);
        Assert.InRange(Math.Abs((double)wfLeft.Width/wfBody.Width-before.Left[2]/viewportWidth),0,0.01);
        Assert.InRange(Math.Abs((double)wfRight.Width/wfBody.Width-before.Right[2]/viewportWidth),0,0.01);
        var operations=Find(rust,"Strategy").BoundingRectangle;
        var outlook=Find(rust,"Market Direction").BoundingRectangle;
        var ready=rust.FindAllDescendants(cf=>cf.ByControlType(FlaUI.Core.Definitions.ControlType.Text)).OrderByDescending(element=>element.BoundingRectangle.Top).First().BoundingRectangle;
        Assert.True(operations.Left < outlook.Left);
        Assert.True(ready.Top > outlook.Top+300);
        foreach(var label in new[]{"Economic Calendar","Status Console"})Assert.NotNull(Find(rust,label));
        rust.Patterns.Window.Pattern.SetWindowVisualState(FlaUI.Core.Definitions.WindowVisualState.Minimized);
        SetForegroundWindow(wfProcess.MainWindowHandle); wf.Focus(); Thread.Sleep(500); wf.Capture().Save(Path.Combine(output,"main-shell-winforms.png"));
        rust.Patterns.Window.Pattern.SetWindowVisualState(FlaUI.Core.Definitions.WindowVisualState.Maximized);
        SetForegroundWindow(rustProcess.MainWindowHandle); rust.Focus(); Thread.Sleep(1000); rust.Capture().Save(Path.Combine(output,"main-shell-rust.png"));
        // UI Automation supplies the text anchor; native panel geometry locates the custom-rendered splitter.
        operations=Find(rust,"Strategy").BoundingRectangle;
        before=Geometry(metrics);
        var clientOrigin = new NativePoint();
        Assert.True(ClientToScreen(rustProcess.MainWindowHandle, ref clientOrigin));
        var originX = clientOrigin.X;
        var originY = clientOrigin.Y;
        var leftPoint = new Point((int)(originX+(before.Left[0]+before.Left[2])*before.PixelsPerPoint), (int)(originY+(before.Left[1]+before.Left[3]/2)*before.PixelsPerPoint));
        File.WriteAllText(Path.Combine(output,"main-shell-drag-coordinates.json"),JsonSerializer.Serialize(new{OriginX=originX,OriginY=originY,Left=leftPoint,Foreground=GetForegroundWindow().ToInt64(),Rust=rustProcess.MainWindowHandle.ToInt64()}));
        Mouse.MoveTo(leftPoint); Thread.Sleep(400);
        Mouse.Down(FlaUI.Core.Input.MouseButton.Left); Thread.Sleep(400);
        Mouse.MoveTo(new Point(leftPoint.X+60,leftPoint.Y)); Thread.Sleep(600);
        File.WriteAllText(Path.Combine(output,"main-shell-drag-during.json"),File.ReadAllText(metrics));
        Mouse.Up(FlaUI.Core.Input.MouseButton.Left); Thread.Sleep(800);
        Mouse.MoveTo(new Point(leftPoint.X+65,leftPoint.Y));Thread.Sleep(500);
        var afterLeft=WaitGeometry(metrics,g=>g.Left[2]>before.Left[2]+20);
        var rightPoint=new Point((int)(originX+afterLeft.Right[0]*afterLeft.PixelsPerPoint),(int)(originY+(afterLeft.Right[1]+afterLeft.Right[3]/2)*afterLeft.PixelsPerPoint));
        Mouse.MoveTo(rightPoint); Thread.Sleep(400);
        Mouse.Drag(rightPoint,new Point(rightPoint.X-60,rightPoint.Y)); Thread.Sleep(800);
        Mouse.MoveTo(new Point(rightPoint.X-65,rightPoint.Y));Thread.Sleep(500);
        var afterRight=WaitGeometry(metrics,g=>g.Right[2]>afterLeft.Right[2]+20);
        Assert.True(wfProcess.Responding && rustProcess.Responding);
        rust.Capture().Save(Path.Combine(output,"main-shell-rust-resized.png"));
        File.WriteAllText(Path.Combine(output,"main-shell-parity.json"),JsonSerializer.Serialize(new{WinFormsTitle=wf.Title,RustTitle=rust.Title,WinFormsLeftRatio=(double)wfLeft.Width/wfBody.Width,WinFormsRightRatio=(double)wfRight.Width/wfBody.Width,NativeBefore=before,NativeAfter=afterRight}));
        }
        Assert.True((await http.GetAsync("http://localhost:22543/health/launch-ready")).IsSuccessStatusCode);
    }
    sealed class DesktopDpiScope : IDisposable
    {
        readonly IntPtr previous = SetThreadDpiAwarenessContext(new IntPtr(-4));
        public void Dispose() => SetThreadDpiAwarenessContext(previous);
    }
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr handle);
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)] struct NativePoint { public int X; public int Y; }
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool ClientToScreen(IntPtr handle, ref NativePoint point);
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    sealed record Regions(float[] Left,float[] Right,[property: System.Text.Json.Serialization.JsonPropertyName("pixels_per_point")] float PixelsPerPoint,float[] Viewport);
    static Regions Geometry(string path) { for(var i=0;i<100;i++){try{return JsonSerializer.Deserialize<Regions>(File.ReadAllText(path),new JsonSerializerOptions { PropertyNameCaseInsensitive=true })!;}catch(Exception e)when(e is IOException or JsonException){Thread.Sleep(100);}}throw new InvalidOperationException("Shell geometry not published"); }
    static Regions WaitGeometry(string path,Func<Regions,bool> predicate){var watch=Stopwatch.StartNew();Regions result;do{result=Geometry(path);if(predicate(result))return result;Thread.Sleep(100);}while(watch.Elapsed<TimeSpan.FromSeconds(10));Assert.True(predicate(result),"Splitter did not resize");return result;}
    static Window WindowFor(UIA3Automation a,Process p){var watch=Stopwatch.StartNew();do{p.Refresh();if(p.MainWindowHandle!=IntPtr.Zero)return a.FromHandle(p.MainWindowHandle).AsWindow();Thread.Sleep(100);}while(watch.Elapsed<TimeSpan.FromSeconds(30));throw new InvalidOperationException("Missing window");}
    static AutomationElement Find(Window w,string label)=>Wait(()=>w.FindFirstDescendant(cf=>cf.ByName(label)),label);
    static AutomationElement FindId(Window w,string id)=>Wait(()=>w.FindFirstDescendant(cf=>cf.ByAutomationId(id)),id);
    static AutomationElement Wait(Func<AutomationElement?> find,string identity){var watch=Stopwatch.StartNew();do{try{if(find() is {} element)return element;}catch(System.Runtime.InteropServices.COMException){}Thread.Sleep(100);}while(watch.Elapsed<TimeSpan.FromSeconds(20));throw new InvalidOperationException("Missing "+identity);}
}
