using System.Diagnostics;
using System.Windows.Forms;
using FlaUI.Core.Definitions;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;
using FluentAssertions;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.UI.Net.ViewModels.Operations;
using TomasAI.IFM.UI.Net.Views.Strategy;

namespace TomasAI.IFM.UI.Net.SystemTests.Layout;

public sealed class StrategyDetailsFlaUiTests
{
    [Fact]
    public async Task Hidden_details_becomes_interactive_and_survives_selection_expansion_and_live_refresh()
    {
        var ready = new TaskCompletionSource<(Form Form, StrategyWorkflowDetailsAccordion Browser, IntPtr Handle)>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                using var form = new Form { Text = "IFM Strategy Details FlaUI", Width = 1050, Height = 700 };
                var tabs = new TabControl { Dock = DockStyle.Fill, Name = "StrategyDetailsTestTabs" };
                tabs.TabPages.Add(new TabPage("Summary"));
                var details = new TabPage("Details");
                var browser = new StrategyWorkflowDetailsAccordion();
                details.Controls.Add(browser);
                tabs.TabPages.Add(details);
                form.Controls.Add(tabs);
                var workflow = CreateWorkflow();
                // Production binds snapshots while Strategy/Details can still be hidden.
                browser.Bind(StrategyWorkflowPresentation.CreateDetails(workflow));
                var tree = Find<TreeView>(browser, "WorkflowDetailsTree");
                tree.CreateControl();

                Find<PropertyGrid>(browser, "WorkflowDetailsProperties").SelectedObject.Should().BeNull();
                form.Shown += (_, _) => ready.TrySetResult((form, browser, form.Handle));
                System.Windows.Forms.Application.Run(form);
            }
            catch (Exception exception) { ready.TrySetException(exception); }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        var (form, browser, handle) = await ready.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try
        {
            using var automation = new UIA3Automation();
            var window = automation.FromHandle(handle).AsWindow();
            window.Focus();
            window.FindFirstDescendant(cf => cf.ByName("Details").And(cf.ByControlType(ControlType.TabItem))).AsTabItem().Select();
            await WaitForDetails(form, browser);
            var tree = window.FindFirstDescendant(cf => cf.ByAutomationId("WorkflowDetailsTree"));
            tree.Should().NotBeNull();
            var grid = window.FindFirstDescendant(cf => cf.ByAutomationId("WorkflowDetailsProperties"));
            grid.Should().NotBeNull();
            var itiRoot = tree.FindFirstChild(cf => cf.ByName("ITI Signal")).AsTreeItem();
            for (var hover = 0; hover < 3; hover++)
            {
                itiRoot.Click();
                FlaUI.Core.Input.Mouse.MoveTo(itiRoot.GetClickablePoint());
                await Task.Delay(1200);
                await OnUi(form, () => { });
                itiRoot.Patterns.ExpandCollapse.Pattern.Expand();
                itiRoot.Patterns.ExpandCollapse.Pattern.Collapse();
            }
            var branchCount = 0;
            async Task Walk(FlaUI.Core.AutomationElements.AutomationElement parent)
            {
                foreach (var element in parent.FindAllChildren(cf => cf.ByControlType(ControlType.TreeItem)))
                {
                    var item = element.AsTreeItem();
                    if (!item.Patterns.ExpandCollapse.IsSupported || item.Patterns.ExpandCollapse.Pattern.ExpandCollapseState.Value == ExpandCollapseState.LeafNode) continue;
                    var started = Stopwatch.StartNew();
                    item.Patterns.ExpandCollapse.Pattern.Expand();
                    var expandElapsed = started.Elapsed;
                    var children = item.FindAllChildren(cf => cf.ByControlType(ControlType.TreeItem));
                    if (children.Length > 0)
                    {
                        item.Patterns.ExpandCollapse.Pattern.ExpandCollapseState.Value.Should().Be(ExpandCollapseState.Expanded, item.Name);
                        await Walk(item);
                        item.Patterns.ExpandCollapse.Pattern.Collapse();
                        item.Patterns.ExpandCollapse.Pattern.ExpandCollapseState.Value.Should().Be(ExpandCollapseState.Collapsed, item.Name);
                        branchCount++;
                    }
                    await OnUi(form, () => { });
                    expandElapsed.Should().BeLessThan(TimeSpan.FromSeconds(3), item.Name);
                }
            }
            await Walk(tree);
            Console.WriteLine($"All reachable nested branches expanded and collapsed: {branchCount}.");
            tree.FindFirstDescendant(cf => cf.ByName("Workflow Pipeline")).AsTreeItem().Patterns.ExpandCollapse.Pattern.Expand();
            var stopwatch = Stopwatch.StartNew();
            var selectionTimes = new List<double>();
            for (var round = 0; round < 12; round++)
            {
                foreach (var name in new[] { "Workflow", "ITI Signal", "Regime Discovery", "Market Condition", "Trade Selection", "Order Composition", "Risk Management" })
                {
                    var item = tree.FindFirstDescendant(cf => cf.ByName(name).And(cf.ByControlType(ControlType.TreeItem))).AsTreeItem();
                    item.Should().NotBeNull();
                    var selectionWatch = Stopwatch.StartNew();
                    item.Select();
                    selectionTimes.Add(selectionWatch.Elapsed.TotalMilliseconds);
                    if (item.Patterns.ExpandCollapse.IsSupported)
                    {
                        item.Patterns.ExpandCollapse.Pattern.Expand();
                        item.Patterns.ExpandCollapse.Pattern.Collapse();
                    }
                    await OnUi(form, () =>
                    {
                        var selected = Find<TreeView>(browser, "WorkflowDetailsTree").SelectedNode;
                        selected!.Text.Should().Be(name);
                        var inspector = Find<PropertyGrid>(browser, "WorkflowDetailsProperties");
                        inspector.SelectedObject.Should().NotBeNull();
                        System.ComponentModel.TypeDescriptor.GetProperties(inspector.SelectedObject!).Cast<System.ComponentModel.PropertyDescriptor>()
                            .Should().OnlyContain(p => p.IsReadOnly);
                    });
                }
                var regime = tree.FindFirstDescendant(cf => cf.ByName("Regime Discovery").And(cf.ByControlType(ControlType.TreeItem))).AsTreeItem();
                regime.Patterns.ExpandCollapse.Pattern.Expand();
                var reasons = regime.FindFirstDescendant(cf => cf.ByName("ContinuationReasonCodes").And(cf.ByControlType(ControlType.TreeItem))).AsTreeItem();
                reasons.Patterns.ExpandCollapse.Pattern.Expand();
                reasons.FindFirstDescendant(cf => cf.ByName("[0]").And(cf.ByControlType(ControlType.TreeItem))).AsTreeItem().Select();
                await OnUi(form, () =>
                {
                    var inspector = Find<PropertyGrid>(browser, "WorkflowDetailsProperties");
                    System.ComponentModel.TypeDescriptor.GetProperties(inspector.SelectedObject!)["Value"]!.GetValue(inspector.SelectedObject).Should().Be("fixture");
                });
                await OnUi(form, () =>
                {
                    var previous = (StrategyWorkflowDetails)browser.Tag!;
                    var selectedPath = Find<TreeView>(browser, "WorkflowDetailsTree").SelectedNode!.FullPath;
                    browser.Bind(StrategyWorkflowPresentation.CreateDetails(previous.Workflow! with { WorkflowRevision = previous.WorkflowRevision + 1 }));
                    Find<TreeView>(browser, "WorkflowDetailsTree").SelectedNode!.FullPath.Should().Be(selectedPath);
                });
                await WaitForDetails(form, browser);
                window.FindFirstDescendant(cf => cf.ByName("Summary").And(cf.ByControlType(ControlType.TabItem))).AsTabItem().Select();
                await OnUi(form, () =>
                {
                    var previous = (StrategyWorkflowDetails)browser.Tag!;
                    browser.Bind(StrategyWorkflowPresentation.CreateDetails(previous.Workflow! with { WorkflowRevision = previous.WorkflowRevision + 1 }));
                });
                window.FindFirstDescendant(cf => cf.ByName("Details").And(cf.ByControlType(ControlType.TabItem))).AsTabItem().Select();
                await WaitForDetails(form, browser);
                await OnUi(form, () => Find<PropertyGrid>(browser, "WorkflowDetailsProperties").SelectedObject.Should().NotBeNull());
            }
            Console.WriteLine($"Cached selection including UIA round trip: maximum {selectionTimes.Max():F1}ms, average {selectionTimes.Average():F1}ms.");
            Console.WriteLine($"FlaUI: 84 selections, expand/collapse, 24 snapshot revisions and 24 tab switches completed in {stopwatch.Elapsed.TotalSeconds:F2}s.");
            await OnUi(form, () =>
            {
                browser.ShowMessage("No workflow selected");
                Find<PropertyGrid>(browser, "WorkflowDetailsProperties").SelectedObject.Should().BeNull();
            });
        }
        finally
        {
            form.BeginInvoke(form.Close);
            thread.Join(TimeSpan.FromSeconds(5)).Should().BeTrue();
        }
    }

    static async Task WaitForDetails(Form form, Control browser)
    {
        var watch = Stopwatch.StartNew();
        while (true)
        {
            var preparing = false;
            await OnUi(form, () => preparing = browser.Controls.OfType<System.Windows.Forms.Label>().Any(label => label.Text.EndsWith("Preparing details...")));
            if (!preparing) return;
            if (watch.Elapsed > TimeSpan.FromSeconds(5)) throw new TimeoutException("Display cache preparation timed out.");
            await Task.Delay(10);
        }
    }

    static T Find<T>(Control browser, string name) where T : Control => browser.Controls.Find(name, true).OfType<T>().Single();

    static async Task OnUi(Form form, Action operation)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        form.BeginInvoke(() =>
        {
            try { operation(); completion.SetResult(); }
            catch (Exception exception) { completion.SetException(exception); }
        });
        await completion.Task.WaitAsync(TimeSpan.FromSeconds(3));
    }

    static IntrinsicTimeStrategyWorkflowView CreateWorkflow() => new()
    {
        WorkflowId = new StrategyWorkflowId(Guid.NewGuid()),
        WorkflowRevision = 1,
        RegimeDiscovery = new() { ProcessingStatus = StrategyActorProcessingStatus.Completed, ContinuationReasonCodes = ["fixture"], Result = new() { ResultId = Guid.NewGuid(), Payload = new byte[256] } },
        MarketCondition = new() { ProcessingStatus = StrategyActorProcessingStatus.Processing }
    };
}
