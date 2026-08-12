using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Extensions;
using TomasAI.IFM.UI.Net.ViewModels.SystemAdmin;

namespace TomasAI.IFM.UI.Net.Views.SystemAdmin;

/// <summary>Displays the NATS-only database-backup protection-set dashboard.</summary>
public partial class BackupDatabasesView : UserControl, IAsyncFormControl
{
    readonly DatabaseBackupViewModel _viewModel;
    Task? _initializeTask;

    /// <summary>Creates the database-backup view.</summary>
    public BackupDatabasesView(DatabaseBackupViewModel viewModel)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        InitializeComponent();
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
        btnRun.Enabled = !_viewModel.IsBusy;
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
        if (clbDatabases.Items.Count > 0)
        {
            var selectedIndex = Math.Max(0, clbDatabases.Items.IndexOf(selected));
            clbDatabases.SelectedIndex = selectedIndex;
        }
        clbDatabases.Enabled = !_viewModel.IsBusy && clbDatabases.Items.Count > 0;
        BindOperationStatus(clbDatabases.SelectedItem?.ToString());
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
            : $"Latest verified point: {latestVerified.RestorePointId} ({latestVerified.VerifiedUtc:u})");
        lbStatusMessages.Items.Add(latestRestoreTested is null
            ? "Latest restore-tested point: none"
            : $"Latest restore-tested point: {latestRestoreTested.RestorePointId} ({latestRestoreTested.RestoreTestedUtc:u})");
        foreach (var operation in _viewModel.State.RecentOperations.Where(item => item.ProtectionSet == protectionSet))
        {
            lbStatusMessages.Items.Add(
                $"{operation.OperationId:N} | {operation.Phase} | {operation.ProgressPercent}% | {operation.Outcome} | {operation.SafeDiagnosticReference}");
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
