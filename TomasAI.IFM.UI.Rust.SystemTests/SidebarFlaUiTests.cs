using System.Diagnostics;
using System.Text.Json;
using FlaUI.UIA3;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
public sealed class SidebarFlaUiTests
{
    [Fact]
    public void Live_calendar_periods_refresh_and_console_status_bar_shows_the_latest_live_message()
    {
        var root=Environment.GetEnvironmentVariable("IFM_RUST_REPO_ROOT")!;
        var output=Environment.GetEnvironmentVariable("IFM_RUST_TEST_ARTIFACTS")!;
        using var state=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,".artifacts/rust-ui/development-ui-processes.json")));
        using var dpi=new DpiScope();using var automation=new UIA3Automation();
        using var rust=Process.GetProcessById(state.RootElement.GetProperty("RustProcessId").GetInt32());
        using var wf=Process.GetProcessById(state.RootElement.GetProperty("WinFormsProcessId").GetInt32());
        var window=automation.FromHandle(rust.MainWindowHandle).AsWindow();
        var baseline=automation.FromHandle(wf.MainWindowHandle).AsWindow();
        baseline.Patterns.Window.Pattern.SetWindowVisualState(WindowVisualState.Maximized);SetForegroundWindow(wf.MainWindowHandle);baseline.Focus();Thread.Sleep(600);
        foreach(var label in new[]{"Economic Calendar","Status Console","Today","Yesterday","Tomorrow","This Week","Next Week","Actual","Forecast","Prior"})Assert.NotNull(baseline.FindFirstDescendant(cf=>cf.ByName(label)));
        baseline.Capture().Save(Path.Combine(output,"calendar-console-winforms.png"));
        baseline.Patterns.Window.Pattern.SetWindowVisualState(WindowVisualState.Minimized);
        window.Patterns.Window.Pattern.SetWindowVisualState(WindowVisualState.Maximized);SetForegroundWindow(rust.MainWindowHandle);window.Focus();
        var metrics=Path.Combine(root,".artifacts/rust-ui/tests/shell-geometry.json");
        WaitUntil(()=>!Read(metrics).GetProperty("calendar_country").GetString()!.Equals(""),"Live calendar did not load");
        var verificationMessage="Status Console UI integration check "+Guid.NewGuid().ToString("N");
        var publisher=new TomasAI.IFM.UI.Rust.Bridge.Net.RustClientServiceScope("nats://localhost:4222");
        try {
            publisher.StartAsync().AsTask().GetAwaiter().GetResult();
            publisher.GetStatusConsoleWriter().WriteConsoleAsync(TomasAI.IFM.Shared.StatusConsole.LogSourceType.IFMApp,verificationMessage).GetAwaiter().GetResult();
            WaitUntil(()=>Read(metrics).GetProperty("latest_status").GetString()==verificationMessage,"Status Console listener did not receive the normal publisher's message");
            window.Patterns.Window.Pattern.SetWindowVisualState(WindowVisualState.Minimized);
            baseline.Patterns.Window.Pattern.SetWindowVisualState(WindowVisualState.Maximized);SetForegroundWindow(wf.MainWindowHandle);baseline.Focus();Thread.Sleep(600);
            WaitUntil(()=>baseline.FindAllDescendants().Any(element=>element.Name?.Contains(verificationMessage)==true),"WinForms did not expose the same published status message");
            baseline.Capture().Save(Path.Combine(output,"calendar-console-winforms.png"));
            baseline.Patterns.Window.Pattern.SetWindowVisualState(WindowVisualState.Minimized);
            window.Patterns.Window.Pattern.SetWindowVisualState(WindowVisualState.Maximized);SetForegroundWindow(rust.MainWindowHandle);window.Focus();Thread.Sleep(500);
        } finally {publisher.DisposeAsync().AsTask().GetAwaiter().GetResult();}
        foreach(var label in new[]{"Status Time","Status","Actual","Forecast","Prior","Country Code:"})Assert.NotNull(window.FindFirstDescendant(cf=>cf.ByName(label)));
        foreach(var period in new[]{"Yesterday","Tomorrow","This Week","Next Week","Today"})
        {
            Thread.Sleep(300);
            window.FindFirstDescendant(cf=>cf.ByName(period))!.Click();
            var retryWatch=Stopwatch.StartNew();
            WaitUntil(()=>{
                var snapshot=Read(metrics);
                if(snapshot.GetProperty("calendar_period").GetString()==period&&!snapshot.GetProperty("calendar_busy").GetBoolean())return true;
                if(!snapshot.GetProperty("calendar_busy").GetBoolean()&&retryWatch.Elapsed>TimeSpan.FromSeconds(1)) {
                    var button=window.FindFirstDescendant(cf=>cf.ByName(period))!;
                    File.WriteAllText(Path.Combine(output,"calendar-click-diagnostic.json"),JsonSerializer.Serialize(new{Period=period,Bounds=button.BoundingRectangle.ToString(),State=snapshot}));
                    window.Capture().Save(Path.Combine(output,"calendar-console-rust-diagnostic.png"));
                    button.Click();retryWatch.Restart();
                }
                return false;
            },"Period did not refresh: "+period);
        }
        Thread.Sleep(400);
        var eventRow=window.FindFirstDescendant(cf=>cf.ByName("Budget Balance (Sep)"));
        if(eventRow is not null){
            eventRow.Click();var retryWatch=Stopwatch.StartNew();
            WaitUntil(()=>{
                var snapshot=Read(metrics);
                if(snapshot.GetProperty("calendar_selected_id").GetString()!.EndsWith("Budget Balance (Sep)"))return true;
                if(retryWatch.Elapsed>TimeSpan.FromSeconds(1)){
                    var row=window.FindFirstDescendant(cf=>cf.ByName("Budget Balance (Sep)"))!;
                    File.WriteAllText(Path.Combine(output,"calendar-row-click-diagnostic.json"),JsonSerializer.Serialize(new{Bounds=row.BoundingRectangle.ToString(),State=snapshot}));
                    window.Capture().Save(Path.Combine(output,"calendar-console-rust-diagnostic.png"));
                    row.Click();retryWatch.Restart();
                }
                return false;
            },"Calendar row selection did not update the shared ViewModel");
        }
        var hasStatusBar=false;
        WaitUntil(()=>{
            var snapshot=Read(metrics);var message=snapshot.GetProperty("latest_status").GetString();
            if(string.IsNullOrEmpty(message))return false;
            hasStatusBar=window.FindAllDescendants(cf=>cf.ByName(message)).Any(element=>element.BoundingRectangle.Top>window.BoundingRectangle.Bottom-55);
            return hasStatusBar;
        },"Latest console message is missing from the status bar");
        var geometry=Read(metrics);Assert.InRange(geometry.GetProperty("status_rows").GetInt32(),1,500);
        Assert.True(geometry.GetProperty("status_drawn_rows").GetInt32()<60);
        Assert.True(rust.Responding&&wf.Responding);
        window.Capture().Save(Path.Combine(output,"calendar-console-rust.png"));
    }
    static JsonElement Read(string path){for(var retry=0;retry<10;retry++){try{using var document=JsonDocument.Parse(File.ReadAllText(path));return document.RootElement.Clone();}catch(Exception e)when(e is IOException or JsonException){Thread.Sleep(100);}}throw new InvalidOperationException("Metrics unavailable");}
    static void WaitUntil(Func<bool> predicate,string message){var watch=Stopwatch.StartNew();while(watch.Elapsed<TimeSpan.FromSeconds(45)){if(predicate())return;Thread.Sleep(150);}Assert.Fail(message);}
    sealed class DpiScope:IDisposable{readonly IntPtr previous=SetThreadDpiAwarenessContext(new IntPtr(-4));public void Dispose()=>SetThreadDpiAwarenessContext(previous);}
    [System.Runtime.InteropServices.DllImport("user32.dll")]static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
    [System.Runtime.InteropServices.DllImport("user32.dll")]static extern bool SetForegroundWindow(IntPtr handle);
}
