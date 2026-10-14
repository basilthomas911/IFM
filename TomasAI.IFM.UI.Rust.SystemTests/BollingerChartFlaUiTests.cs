using System.Diagnostics;
using System.Text.Json;
using FlaUI.UIA3;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
public sealed class BollingerChartFlaUiTests
{
    [Fact]
    public void Updated_live_ES_BB_chart_selects_and_remains_responsive()
    {
        var root=Environment.GetEnvironmentVariable("IFM_RUST_REPO_ROOT")!;
        var output=Environment.GetEnvironmentVariable("IFM_RUST_TEST_ARTIFACTS")!;
        using var state=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,".artifacts/rust-ui/development-ui-processes.json")));
        using var process=Process.GetProcessById(state.RootElement.GetProperty("RustProcessId").GetInt32());
        using var dpi=new DpiScope();
        using var automation=new UIA3Automation();
        var window=automation.FromHandle(process.MainWindowHandle).AsWindow();
        window.Patterns.Window.Pattern.SetWindowVisualState(WindowVisualState.Maximized);window.Focus();
        window.FindFirstDescendant(cf=>cf.ByName("ES-BB"))!.Click();
        Thread.Sleep(20000); // Independent 15-second history refresh plus dashboard publication.
        using var geometry=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,".artifacts/rust-ui/tests/shell-geometry.json")));
        Assert.Equal(1,geometry.RootElement.GetProperty("market_tab").GetInt32());
        Assert.True(process.Responding);
        window.Capture().Save(Path.Combine(output,"bollinger-october-rust.png"));
    }
    sealed class DpiScope : IDisposable
    {
        readonly IntPtr previous=SetThreadDpiAwarenessContext(new IntPtr(-4));
        public void Dispose()=>SetThreadDpiAwarenessContext(previous);
    }
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
}
