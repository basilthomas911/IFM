using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Extensions;
using TomasAI.IFM.UI.Net.Models;
using TomasAI.IFM.UI.Net.ViewModels.SystemAdmin;

namespace TomasAI.IFM.UI.Net.Views.SystemAdmin;

/// <summary>Displays the NATS-only database-backup protection-set dashboard.</summary>
public partial class BackupDatabasesView : UserControl, IAsyncFormControl
{
    readonly DatabaseBackupViewModel _viewModel;
    readonly Label _backupModeLabel = new();
    readonly ComboBox _backupMode = new();
    Task? _initializeTask;

    /// <summary>Creates the database-backup view.</summary>
    public BackupDatabasesView(DatabaseBackupViewModel viewModel)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        InitializeComponent();
        clbDatabases.ItemCheck += clbDatabases_ItemCheck;
        ConfigureModeControls();
    }

    /// <inheritdoc />
    public void Open()
        => _initializeTask ??= _viewModel.InitializeAsync(CancellationToken.None);

    /// <inheritdoc />
    public void Close() => _ = ((IAsyncFormControl)this).CloseAsync();

    async ValueTask IAsyncFormControl.CloseAsync()
    {
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
        }
        catch (Exception exception)
        {
            this.ShowErrorMessage(exception.Message, "Database Backup");
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
        _backupModeLabel.Location = new Point(300, 6);
        _backupModeLabel.Text = "Mode:";
        _backupMode.DropDownStyle = ComboBoxStyle.DropDownList;
        _backupMode.Name = "ddlBackupMode";
        _backupMode.Font = radDiffBackup.Font;
        _backupMode.Location = new Point(355, 2);
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
        => this.Post(() => this.ShowErrorMessage(message, "Database Backup"));

    void RefreshRequested(Guid operationId)
        => this.Post(() => _ = RefreshOnUiAsync());

    async Task RefreshOnUiAsync()
    {
        await _viewModel.RefreshAsync();
        BindState();
    }

    void BindState()
    {
        Cursor = _viewModel.IsBusy ? Cursors.WaitCursor : Cursors.Default;
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
        clbDatabases.Enabled = !_viewModel.IsBusy && clbDatabases.Items.Count > 0;
        btnRun.Enabled = !_viewModel.IsBusy && clbDatabases.CheckedItems.Count > 0;
        BindOperationStatus(clbDatabases.SelectedItem?.ToString());
    }

    void clbDatabases_ItemCheck(object? sender, ItemCheckEventArgs e)
    {
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

    async void radDiffBackup_CheckedChanged(object sender, EventArgs e)
    {
        if (!radDiffBackup.Checked) return;
        _viewModel.SelectSource(BackupSource.LocalWorkstation);
        await _viewModel.RefreshAsync();
    }

    async void radFullBackup_CheckedChanged(object sender, EventArgs e)
    {
        if (!radFullBackup.Checked) return;
        _viewModel.SelectSource(BackupSource.AwsCloud);
        await _viewModel.RefreshAsync();
    }

    void nudCommandTimeout_ValueChanged(object sender, EventArgs e) { }

    void clbDatabases_SelectedIndexChanged(object sender, EventArgs e)
    {
        var protectionSet = clbDatabases.SelectedItem?.ToString();
        _viewModel.SelectProtectionSet(protectionSet);
        BindOperationStatus(protectionSet);
    }
}
