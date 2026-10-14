using System.Diagnostics;
using System.Text.Json;
using FlaUI.UIA3;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;

public sealed class MarketChartFlaUiTests
{
    [Fact]
    public void Both_live_clients_have_selectable_ES_ESBB_and_VX_chart_tabs()
    {
        var root=Environment.GetEnvironmentVariable("IFM_RUST_REPO_ROOT")!;
        var output=Environment.GetEnvironmentVariable("IFM_RUST_TEST_ARTIFACTS")!;
        using var state=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,".artifacts/rust-ui/development-ui-processes.json")));
        using var dpi=new DpiScope();
        using var automation=new UIA3Automation();
        var windows=new[]{("winforms",state.RootElement.GetProperty("WinFormsProcessId").GetInt32()),("rust",state.RootElement.GetProperty("RustProcessId").GetInt32())}.Select(pair=>{var process=Process.GetProcessById(pair.Item2);return (pair.Item1,process,window:automation.FromHandle(process.MainWindowHandle).AsWindow());}).ToArray();
        foreach(var current in windows)
        {
            foreach(var other in windows.Where(w=>w.Item1!=current.Item1))other.window.Patterns.Window.Pattern.SetWindowVisualState(FlaUI.Core.Definitions.WindowVisualState.Minimized);
            current.window.Patterns.Window.Pattern.SetWindowVisualState(FlaUI.Core.Definitions.WindowVisualState.Maximized);
            SetForegroundWindow(current.process.MainWindowHandle);current.window.Focus();Thread.Sleep(1200);
            foreach(var label in new[]{"ES","ES-BB","VX","ES"})
            {
                var watch=Stopwatch.StartNew();AutomationElement? tab=null;
                while(tab is null&&watch.Elapsed<TimeSpan.FromSeconds(20)){
                    tab=current.window.FindAllDescendants(cf=>cf.ByName(label)).FirstOrDefault(element=>element.BoundingRectangle.Left>current.window.BoundingRectangle.Left+current.window.BoundingRectangle.Width*0.65&&element.BoundingRectangle.Top<current.window.BoundingRectangle.Top+current.window.BoundingRectangle.Height*0.5);
                    if(tab is null)Thread.Sleep(200);
                }
                Assert.NotNull(tab);tab.Click();Thread.Sleep(1000);
                if(current.Item1=="rust")
                {
                    var expected=Array.IndexOf(new[]{"ES","ES-BB","VX"},label);
                    for(var retry=0;retry<3;retry++)
                    {
                        using var geometry=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,".artifacts/rust-ui/tests/shell-geometry.json")));
                        if(geometry.RootElement.GetProperty("market_tab").GetInt32()==expected)break;
                        tab=current.window.FindAllDescendants(cf=>cf.ByName(label)).First(element=>element.BoundingRectangle.Left>current.window.BoundingRectangle.Width*0.65&&element.BoundingRectangle.Top<current.window.BoundingRectangle.Height*0.5);
                        tab.Click();Thread.Sleep(800);
                    }
                    using var selected=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,".artifacts/rust-ui/tests/shell-geometry.json")));
                    Assert.Equal(expected,selected.RootElement.GetProperty("market_tab").GetInt32());
                }
                Assert.True(current.process.Responding);
                current.window.Capture().Save(Path.Combine(output,$"market-chart-{current.Item1}-{label}.png"));
            }
        }
    }
    sealed class DpiScope : IDisposable { readonly IntPtr previous=SetThreadDpiAwarenessContext(new IntPtr(-4)); public void Dispose()=>SetThreadDpiAwarenessContext(previous); }
    [System.Runtime.InteropServices.DllImport("user32.dll")]static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
    [System.Runtime.InteropServices.DllImport("user32.dll")]static extern bool SetForegroundWindow(IntPtr handle);
}
