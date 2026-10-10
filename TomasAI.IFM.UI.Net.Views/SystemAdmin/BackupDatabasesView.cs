using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Extensions;
using TomasAI.IFM.UI.Net.Models;
using TomasAI.IFM.UI.Net.ViewModels.SystemAdmin;

namespace TomasAI.IFM.UI.Net.Views.SystemAdmin;

/// <summary>Displays the NATS-only database-backup protection-set dashboard.</summary>
public partial class BackupDatabasesView : DarkTradingView, IAsyncFormControl
{
    readonly DatabaseBackupViewModel _viewModel;
    readonly Label _backupModeLabel = new();
    readonly ComboBox _backupMode = new();
    Task? _initializeTask;
    bool _bindingState;

    /// <summary>Creates the database-backup view.</summary>
    public BackupDatabasesView(DatabaseBackupViewModel viewModel)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        InitializeComponent();
        clbDatabases.ItemCheck += clbDatabases_ItemCheck;
        ConfigureModeControls();
        CreateBackupTabs();
    }

    /// <inheritdoc />
    public void Open()
        => _initializeTask ??= _viewModel.InitializeAsync(CancellationToken.None);

    /// <inheritdoc />
    public void Close() => UiExceptionReporter.Observe(((IAsyncFormControl)this).CloseAsync(), nameof(IAsyncFormControl.CloseAsync), this);

    async ValueTask IAsyncFormControl.CloseAsync()
    {
        _outputCancellation.Cancel();
        _logsCancellation.Cancel();
        Unsubscribe();
        await _viewModel.StopAsync(CancellationToken.None);
        await _viewModel.DisposeAsync();
    }

    void IFormControl.Resize(Control parentControl) { }

    async void BackupDatabasesView_Load(object sender, EventArgs e)
    {
        ConfigureControls();
        Subscribe();
        try
        {
            _initializeTask ??= _viewModel.InitializeAsync(CancellationToken.None);
            await _initializeTask;
            BindState();
            await RefreshLogsAsync();
            await RefreshSetupHealthAsync(BackupSource.LocalWorkstation);
        }
        catch (Exception exception)
        {
            UiExceptionReporter.Report(exception, nameof(BackupDatabasesView), nameof(BackupDatabasesView_Load), this);
            _logStatus.Text = exception.Message;
        }
    }

    void ConfigureControls()
    {
        radDiffBackup.Text = "Local Workstation";
        radFullBackup.Text = "AWS Cloud";
        radDiffBackup.Checked = true;
        radFullBackup.Checked = false;
        lblCommandTimeout.Visible = false;
        nudCommandTimeout.Visible = false;
        btnRun.Text = "Request Backup";
        _viewModel.SelectSource(BackupSource.LocalWorkstation);
        _backupMode.SelectedItem = DatabaseBackupMode.Full;
        _viewModel.SelectBackupMode(DatabaseBackupMode.Full);
    }

    void ConfigureModeControls()
    {
        _backupModeLabel.Name = "lblBackupMode";
        _backupModeLabel.AutoSize = true;
        _backupModeLabel.Font = radDiffBackup.Font;
        _backupModeLabel.ForeColor = Color.White;
        _backupModeLabel.Location = new Point(0, 6);
        _backupModeLabel.Text = "Backup mode:";
        _backupMode.DropDownStyle = ComboBoxStyle.DropDownList;
        _backupMode.Name = "ddlBackupMode";
        _backupMode.Font = radDiffBackup.Font;
        _backupMode.Location = new Point(105, 2);
        _backupMode.Size = new Size(145, 26);
        _backupMode.Items.AddRange([
            DatabaseBackupMode.Full,
            DatabaseBackupMode.Automatic,
            DatabaseBackupMode.Incremental]);
        _backupMode.SelectedIndexChanged += (_, _) =>
        {
            if (_backupMode.SelectedItem is DatabaseBackupMode mode)
                _viewModel.SelectBackupMode(mode);
            UpdateBackupModeAccessibility();
        };
        UpdateBackupModeAccessibility();
        pnlBackupType.Controls.Add(_backupModeLabel);
        pnlBackupType.Controls.Add(_backupMode);
        _backupMode.BringToFront();
        _backupModeLabel.BringToFront();
    }

    void UpdateBackupModeAccessibility()
    {
        var selected = _backupMode.SelectedItem?.ToString() ?? string.Empty;
        _backupMode.AccessibleName = "Database backup mode; selected=" + selected
            + "; catalog: "
            + string.Join(", ", _backupMode.Items.Cast<object>());
    }

    void Subscribe()
    {
        _viewModel.StateChanged += StateChanged;
        _viewModel.Error += ShowSafeError;
        _viewModel.RefreshRequested += RefreshRequested;
    }

    void Unsubscribe()
    {
        _viewModel.StateChanged -= StateChanged;
        _viewModel.Error -= ShowSafeError;
        _viewModel.RefreshRequested -= RefreshRequested;
    }

    void StateChanged() => this.Post(BindState);

    void ShowSafeError(string message)
        => this.Post(() => _logStatus.Text = message);

    void RefreshRequested(Guid operationId)
        => this.Post(() => UiExceptionReporter.Observe(RefreshOnUiAsync(), nameof(RefreshOnUiAsync), this));

    async Task RefreshOnUiAsync()
    {
        await _viewModel.RefreshAsync();
        BindState();
    }

    void BindState()
    {
        Cursor = _viewModel.IsBusy ? Cursors.WaitCursor : Cursors.Default;
        _sources.Enabled = !_viewModel.IsBusy;
        _bindingState = true;
        try
        {
            var selected = clbDatabases.SelectedItem?.ToString();
            var checkedIds = clbDatabases.CheckedItems.Cast<object>()
                .Select(item => item.ToString()).Where(item => item is not null).ToHashSet();
            clbDatabases.Items.Clear();
            foreach (var protectionSet in _viewModel.State.ProtectionSets)
            {
                var index = clbDatabases.Items.Add(protectionSet.Id);
                clbDatabases.SetItemChecked(index, checkedIds.Contains(protectionSet.Id));
            }
            clbDatabases.AccessibleName = "Database protection sets; catalog: "
                + string.Join(", ", _viewModel.State.ProtectionSets.Select(item => item.Id));
            if (clbDatabases.Items.Count > 0)
            {
                var selectedIndex = selected is null
                    ? 0
                    : Math.Max(0, clbDatabases.Items.IndexOf(selected));
                clbDatabases.SelectedIndex = selectedIndex;
            }
        }
        finally { _bindingState = false; }
        clbDatabases.Enabled = !_viewModel.IsBusy && clbDatabases.Items.Count > 0;
        btnRun.Enabled = !_viewModel.IsBusy && clbDatabases.CheckedItems.Count > 0;
        BindOperationStatus(clbDatabases.SelectedItem?.ToString());
        if (!_viewModel.IsBusy) UiExceptionReporter.Observe(RefreshLogsAsync(), nameof(RefreshLogsAsync), this);
    }

    void clbDatabases_ItemCheck(object? sender, ItemCheckEventArgs e)
    {
        var protectionSet = clbDatabases.Items[e.Index]?.ToString();
        if (e.NewValue == CheckState.Checked && !_viewModel.State.ProtectionSets.Any(item => item.Id == protectionSet && item.Enabled))
        {
            e.NewValue = CheckState.Unchecked;
            _setupStatus.Text = "This protection set is disabled on the backup host.";
            return;
        }
        var checkedCount = clbDatabases.CheckedItems.Count;
        if (e.CurrentValue != CheckState.Checked && e.NewValue == CheckState.Checked)
            checkedCount++;
        else if (e.CurrentValue == CheckState.Checked && e.NewValue != CheckState.Checked)
            checkedCount--;
        btnRun.Enabled = !_viewModel.IsBusy && checkedCount > 0;
    }

    void BindOperationStatus(string? protectionSet)
    {
        lbStatusMessages.Items.Clear();
        if (string.IsNullOrWhiteSpace(protectionSet))
            return;
        var latestVerified = _viewModel.State.LatestVerified;
        var latestRestoreTested = _viewModel.State.LatestRestoreTested;
        if (string.IsNullOrWhiteSpace(_restorePoint.Text) && latestVerified is not null) _restorePoint.Text = latestVerified.RestorePointId;
        lbStatusMessages.Items.Add(latestVerified is null
            ? "Latest verified point: none"
            : $"Latest verified point: {latestVerified.RestorePointId} ({EasternTime.FromUtc(latestVerified.VerifiedUtc):g})");
        lbStatusMessages.Items.Add(latestRestoreTested is null
            ? "Latest restore-tested point: none"
            : $"Latest restore-tested point: {latestRestoreTested.RestorePointId} ({EasternTime.FromUtc(latestRestoreTested.RestoreTestedUtc):g})");
        foreach (var operation in _viewModel.State.RecentOperations.Where(item => item.ProtectionSet == protectionSet))
        {
            lbStatusMessages.Items.Add(
                $"{operation.OperationId:N} | {operation.RequestedMode}/{operation.ResolvedMode} | {operation.Phase} | {operation.ProgressPercent}% | {operation.Outcome} | {operation.SafeDiagnosticReference}");
        }
    }

    async void btnRun_Click(object sender, EventArgs e)
    {
        var protectionSets = clbDatabases.CheckedItems.Cast<object>()
            .Select(item => item.ToString()).Where(item => !string.IsNullOrWhiteSpace(item)).Cast<string>().ToArray();
        await _viewModel.RequestBackupsAsync(protectionSets);
        await _viewModel.RefreshAsync();
    }

    // Source tabs own selection. The hidden compatibility radio buttons must not race their queries.
    void radDiffBackup_CheckedChanged(object sender, EventArgs e) { }
    void radFullBackup_CheckedChanged(object sender, EventArgs e) { }

    void nudCommandTimeout_ValueChanged(object sender, EventArgs e) { }

    async void clbDatabases_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (_bindingState) return;
        _restorePoint.Clear();
        var protectionSet = clbDatabases.SelectedItem?.ToString();
        _viewModel.SelectProtectionSet(protectionSet);
        await _viewModel.RefreshAsync();
    }
}
