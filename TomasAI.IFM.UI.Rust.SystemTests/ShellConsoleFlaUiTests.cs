using System.Diagnostics;
using System.Text.Json;
using FlaUI.UIA3;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;

public sealed class ShellConsoleFlaUiTests
{
    [Fact]
    public void Live_rust_console_receives_actual_shared_shell_market_outlook_messages()
    {
        var root=Environment.GetEnvironmentVariable("IFM_RUST_REPO_ROOT") ?? throw new InvalidOperationException("Live repository path is required.");
        var output=Environment.GetEnvironmentVariable("IFM_RUST_TEST_ARTIFACTS") ?? throw new InvalidOperationException("Artifact path is required.");
        using var state=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,".artifacts/rust-ui/development-ui-processes.json")));
        using var process=Process.GetProcessById(state.RootElement.GetProperty("RustProcessId").GetInt32());
        using var automation=new UIA3Automation();
        var window=automation.FromHandle(process.MainWindowHandle).AsWindow();
        window.Patterns.Window.Pattern.SetWindowVisualState(WindowVisualState.Maximized);
        window.Focus();
        string? found=null;
        var watch=Stopwatch.StartNew();
        while(watch.Elapsed<TimeSpan.FromSeconds(60))
        {
            found=window.FindAllDescendants().Select(element=>element.Name).FirstOrDefault(name=>
                name is not null && name.StartsWith("ES",StringComparison.Ordinal) && name.Contains(" Market Outlook refreshed by ",StringComparison.Ordinal));
            if(found is not null)break;
            Thread.Sleep(150);
        }
        Assert.NotNull(found);
        Assert.True(process.Responding);
        Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(output,"shared-shell-console-message.txt"),found);
        window.Capture().Save(Path.Combine(output,"shared-shell-console-rust.png"));
    }
}
