using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Extensions;
using TomasAI.IFM.UI.Net.Models.SystemAdmin;
using TomasAI.IFM.UI.Net.ViewModels.SystemAdmin;
namespace TomasAI.IFM.UI.Net.Views.SystemAdmin;
/// <summary>Displays actor-owned schedules, timing editor and persisted execution receipts.</summary>
public sealed partial class ScheduledTasksView : DarkTradingView, IAsyncFormControl
{
    private readonly ScheduledTasksViewModel _viewModel;
    private readonly DataGridView _schedules = Grid("ScheduledTaskList");
    private readonly DataGridView _runs = Grid("ScheduledTaskRuns");
    private readonly ComboBox _task = new() { Name = "ScheduledTaskProject", DropDownStyle = ComboBoxStyle.DropDownList, Width = 230 };
    private readonly TextBox _name = Edit("ScheduledTaskName", 230);
    private readonly TextBox _cron = Edit("ScheduledTaskCron", 230);
    private readonly TextBox _zone = Edit("ScheduledTaskTimeZone", 180);
    private readonly TextBox _environment = Edit("ScheduledTaskEnvironment", 110);
    private readonly TextBox _host = Edit("ScheduledTaskHost", 160);
    private readonly CheckBox _oneTime = new() { Name = "ScheduledTaskOneTime", Text = "One-time schedule", AutoSize = true };
    private readonly DateTimePicker _start = DatePicker("ScheduledTaskStart");
    private readonly DateTimePicker _end = DatePicker("ScheduledTaskEnd");
    private readonly DateTimePicker _time = new() { Name = "ScheduledTaskDailyTime", Format = DateTimePickerFormat.Time, ShowUpDown = true, Width = 110 };
    private readonly CheckedListBox _days = new() { Name = "ScheduledTaskDays", Height = 100, Width = 110, CheckOnClick = true };
    private readonly NumericUpDown _runtime = new() { Name = "ScheduledTaskMaximumRuntime", Minimum = 1, Maximum = 86400, Value = 1800, Width = 100 };
    private readonly NumericUpDown _tolerance = new() { Name = "ScheduledTaskDispatchTolerance", Minimum = 1, Maximum = 3600, Value = 60, Width = 100 };
    private readonly Label _status = new() { Name = "ScheduledTaskStatus", Dock = DockStyle.Fill, AutoEllipsis = true, ForeColor = Color.White };
    private readonly TextBox _preview = new() { Name = "ScheduledTaskPreview", Multiline = true, ReadOnly = true, Dock = DockStyle.Fill, ScrollBars = ScrollBars.Vertical, BackColor = Color.Black, ForeColor = Color.White };
    private readonly TextBox _runDetails = new() { Name = "ScheduledTaskRunDetails", Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, Dock = DockStyle.Fill, BackColor = Color.Black, ForeColor = Color.White };
    private readonly TextBox _resolutionReason = Edit("ScheduledTaskResolutionReason", 300);
    private Button? _resolve;
    private readonly List<Button> _buttons = [];
    private Task? _initialize;
    private bool _binding;
    private bool _newSchedule;
    private bool _closed;
    /// <summary>Creates a docked task editor with explicit lifecycle ownership.</summary>
    public ScheduledTasksView(ScheduledTasksViewModel viewModel)
    {
        _viewModel = viewModel;
        Name = "ScheduledTasksView"; Dock = DockStyle.Fill; BackColor = Color.Black;
        _environment.Text = viewModel.Environment; _host.Text = viewModel.HostId;
        _zone.Text = "America/New_York"; _cron.Text = "0 1 17 ? * MON-FRI";
        _time.Value = DateTime.Today.AddHours(17).AddMinutes(1);
        _days.Items.AddRange(["SUN", "MON", "TUE", "WED", "THU", "FRI", "SAT"]);
        for (var day = 1; day <= 5; day++) _days.SetItemChecked(day, true);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Color.Black, ColumnCount = 1, RowCount = 5, Padding = new Padding(6) };
        layout.RowStyles.Add(new(SizeType.Absolute, 38)); layout.RowStyles.Add(new(SizeType.Percent, 35));
        layout.RowStyles.Add(new(SizeType.Absolute, 220)); layout.RowStyles.Add(new(SizeType.Percent, 65)); layout.RowStyles.Add(new(SizeType.Absolute, 28));
        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Fill, BackColor = Color.Black, WrapContents = false, AutoScroll = true };
        Button AddButton(string caption, Func<Task> action)
        {
            var button = new Button { Name = "ScheduledTask" + caption.Replace(" ", ""), Text = caption, AutoSize = true, BackColor = Color.FromArgb(40, 40, 40), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            button.Click += async (_, _) => await RunAsync(action); _buttons.Add(button); toolbar.Controls.Add(button); return button;
        }
        AddButton("Refresh", RefreshAsync);
        AddButton("New", () => { _newSchedule = true; _viewModel.SelectedScheduleId = null; _name.Clear(); _task.Enabled = true; return Task.CompletedTask; });
        AddButton("Save", () => _viewModel.SaveAsync(_newSchedule ? null : Selected?.Id, _newSchedule ? 0 : Selected?.Revision ?? 0, ReadSchedule()));
        AddButton("Enable", () => Selected is { } selected ? _viewModel.SetEnabledAsync(selected.Id, selected.Revision, true) : Task.CompletedTask);
        AddButton("Disable", () => Selected is { } selected ? _viewModel.SetEnabledAsync(selected.Id, selected.Revision, false) : Task.CompletedTask);
        AddButton("Remove", () => Selected is { } selected ? _viewModel.RemoveAsync(selected.Id, selected.Revision) : Task.CompletedTask);
        AddButton("Run Now", () => Selected is { } selected ? _viewModel.RunNowAsync(selected) : Task.CompletedTask);
        AddButton("Preview", PreviewAsync);
        _resolve = AddButton("Resolve as Failed", ResolveReviewedRunAsync);
        var editor = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, BackColor = Color.Black };
        editor.ColumnStyles.Add(new(SizeType.Percent, 68)); editor.ColumnStyles.Add(new(SizeType.Percent, 32));
        var fields = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.Black };
        AddField(fields, "Task:", _task); AddField(fields, "Name:", _name); AddField(fields, "Environment:", _environment); AddField(fields, "Host:", _host);
        AddField(fields, "Cron:", _cron); AddField(fields, "Time zone:", _zone); fields.Controls.Add(_oneTime);
        AddField(fields, "Start (zone):", _start); AddField(fields, "End (zone):", _end); AddField(fields, "Runtime (seconds):", _runtime); AddField(fields, "Dispatch tolerance (seconds):", _tolerance);
        AddField(fields, "Daily time:", _time); AddField(fields, "Days:", _days);
        var useTime = new Button { Text = "Set cron from days/time", AutoSize = true, BackColor = Color.FromArgb(40, 40, 40), ForeColor = Color.White };
        useTime.Click += (_, _) => _cron.Text = $"0 {_time.Value.Minute} {_time.Value.Hour} ? * {string.Join(",", _days.CheckedItems.Cast<string>())}";
        fields.Controls.Add(useTime); editor.Controls.Add(fields, 0, 0); editor.Controls.Add(_preview, 1, 0);
        var runPanel = new SplitContainer { Name = "ScheduledTaskRunSplit", Dock = DockStyle.Fill, BackColor = Color.Black };
        runPanel.Panel1.Controls.Add(_runs);
        var detailsPanel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, BackColor = Color.Black };
        detailsPanel.RowStyles.Add(new(SizeType.Percent, 100)); detailsPanel.RowStyles.Add(new(SizeType.Absolute, 38));
        detailsPanel.Controls.Add(_runDetails, 0, 0);
        var reasonPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, BackColor = Color.Black, WrapContents = false };
        AddField(reasonPanel, "Review reason:", _resolutionReason); detailsPanel.Controls.Add(reasonPanel, 0, 1);
        runPanel.Panel2.Controls.Add(detailsPanel);
        runPanel.SizeChanged += (_, _) => { if (runPanel.Width > 100) runPanel.SplitterDistance = runPanel.Width / 2; };
        _runs.SelectionChanged += (_, _) => BindRunDetails();
        layout.Controls.Add(toolbar, 0, 0); layout.Controls.Add(_schedules, 0, 1); layout.Controls.Add(editor, 0, 2); layout.Controls.Add(runPanel, 0, 3); layout.Controls.Add(_status, 0, 4); CreateLogTabs(layout);
        _schedules.SelectionChanged += async (_, _) =>
        {
            if (_binding || _schedules.CurrentRow?.Tag is not ScheduledTaskDefinitionUiModel selected) return;
            _newSchedule = false; _viewModel.SelectedScheduleId = selected.Id; BindEditor(selected);
            await RunAsync(_viewModel.RefreshAsync);
        };
        _viewModel.StateChanged += BindState;
        _viewModel.Error += ShowError;
        _viewModel.RefreshRequested += OnRefreshRequested;
        Load += async (_, _) => { Open(); await RunAsync(() => _initialize!); };
    }
    private ScheduledTaskDefinitionUiModel? Selected => _viewModel.State.Schedules.FirstOrDefault(s => s.Id == _viewModel.SelectedScheduleId);
    /// <inheritdoc />
    public void Open() => _initialize ??= _viewModel.InitializeAsync();
    /// <inheritdoc />
    public void Close() => UiExceptionReporter.Observe(CloseAsync(), nameof(CloseAsync), this);
    /// <inheritdoc />
    public async ValueTask CloseAsync()
    {
        if (_closed) return; _closed = true;
        _viewModel.StateChanged -= BindState; _viewModel.Error -= ShowError; _viewModel.RefreshRequested -= OnRefreshRequested;
        _outputSelection.Cancel();
        _logImages.Dispose();
        await _viewModel.DisposeAsync();
    }
    /// <inheritdoc />
    void IFormControl.Resize(Control parentControl) { }
    /// <summary>Reloads the chosen host's persisted schedule list and selected run page.</summary>
    private Task RefreshAsync() { _viewModel.Environment = _environment.Text; _viewModel.HostId = _host.Text; return _viewModel.RefreshAsync(); }
    /// <summary>Displays UTC and configured-zone occurrences without saving configuration.</summary>
    private async Task PreviewAsync()
    {
        var preview = await _viewModel.PreviewAsync(ReadSchedule());
        if (preview is null) return;
        var zone = TimeZoneInfo.FindSystemTimeZoneById(_zone.Text);
        _preview.Text = preview.Valid ? string.Join(Environment.NewLine, preview.NextFireTimesUtc.Select(t => $"{TimeZoneInfo.ConvertTime(t, zone):yyyy-MM-dd HH:mm:ss zzz} / {t:HH:mm:ss} UTC")) : string.Join(Environment.NewLine, preview.Errors);
    }
    /// <summary>Converts explicit-zone date pickers into UTC configuration bounds.</summary>
    private ScheduledTaskScheduleUiModel ReadSchedule()
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(_zone.Text);
        DateTimeOffset? Bound(DateTimePicker picker) => picker.Checked ? new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(picker.Value, DateTimeKind.Unspecified), zone)) : null;
        return new(_task.SelectedValue as string ?? _task.Text, _name.Text, _environment.Text, _host.Text, _oneTime.Checked,
            _cron.Text, _zone.Text, Bound(_start), Bound(_end), (int)_runtime.Value, (int)_tolerance.Value, "");
    }
    /// <summary>Loads one persisted schedule into its timing editor.</summary>
    private void BindEditor(ScheduledTaskDefinitionUiModel definition)
    {
        var schedule = definition.Schedule; _task.SelectedValue = schedule.TaskKey; _task.Enabled = false;
        _name.Text = schedule.Name; _cron.Text = schedule.Expression; _zone.Text = schedule.TimeZoneId; _environment.Text = schedule.Environment; _host.Text = schedule.HostId;
        _oneTime.Checked = schedule.OneTime; _runtime.Value = Math.Clamp(schedule.MaximumRuntimeSeconds, 1, 86400); _tolerance.Value = Math.Clamp(schedule.DispatchToleranceSeconds, 1, 3600);
        var zone = TimeZoneInfo.FindSystemTimeZoneById(schedule.TimeZoneId);
        _start.Checked = schedule.StartsAtUtc is not null; if (schedule.StartsAtUtc is { } start) _start.Value = TimeZoneInfo.ConvertTime(start, zone).DateTime;
        _end.Checked = schedule.EndsAtUtc is not null; if (schedule.EndsAtUtc is { } end) _end.Value = TimeZoneInfo.ConvertTime(end, zone).DateTime;
    }
    /// <summary>Rebinds immutable projected rows while retaining the selected actor identity.</summary>
    private void BindState()
    {
        if (_closed || IsDisposed) return;
        if (InvokeRequired) { this.Post(BindState); return; }
        _binding = true;
        try
        {
            var firstSelection = !_newSchedule && _viewModel.SelectedScheduleId is null ? _viewModel.State.Schedules.FirstOrDefault(s => !s.Removed) : null;
            if (firstSelection is not null) _viewModel.SelectedScheduleId = firstSelection.Id;
            var selectedTask = _task.SelectedValue as string;
            _task.DataSource = _viewModel.State.Projects; _task.DisplayMember = nameof(ScheduledTaskProjectUiModel.Name); _task.ValueMember = nameof(ScheduledTaskProjectUiModel.TaskKey);
            if (selectedTask is not null) _task.SelectedValue = selectedTask;
            _schedules.Rows.Clear();
            if (_schedules.Columns.Count == 0) foreach (var name in new[] { "Name", "Task", "Enabled", "Timing", "Desired", "Applied", "Installation", "Active run" }) _schedules.Columns.Add(name, name);
            foreach (var definition in _viewModel.State.Schedules.Where(s => !s.Removed))
            {
                var index = _schedules.Rows.Add(definition.Schedule.Name, definition.Schedule.TaskKey, definition.Enabled, definition.Schedule.OneTime ? definition.Schedule.StartsAtUtc : definition.Schedule.Expression, definition.DesiredRevision, definition.AppliedRevision, definition.InstallationStatus, definition.ActiveRunId);
                _schedules.Rows[index].Tag = definition;
                if (definition.Id == _viewModel.SelectedScheduleId) _schedules.CurrentCell = _schedules.Rows[index].Cells[0];
            }
            var runId = (_runs.CurrentRow?.DataBoundItem as ScheduledTaskRunUiModel)?.Id;
            _runs.DataSource = _viewModel.State.Runs;
            foreach (var name in new[] { "Detail", "Revision", "OperationCommandId", "StandardOutputTail", "StandardErrorTail", "OutputDirectory" })
                if (_runs.Columns.Contains(name)) _runs.Columns[name].Visible = false;
            foreach (DataGridViewRow row in _runs.Rows)
                if (row.DataBoundItem is ScheduledTaskRunUiModel run && run.Id == runId) _runs.CurrentCell = row.Cells[0];
            foreach (var button in _buttons) button.Enabled = !_viewModel.IsBusy;
            BindRunDetails();
            if (!_viewModel.IsBusy) UiExceptionReporter.Observe(new ValueTask(RefreshLogsAsync()), nameof(RefreshLogsAsync), this);
            if (firstSelection is not null) { BindEditor(firstSelection); OnRefreshRequested(); }
            _status.Text = _viewModel.State.HostStatus + (Selected is { } s ? $" | {s.InstallationStatus}: {s.InstallationDetail}" : "");
        }
        finally { _binding = false; }
    }
    /// <summary>Marshals a public notification into a persisted dashboard refresh.</summary>
    private void OnRefreshRequested() => this.Post(() => { if (!_closed) UiExceptionReporter.Observe(new ValueTask(RunAsync(_viewModel.RefreshAsync)), nameof(OnRefreshRequested), this); });
    /// <summary>Reports service failures in the persistent status line.</summary>
    private void ShowError(string message) { if (!_closed) this.Post(() => _status.Text = message); }
    /// <summary>Observes async button actions and handles cancelled view closure.</summary>
    private async Task RunAsync(Func<Task> action)
    { try { await action(); } catch (OperationCanceledException) when (_closed) { } catch (Exception exception) { ShowError(exception.Message); } }
    /// <summary>Creates a labelled field with a black panel and colon-ended caption.</summary>
    private static void AddField(FlowLayoutPanel parent, string caption, Control control)
    {
        var panel = new FlowLayoutPanel { AutoSize = true, BackColor = Color.Black, Margin = new Padding(4), WrapContents = false };
        panel.Controls.Add(new Label { Text = caption, AutoSize = true, ForeColor = Color.White, Padding = new Padding(0, 5, 4, 0) });
        control.BackColor = Color.Black; control.ForeColor = Color.White; panel.Controls.Add(control); parent.Controls.Add(panel);
    }
    /// <summary>Shows the selected persisted outcome, identities and bounded output without reading command memory.</summary>
    private void BindRunDetails()
    {
        var run = _runs.CurrentRow?.DataBoundItem as ScheduledTaskRunUiModel;
        if (_resolve is not null) _resolve.Enabled = !_viewModel.IsBusy && run?.Status == "Uncertain";
        _runDetails.Text = run is null ? "Select a run to view its outcome and output." :
            $"Run: {run.Id}\r\nStatus: {run.Status}\r\nStage: {run.Stage}\r\nValue date: {run.ValueDate}\r\nStarted: {run.StartedAtUtc:O}\r\nFinished: {run.FinishedAtUtc:O}\r\nExit code: {run.ExitCode}\r\nOperation: {run.OperationCommandId}\r\nProcess: {run.ProcessId}\r\nOutput artifact: {run.OutputDirectory}\r\n\r\n{run.Detail}\r\n\r\nStandard output (tail):\r\n{run.StandardOutputTail}\r\n\r\nStandard error (tail):\r\n{run.StandardErrorTail}";
    }
    /// <summary>Requires a review reason before resolving an uncertain occurrence; the operation does not rerun the task.</summary>
    private async Task ResolveReviewedRunAsync()
    {
        if (_runs.CurrentRow?.DataBoundItem is not ScheduledTaskRunUiModel { Status: "Uncertain" } run) return;
        if (string.IsNullOrWhiteSpace(_resolutionReason.Text)) throw new InvalidOperationException("Enter the result of your review before resolving the interrupted run.");
        await _viewModel.ResolveUncertainAsync(run, _resolutionReason.Text.Trim());
    }
    /// <summary>Creates a read-only dark grid with visible bottom scrollbars.</summary>
    private static DataGridView Grid(string name) => new() { Name = name, Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells, SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false,
        BackgroundColor = Color.Black, DefaultCellStyle = new() { BackColor = Color.Black, ForeColor = Color.White }, EnableHeadersVisualStyles = false,
        ColumnHeadersDefaultCellStyle = new() { BackColor = Color.Black, ForeColor = Color.White } };
    /// <summary>Creates an editable dark text field.</summary>
    private static TextBox Edit(string name, int width) => new() { Name = name, Width = width, BackColor = Color.Black, ForeColor = Color.White };
    /// <summary>Creates an optional date bound displayed in the configured zone.</summary>
    private static DateTimePicker DatePicker(string name) => new() { Name = name, Format = DateTimePickerFormat.Custom, CustomFormat = "yyyy-MM-dd HH:mm:ss", ShowCheckBox = true, Checked = false, Width = 190 };
}
