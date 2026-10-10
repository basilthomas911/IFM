using System.Diagnostics;
using FlaUI.Core.Definitions;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;
using FluentAssertions;
namespace TomasAI.IFM.UI.Net.SystemTests.Layout;
public sealed class LiveStrategyDetailsFlaUiTests
{
    [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    static extern int GetWindowLong(IntPtr handle, int index);
    [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
    static extern int SetWindowLong(IntPtr handle, int index, int value);
    [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "SendMessageTimeoutW")]
    static extern IntPtr SendMessageTimeout(IntPtr handle, int message, IntPtr wParam, IntPtr lParam, int flags, int timeout, out IntPtr result);

    [LiveStrategyFact]
    [Trait("Category", "ManualLiveUI")]
    public async Task Current_live_iti_selection_and_expansion_latency()
    {
        using var process = Process.GetProcessesByName("TomasAI.IFM.UI.Net").Single();
        using var automation = new UIA3Automation();
        var window = automation.FromHandle(process.MainWindowHandle).AsWindow();
        window.Focus();
        window.FindFirstDescendant(cf => cf.ByName("Strategy").And(cf.ByControlType(ControlType.TabItem))).AsTabItem().Select();
        window.FindFirstDescendant(cf => cf.ByName("Details").And(cf.ByControlType(ControlType.TabItem))).AsTabItem().Select();
        var tree = window.FindFirstDescendant(cf => cf.ByAutomationId("WorkflowDetailsTree"));
        tree.Should().NotBeNull("leave the affected workflow Details tab visible for this targeted diagnostic");
        if (tree.FindFirstChild(cf => cf.ByName("ITI Signal")) is null)
        {
            window.FindFirstDescendant(cf => cf.ByAutomationId("lstStrategyWorkflows"))
                .FindFirstDescendant(cf => cf.ByControlType(ControlType.ListItem)).Patterns.SelectionItem.Pattern.Select();
            await Task.Delay(1000);
        }
        var updatesTab = window.FindFirstDescendant(cf => cf.ByName("Strategy Updates").And(cf.ByControlType(ControlType.TabItem)));
        updatesTab?.AsTabItem().Select();
        var list = window.FindFirstDescendant(cf => cf.ByAutomationId("lstStrategyWorkflows"));
        var candidates = list.FindAllDescendants(cf => cf.ByControlType(ControlType.ListItem));
        var target = candidates.FirstOrDefault(item => item.Name.Contains("18:40") || item.Name.Contains("6:40"));
        if (target is null)
        {
            foreach (var candidate in candidates) Console.WriteLine("ROW " + candidate.Name);
        }
        target.Should().NotBeNull("test the user's 6:40 workflow row");
        target!.Patterns.SelectionItem.Pattern.Select();
        window.FindFirstDescendant(cf => cf.ByName("Details").And(cf.ByControlType(ControlType.TabItem))).AsTabItem().Select();
        await Task.Delay(1000);
        if (Environment.GetEnvironmentVariable("IFM_TEST_DISABLE_TREE_TOOLTIPS") == "1")
        {
            var handle = (IntPtr)tree.Properties.NativeWindowHandle.Value;
            var style = GetWindowLong(handle, -16);
            SetWindowLong(handle, -16, (style | 0x80) & ~0x800);
            SendMessageTimeout(handle, 0x1100 + 24, IntPtr.Zero, IntPtr.Zero, 2, 1000, out var oldTooltip);
            Console.WriteLine($"Disabled native hover tooltips on tree {handle}, old tooltip {oldTooltip}.");
        }
        var timings = new List<string>();
        for (var cycle = 0; cycle < 20; cycle++)
        {
            var workflow = tree.FindFirstChild(cf => cf.ByName("Workflow")).AsTreeItem();
            var iti = tree.FindFirstChild(cf => cf.ByName("ITI Signal")).AsTreeItem();
            workflow.Select();
            var watch = Stopwatch.StartNew();
            if (Environment.GetEnvironmentVariable("IFM_TEST_MOUSE") == "1")
                iti.Click();
            else
                await Task.Run(() => iti.Select()).WaitAsync(TimeSpan.FromSeconds(5));
            var selection = watch.Elapsed.TotalMilliseconds;
            iti.Patterns.ExpandCollapse.Pattern.Collapse();
            watch.Restart();
            if (Environment.GetEnvironmentVariable("IFM_TEST_MOUSE") == "1")
            {
                var bounds = iti.BoundingRectangle;
                Console.WriteLine($"Mouse glyph: {bounds.Left - 12}, {bounds.Top + bounds.Height / 2}; label={bounds}");
                FlaUI.Core.Input.Mouse.LeftClick(new System.Drawing.Point(bounds.Left - 12, bounds.Top + bounds.Height / 2));
                await Task.Run(() =>
                {
                    var timeout = Stopwatch.StartNew();
                    while (iti.Patterns.ExpandCollapse.Pattern.ExpandCollapseState.Value != ExpandCollapseState.Expanded)
                    {
                        if (timeout.Elapsed > TimeSpan.FromSeconds(25)) throw new TimeoutException("Mouse expansion did not complete.");
                        Thread.Sleep(10);
                    }
                }).WaitAsync(TimeSpan.FromSeconds(30));
            }
            else
                await Task.Run(() => iti.Patterns.ExpandCollapse.Pattern.Expand()).WaitAsync(TimeSpan.FromSeconds(5));
            var expansion = watch.Elapsed.TotalMilliseconds;
            iti.Patterns.ExpandCollapse.Pattern.ExpandCollapseState.Value.Should().Be(ExpandCollapseState.Expanded);
            var children = iti.FindAllChildren(cf => cf.ByControlType(ControlType.TreeItem));
            timings.Add($"Cycle={cycle} SelectMs={selection:F1} ExpandMs={expansion:F1} Children={children.Length}");
            if (Environment.GetEnvironmentVariable("IFM_TEST_DISABLE_TREE_TOOLTIPS") == "1")
            {
                FlaUI.Core.Input.Mouse.MoveTo(iti.GetClickablePoint());
                await Task.Delay(1500);
            }
            else await Task.Delay(100);
        }
        File.WriteAllLines(@"C:\repos\IFM\.artifacts\strategy-iti-live-latency.log", timings);
        foreach (var timing in timings) Console.WriteLine(timing);
    }

    [LiveStrategyFact]
    [Trait("Category", "ManualLiveUI")]
    public async Task Each_live_strategy_branch_expands_and_collapses()
    {
        // Explicit opt-in: this navigates the user's running UI, without changing business state.
        using var process = Process.GetProcessesByName("TomasAI.IFM.UI.Net").Single();
        using var automation = new UIA3Automation();
        var window = automation.FromHandle(process.MainWindowHandle).AsWindow();
        window.Focus();
        var strategy = window.FindFirstDescendant(cf => cf.ByName("Strategy").And(cf.ByControlType(ControlType.TabItem)));
        strategy.Should().NotBeNull();
        strategy.AsTabItem().Select();
        var updatesTab = window.FindFirstDescendant(cf => cf.ByName("Strategy Updates").And(cf.ByControlType(ControlType.TabItem)));
        updatesTab?.AsTabItem().Select();
        var list = window.FindFirstDescendant(cf => cf.ByAutomationId("lstStrategyWorkflows"));
        list.Should().NotBeNull();
        var row = list.FindFirstDescendant(cf => cf.ByControlType(ControlType.ListItem));
        row.Should().NotBeNull("the live check requires at least one real strategy workflow");
        row.Patterns.SelectionItem.Pattern.Select();
        window.FindFirstDescendant(cf => cf.ByName("Details").And(cf.ByControlType(ControlType.TabItem))).AsTabItem().Select();
        await Task.Delay(500);
        var tree = window.FindFirstDescendant(cf => cf.ByAutomationId("WorkflowDetailsTree"));
        tree.Should().NotBeNull();
        var audit = @"C:\repos\IFM\.artifacts\strategy-details-live-branches.log";
        File.WriteAllText(audit, $"Live UI PID {process.Id}, {DateTimeOffset.Now:O}\n");
        int count = 0;
        async Task Walk(AutomationElement parent, string path)
        {
            foreach (var element in parent.FindAllChildren(cf => cf.ByControlType(ControlType.TreeItem)))
            {
                var item = element.AsTreeItem();
                if (!item.Patterns.ExpandCollapse.IsSupported || item.Patterns.ExpandCollapse.Pattern.ExpandCollapseState.Value == ExpandCollapseState.LeafNode) continue;
                var current = path + "/" + item.Name;
                File.AppendAllText(audit, $"EXPAND {current}\n");
                var watch = Stopwatch.StartNew();
                await Task.Run(() => item.Patterns.ExpandCollapse.Pattern.Expand()).WaitAsync(TimeSpan.FromSeconds(5));
                var children = item.FindAllChildren(cf => cf.ByControlType(ControlType.TreeItem));
                if (children.Length > 0)
                {
                    item.Patterns.ExpandCollapse.Pattern.ExpandCollapseState.Value.Should().Be(ExpandCollapseState.Expanded, current);
                    await Walk(item, current);
                    File.AppendAllText(audit, $"COLLAPSE {current}\n");
                    await Task.Run(() => item.Patterns.ExpandCollapse.Pattern.Collapse()).WaitAsync(TimeSpan.FromSeconds(5));
                    item.Patterns.ExpandCollapse.Pattern.ExpandCollapseState.Value.Should().Be(ExpandCollapseState.Collapsed, current);
                    count++;
                }
                File.AppendAllText(audit, $"DONE {current} {watch.Elapsed.TotalMilliseconds:F1}ms\n");
            }
        }
        updatesTab?.AsTabItem().Select();
        var rows = list.FindAllDescendants(cf => cf.ByControlType(ControlType.ListItem));
        var checkedRows = Math.Min(rows.Length, 20);
        for (var rowIndex = 0; rowIndex < checkedRows; rowIndex++)
        {
            File.AppendAllText(audit, $"WORKFLOW ROW {rowIndex}\n");
            updatesTab?.AsTabItem().Select();
            rows[rowIndex].Patterns.SelectionItem.Pattern.Select();
            window.FindFirstDescendant(cf => cf.ByName("Details").And(cf.ByControlType(ControlType.TabItem))).AsTabItem().Select();
            await Task.Delay(150);
            for (var cycle = 0; cycle < 3; cycle++) await Walk(tree, $"Row{rowIndex}/Cycle{cycle}");
        }
        Console.WriteLine($"LIVE checked {checkedRows} real workflow rows, three full-tree cycles each.");
        count.Should().BeGreaterThan(0);
        Console.WriteLine($"LIVE verified all {count} reachable expandable branches; audit: {audit}");
    }
}

/// <summary>Runs interactive live UI checks only when explicitly enabled by the test operator.</summary>
public sealed class LiveStrategyFactAttribute : FactAttribute
{
    public LiveStrategyFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("IFM_TEST_LIVE_STRATEGY_UI") != "1")
            Skip = "Set IFM_TEST_LIVE_STRATEGY_UI=1 to navigate the running development UI.";
    }
}
