using System.Diagnostics;
using System.Windows.Forms;
using FlaUI.UIA3;
using FlaUI.Core.AutomationElements;
using TextBox = System.Windows.Forms.TextBox;
using Button = System.Windows.Forms.Button;
using NSubstitute;
using TomasAI.IFM.UI.Net.Models.SystemAdmin;
using TomasAI.IFM.UI.Net.Services.Operations;
using TomasAI.IFM.UI.Net.Services.Subscriptions;
using TomasAI.IFM.UI.Net.Services.SystemAdmin;
using TomasAI.IFM.UI.Net.ViewModels.SystemAdmin;
using TomasAI.IFM.UI.Net.Views.SystemAdmin;
namespace TomasAI.IFM.UI.Net.SystemTests.Layout;

/// <summary>Checks persisted outcome display and explicit uncertainty resolution in the real WinForms control.</summary>
public sealed class ScheduledTasksViewTests
{
    [Fact]
    public void First_schedule_and_run_details_are_visible_and_resolution_is_explicit()
    {
        var id = Guid.NewGuid(); var runId = Guid.NewGuid();
        var schedule = new ScheduledTaskScheduleUiModel("futures-market-close", "Market close", "Development", "development", false,
            "0 1 17 ? * MON-FRI", "America/New_York", null, null, 1800, 60, "");
        var definition = new ScheduledTaskDefinitionUiModel(id, 4, 2, schedule, true, false, "Applied", 2, "Verified", runId);
        var run = new ScheduledTaskRunUiModel(runId, DateTimeOffset.UtcNow, "Uncertain", "FeedsStopped", "Review interrupted finalization", null, null, null,
            new DateOnly(2026, 10, 7), 3, Guid.NewGuid(), 42, "feed shutdown confirmed", "process interrupted", "futures-market-close/fixture");
        var service = Substitute.For<IScheduledTaskService>(); var subscription = Substitute.For<IUiEventSubscription>();
        service.CreateNotificationSubscription(Arg.Any<Func<Guid, ValueTask>>()).Returns(subscription);
        service.LoadAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(UiOperationResult<ScheduledTaskDashboardUiModel>.Success(new([definition], [new("futures-market-close", "Market close", true, "Windows", "3")], [run], "Ready", DateTimeOffset.UtcNow)));
        service.ResolveUncertainAsync(Arg.Any<ScheduledTaskRunUiModel>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(UiOperationResult.Success());
        using var form = new Form { Width = 1400, Height = 850, Text = "Scheduled Tasks qualification" };
        using var view = new ScheduledTasksView(new ScheduledTasksViewModel(service)) { Dock = DockStyle.Fill };
        form.Controls.Add(view); form.Show();
        var watch = Stopwatch.StartNew();
        while (watch.Elapsed < TimeSpan.FromSeconds(5) && Find<TextBox>(view, "ScheduledTaskName").Text != "Market close") { System.Windows.Forms.Application.DoEvents(); Thread.Sleep(10); }
        Assert.Equal("Market close", Find<TextBox>(view, "ScheduledTaskName").Text);
        Find<TabControl>(view, "ScheduledTaskTabs").SelectedIndex = 1;
        System.Windows.Forms.Application.DoEvents();
        var details = Find<TextBox>(view, "ScheduledTaskRunDetails");
        Assert.True(details.ReadOnly); Assert.Contains("feed shutdown confirmed", details.Text); Assert.Contains("process interrupted", details.Text);
        Assert.True(details.Bottom <= details.Parent!.ClientSize.Height);
        // UIA reads the displayed property instead of trusting only the view model.
        var handle = form.Handle;
        var inspection = Task.Run(() =>
        {
            using var automation = new UIA3Automation();
            return automation.FromHandle(handle).FindFirstDescendant(x => x.ByAutomationId("ScheduledTaskRunDetails")).AsTextBox().Text;
        });
        var inspectionWatch = Stopwatch.StartNew();
        while (!inspection.IsCompleted && inspectionWatch.Elapsed < TimeSpan.FromSeconds(15)) { System.Windows.Forms.Application.DoEvents(); Thread.Sleep(10); }
        Assert.True(inspection.IsCompleted, "UI Automation did not complete within its bounded observation window.");
        Assert.Contains("feed shutdown confirmed", inspection.GetAwaiter().GetResult());
        var resolve = Controls(view).OfType<Button>().Single(x => x.Text == "Resolve as Failed");
        Assert.True(resolve.Enabled);
        resolve.PerformClick(); System.Windows.Forms.Application.DoEvents();
        Assert.DoesNotContain(service.ReceivedCalls(), x => x.GetMethodInfo().Name == nameof(IScheduledTaskService.ResolveUncertainAsync));
        Find<TextBox>(view, "ScheduledTaskResolutionReason").Text = "Inspected committed position source outcomes";
        resolve.PerformClick(); System.Windows.Forms.Application.DoEvents();
        Assert.Single(service.ReceivedCalls(), x => x.GetMethodInfo().Name == nameof(IScheduledTaskService.ResolveUncertainAsync));
        view.CloseAsync().AsTask().GetAwaiter().GetResult();
        subscription.Received(1).DisposeAsync();
    }
    [Fact]
    public void Logs_group_runs_by_date_preserve_status_and_read_full_paged_stdout()
    {
        var id = Guid.NewGuid(); var disabledId = Guid.NewGuid(); var scheduledId = Guid.NewGuid();
        var schedule = new ScheduledTaskScheduleUiModel("futures-market-close", "Market close", "Development", "development", false,
            "0 1 17 ? * MON-FRI", "America/New_York", null, null, 1800, 60, "");
        var definition = new ScheduledTaskDefinitionUiModel(id, 4, 2, schedule, true, false, "Applied", 2, "Verified", null);
        var run = new ScheduledTaskRunUiModel(Guid.NewGuid(), new DateTimeOffset(2026, 1, 2, 22, 1, 0, TimeSpan.Zero), "Succeeded", "BusinessCompleted", "Done", new DateTimeOffset(2026, 1, 2, 22, 1, 2, TimeSpan.Zero), null, 0, null);
        var old = run with { Id = Guid.NewGuid(), IntendedFireTimeUtc = run.IntendedFireTimeUtc.AddYears(-1), Status = "Failed" };
        var running = run with { Id = Guid.NewGuid(), Status = "Running", IntendedFireTimeUtc = run.IntendedFireTimeUtc.AddDays(1) };
        var pending = run with { Id = Guid.NewGuid(), Status = "Requested", StartedAtUtc = null, IntendedFireTimeUtc = run.IntendedFireTimeUtc.AddMonths(-1) };
        var service = Substitute.For<IScheduledTaskService>(); var subscription = Substitute.For<IUiEventSubscription>();
        service.CreateNotificationSubscription(Arg.Any<Func<Guid, ValueTask>>()).Returns(subscription);
        service.LoadAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(UiOperationResult<ScheduledTaskDashboardUiModel>.Success(new([definition, definition with { Id = disabledId, Enabled = false, Schedule = schedule with { Name = "Disabled" } }, definition with { Id = scheduledId, Schedule = schedule with { Name = "Scheduled" } }], [], [run], "Ready", null)));
        service.LoadRunHistoryAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<byte[]?>(), Arg.Any<CancellationToken>())
            .Returns(call => UiOperationResult<ScheduledTaskRunPageUiModel>.Success(new(call.ArgAt<Guid>(2) == id ? call.ArgAt<byte[]?>(3) is null ? [running, run, pending] : [old] : [], call.ArgAt<Guid>(2) == id && call.ArgAt<byte[]?>(3) is null ? [1] : null)));
        service.PreviewAsync(Arg.Any<ScheduledTaskScheduleUiModel>(), Arg.Any<CancellationToken>())
            .Returns(UiOperationResult<ScheduledTaskPreviewUiModel>.Success(new(true, [], [run.IntendedFireTimeUtc.AddYears(1)])));
        service.LoadOutputAsync(Arg.Any<string>(), Arg.Any<string>(), id, Arg.Any<ScheduledTaskRunUiModel>(), Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(call => UiOperationResult<ScheduledTaskOutputPageUiModel>.Success(call.ArgAt<long>(4) == 0 ? new("complete stdout beginning\r\n", 25, false, true) : new("final stdout line", 42, true, true)));
        using var form = new Form { Width = 1400, Height = 850, Text = "Scheduled Logs qualification" };
        using var view = new ScheduledTasksView(new ScheduledTasksViewModel(service)) { Dock = DockStyle.Fill };
        form.Controls.Add(view); form.Show();
        var tree = Find<System.Windows.Forms.TreeView>(view, "ScheduledTaskLogTree");
        var watch = Stopwatch.StartNew();
        while (tree.Nodes.Count != 3 && watch.Elapsed < TimeSpan.FromSeconds(5)) { System.Windows.Forms.Application.DoEvents(); Thread.Sleep(10); }
        var tabs = Find<TabControl>(view, "ScheduledTaskTabs");
        Assert.Equal(new[] { "Logs", "Setup" }, tabs.TabPages.Cast<TabPage>().Select(t => t.Text));
        Assert.Equal("Logs", tabs.SelectedTab!.Text);
        var root = tree.Nodes[id.ToString()]!;
        Assert.Contains("Scheduled: 2027-01-02", root.Text); Assert.Contains("Ran:", root.Text); Assert.Equal("Running", root.ImageKey);
        Assert.Equal("Unscheduled", tree.Nodes[disabledId.ToString()]!.ImageKey);
        Assert.Equal("Scheduled", tree.Nodes[scheduledId.ToString()]!.ImageKey);
        var leaf = root.Nodes[0].Nodes[0].Nodes.Cast<TreeNode>().SelectMany(day => day.Nodes.Cast<TreeNode>()).Single(node => node.Name == run.Id.ToString());
        Assert.Equal("Succeeded", leaf.ImageKey);
        tree.SelectedNode = leaf; System.Windows.Forms.Application.DoEvents();
        var output = Find<TextBox>(view, "ScheduledTaskStandardOutput"); Assert.True(output.ReadOnly); Assert.Contains("complete stdout beginning", output.Text);
        var more = Find<Button>(view, "ScheduledTaskMoreOutput"); Assert.True(more.Enabled); more.PerformClick(); System.Windows.Forms.Application.DoEvents();
        Assert.Contains("final stdout line", output.Text); Assert.False(more.Enabled);
        tree.SelectedNode = root.Nodes.Cast<TreeNode>().Single(n => n.Text.StartsWith("Load older")); System.Windows.Forms.Application.DoEvents();
        Assert.Contains(tree.Nodes[id.ToString()]!.Nodes.Cast<TreeNode>(), n => n.Text == "2025");
        var handle = form.Handle;
        var inspection = Task.Run(() => { using var automation = new UIA3Automation(); return automation.FromHandle(handle).FindFirstDescendant(x => x.ByAutomationId("ScheduledTaskTabs")).AsTab().TabItems.Select(t => t.Name).ToArray(); });
        watch.Restart(); while (!inspection.IsCompleted && watch.Elapsed < TimeSpan.FromSeconds(15)) { System.Windows.Forms.Application.DoEvents(); Thread.Sleep(10); }
        Assert.True(inspection.IsCompleted); Assert.Equal(new[] { "Logs", "Setup" }, inspection.GetAwaiter().GetResult());
        view.CloseAsync().AsTask().GetAwaiter().GetResult();
    }
    private static T Find<T>(Control control, string name) where T : Control => Controls(control).OfType<T>().Single(x => x.Name == name);
    private static IEnumerable<Control> Controls(Control parent) { foreach (Control child in parent.Controls) { yield return child; foreach (var nested in Controls(child)) yield return nested; } }
}
