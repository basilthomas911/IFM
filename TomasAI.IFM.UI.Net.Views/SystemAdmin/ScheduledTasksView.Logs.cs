using System.Globalization;
using TomasAI.IFM.UI.Net.Models.SystemAdmin;
namespace TomasAI.IFM.UI.Net.Views.SystemAdmin;
/// <summary>Displays dated occurrence history and retained stdout beside the existing Setup editor.</summary>
public sealed partial class ScheduledTasksView
{
    private readonly TabControl _tabs = new() { Name = "ScheduledTaskTabs", Dock = DockStyle.Fill };
    private readonly TreeView _logTree = new() { Name = "ScheduledTaskLogTree", Dock = DockStyle.Fill, BackColor = Color.Black, ForeColor = Color.White, HideSelection = false };
    private readonly TextBox _stdout = new() { Name = "ScheduledTaskStandardOutput", Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = false, BackColor = Color.Black, ForeColor = Color.White, MaxLength = int.MaxValue };
    private readonly Label _outputStatus = new() { Name = "ScheduledTaskOutputStatus", AutoSize = true, ForeColor = Color.White };
    private readonly Button _moreOutput = new() { Name = "ScheduledTaskMoreOutput", Text = "Load more output", AutoSize = true, Enabled = false, BackColor = Color.FromArgb(40, 40, 40), ForeColor = Color.White };
    private readonly ImageList _logImages = new() { ImageSize = new Size(16, 16), ColorDepth = ColorDepth.Depth32Bit };
    private readonly Dictionary<Guid, ScheduledTaskRunPageUiModel> _logHistory = [];
    private readonly Dictionary<Guid, DateTimeOffset?> _nextFires = [];
    private readonly SemaphoreSlim _logsGate = new(1, 1);
    private CancellationTokenSource _outputSelection = new();
    private long _outputOffset;
    private bool _bindingLogs;
    private sealed record RunSelection(Guid ScheduleId, ScheduledTaskRunUiModel Run);
    private sealed record MoreHistory(Guid ScheduleId);

    /// <summary>Creates Logs first and preserves every existing control under Setup.</summary>
    private void CreateLogTabs(Control setup)
    {
        foreach (var (key, color) in new[] { ("Scheduled", Color.DodgerBlue), ("Unscheduled", Color.Gray), ("Running", Color.Yellow), ("Succeeded", Color.LimeGreen), ("Failed", Color.Red) })
        {
            var bitmap = new Bitmap(16, 16);
            using var graphics = Graphics.FromImage(bitmap);
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var brush = new SolidBrush(color);
            graphics.FillEllipse(brush, 2, 2, 12, 12);
            _logImages.Images.Add(key, bitmap);
        }
        _logTree.ImageList = _logImages;
        var logs = new TabPage("Logs") { Name = "ScheduledTaskLogsTab", BackColor = Color.Black };
        var setupTab = new TabPage("Setup") { Name = "ScheduledTaskSetupTab", BackColor = Color.Black };
        setupTab.Controls.Add(setup);
        var split = new SplitContainer { Name = "ScheduledTaskLogsSplit", Dock = DockStyle.Fill, BackColor = Color.Black };
        split.SizeChanged += (_, _) => { if (split.Width > 220) split.SplitterDistance = split.Width * 2 / 5; };
        split.Panel1.Controls.Add(_logTree);
        var right = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Color.Black, ColumnCount = 1, RowCount = 2 };
        right.RowStyles.Add(new(SizeType.Percent, 100)); right.RowStyles.Add(new(SizeType.Absolute, 42));
        right.Controls.Add(_stdout, 0, 0);
        var outputToolbar = new FlowLayoutPanel { Dock = DockStyle.Fill, BackColor = Color.Black, AutoScroll = true, WrapContents = false };
        outputToolbar.Controls.Add(_moreOutput); outputToolbar.Controls.Add(_outputStatus);
        right.Controls.Add(outputToolbar, 0, 1); split.Panel2.Controls.Add(right);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Color.Black, RowCount = 2, ColumnCount = 1 };
        layout.RowStyles.Add(new(SizeType.Absolute, 38)); layout.RowStyles.Add(new(SizeType.Percent, 100));
        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Fill, BackColor = Color.Black };
        var refresh = new Button { Name = "ScheduledTaskLogsRefresh", Text = "Refresh logs", AutoSize = true, BackColor = Color.FromArgb(40, 40, 40), ForeColor = Color.White };
        refresh.Click += async (_, _) => await RunAsync(RefreshAsync);
        toolbar.Controls.Add(refresh);
        foreach (var (caption, color) in new[] { ("Scheduled", Color.DodgerBlue), ("Unscheduled", Color.Gray), ("Running", Color.Yellow), ("Succeeded", Color.LimeGreen), ("Failed/uncertain", Color.Red) })
            toolbar.Controls.Add(new Label { Text = "\u25cf " + caption, AutoSize = true, ForeColor = color, Padding = new Padding(6) });
        layout.Controls.Add(toolbar, 0, 0); layout.Controls.Add(split, 0, 1); logs.Controls.Add(layout);
        _tabs.TabPages.Add(logs); _tabs.TabPages.Add(setupTab); Controls.Add(_tabs);
        _logTree.AfterSelect += async (_, e) =>
        {
            if (_bindingLogs) return;
            if (e.Node?.Tag is MoreHistory more) { await RunAsync(() => LoadOlderHistoryAsync(more.ScheduleId)); return; }
            _outputSelection.Cancel(); _outputSelection.Dispose(); _outputSelection = new(); _outputOffset = 0; _stdout.Clear(); _moreOutput.Enabled = false;
            if (e.Node?.Tag is RunSelection) await RunAsync(() => ReadOutputPageAsync(_outputSelection.Token));
            else _outputStatus.Text = "Select an occurrence to read retained standard output.";
        };
        _moreOutput.Click += async (_, _) => await RunAsync(() => ReadOutputPageAsync(_outputSelection.Token));
    }

    /// <summary>Refreshes the newest page for every task without discarding loaded older pages.</summary>
    private async Task RefreshLogsAsync()
    {
        if (_closed || !await _logsGate.WaitAsync(0)) return;
        try
        {
            foreach (var definition in _viewModel.State.Schedules)
            {
                var page = await _viewModel.LoadRunHistoryAsync(definition.Id, null);
                if (_closed) return;
                if (page is not null)
                {
                    var older = _logHistory.GetValueOrDefault(definition.Id);
                    _logHistory[definition.Id] = new([.. page.Runs.Concat(older?.Runs ?? []).DistinctBy(run => run.Id).OrderByDescending(run => run.IntendedFireTimeUtc)], older is null ? page.PagingState : older.PagingState);
                }
                var preview = definition.Enabled && !definition.Removed ? await _viewModel.PreviewAsync(definition.Schedule) : null;
                _nextFires[definition.Id] = preview?.NextFireTimesUtc.FirstOrDefault();
            }
            if (!_closed) BindLogTree();
        }
        finally { _logsGate.Release(); }
    }

    /// <summary>Fetches another opaque-cursor history page, making all retained runs reachable.</summary>
    private async Task LoadOlderHistoryAsync(Guid scheduleId)
    {
        if (!_logHistory.TryGetValue(scheduleId, out var old) || old.PagingState is null) return;
        await _logsGate.WaitAsync();
        try
        {
            var page = await _viewModel.LoadRunHistoryAsync(scheduleId, old.PagingState);
            if (page is null || _closed) return;
            _logHistory[scheduleId] = new([.. old.Runs.Concat(page.Runs).DistinctBy(run => run.Id)], page.PagingState);
            BindLogTree();
        }
        finally { _logsGate.Release(); }
    }

    /// <summary>Groups task occurrences by configured-zone year, month and day, preserving expansion and selection.</summary>
    private void BindLogTree()
    {
        var expanded = new HashSet<string>();
        void Remember(TreeNodeCollection nodes) { foreach (TreeNode node in nodes) { if (node.IsExpanded) expanded.Add(node.Name); Remember(node.Nodes); } }
        Remember(_logTree.Nodes);
        var selected = _logTree.SelectedNode?.Name;
        _bindingLogs = true;
        _logTree.BeginUpdate();
        try
        {
            _logTree.Nodes.Clear();
            foreach (var definition in _viewModel.State.Schedules.OrderBy(d => d.Schedule.Name))
            {
                var zone = TimeZoneInfo.FindSystemTimeZoneById(definition.Schedule.TimeZoneId);
                string Date(DateTimeOffset? value) => value is { } date && date != default ? TimeZoneInfo.ConvertTime(date, zone).ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture) : "--";
                var page = _logHistory.GetValueOrDefault(definition.Id);
                var latest = page?.Runs.OrderByDescending(r => r.IntendedFireTimeUtc).FirstOrDefault();
                var lastActual = page?.Runs.Where(r => r.StartedAtUtc.HasValue).OrderByDescending(r => r.StartedAtUtc).FirstOrDefault();
                var running = page?.Runs.FirstOrDefault(r => r.Status == "Running");
                var status = running is not null ? "Running" : !definition.Enabled || definition.Removed ? "Unscheduled" : latest?.Status == "Succeeded" ? "Succeeded" : latest?.Status is "Failed" or "Uncertain" or "Rejected" ? "Failed" : "Scheduled";
                var taskRoot = new TreeNode($"{definition.Schedule.Name} | Scheduled: {Date(_nextFires.GetValueOrDefault(definition.Id))} | Ran: {Date(lastActual?.StartedAtUtc)} | {status}") { Name = definition.Id.ToString(), Tag = definition, ImageKey = status, SelectedImageKey = status };
                _logTree.Nodes.Add(taskRoot);
                foreach (var run in page?.Runs ?? [])
                {
                    var date = TimeZoneInfo.ConvertTime(run.IntendedFireTimeUtc, zone);
                    TreeNode Group(TreeNode parent, string key, string caption)
                    {
                        var found = parent.Nodes[key]; if (found is not null) return found;
                        var group = new TreeNode(caption) { Name = key, ImageKey = "Unscheduled", SelectedImageKey = "Unscheduled" }; parent.Nodes.Add(group); return group;
                    }
                    var year = Group(taskRoot, $"{definition.Id}/{date:yyyy}", date.ToString("yyyy"));
                    var month = Group(year, $"{year.Name}/{date:MM}", date.ToString("MMMM"));
                    var day = Group(month, $"{month.Name}/{date:dd}", date.ToString("dd dddd"));
                    var color = run.Status switch { "Running" => "Running", "Succeeded" => "Succeeded", "Failed" or "Uncertain" or "Rejected" => "Failed", "Skipped" => "Unscheduled", _ => "Scheduled" };
                    day.Nodes.Add(new TreeNode($"Scheduled: {Date(run.IntendedFireTimeUtc)} | Ran: {Date(run.StartedAtUtc)} | {run.Status}") { Name = run.Id.ToString(), Tag = new RunSelection(definition.Id, run), ImageKey = color, SelectedImageKey = color });
                }
                if (page?.PagingState is not null) taskRoot.Nodes.Add(new TreeNode("Load older runs...") { Name = definition.Id + "/more", Tag = new MoreHistory(definition.Id), ImageKey = "Scheduled", SelectedImageKey = "Scheduled" });
            }
            void Restore(TreeNodeCollection nodes) { foreach (TreeNode node in nodes) { if (expanded.Contains(node.Name)) node.Expand(); if (node.Name == selected) _logTree.SelectedNode = node; Restore(node.Nodes); } }
            Restore(_logTree.Nodes);
        }
        finally { _logTree.EndUpdate(); _bindingLogs = false; }
    }

    /// <summary>Loads retained stdout pages; old selection responses cannot overwrite the current run.</summary>
    private async Task ReadOutputPageAsync(CancellationToken token)
    {
        if (_logTree.SelectedNode?.Tag is not RunSelection selection) return;
        _moreOutput.Enabled = false;
        try
        {
            var page = await _viewModel.LoadOutputAsync(selection.ScheduleId, selection.Run, _outputOffset, token);
            if (token.IsCancellationRequested || _closed || page is null) return;
            if (!page.Available)
            {
                _stdout.Text = string.IsNullOrEmpty(page.Text) ? selection.Run.StandardOutputTail : page.Text;
                _outputStatus.Text = "Full stdout is unavailable or expired; showing persisted tail.";
                return;
            }
            _stdout.AppendText(page.Text); _outputOffset = page.NextOffset;
            _outputStatus.Text = page.EndOfOutput ? "End of retained stdout (capture limits and retention apply)." : "More retained stdout is available.";
            _moreOutput.Enabled = !page.EndOfOutput || selection.Run.Status == "Running";
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
    }
}
