using System.ComponentModel;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.UI.Net.Models.SystemAdmin;
namespace TomasAI.IFM.UI.Net.Views.SystemAdmin;
/// <summary>Connects source setup, revision-matched policies and fresh-target restore drills.</summary>
public partial class BackupDatabasesView
{
    readonly TextBox _policyId = new() { Name = "BackupPolicyId", Width = 220, PlaceholderText = "Policy identifier" };
    readonly PropertyGrid _policyGrid = new() { Name = "BackupPolicySettings", Dock = DockStyle.Fill, ToolbarVisible = false, HelpVisible = true, BackColor = Color.Black, ViewBackColor = Color.Black, ViewForeColor = Color.White };
    readonly Label _setupStatus = new() { Name = "BackupSetupStatus", AutoSize = true, ForeColor = Color.White };
    readonly TextBox _restorePoint = new() { Name = "BackupRestorePoint", Width = 240, PlaceholderText = "Verified restore point ID" };
    readonly TextBox _targetProfile = new() { Name = "BackupRestoreTarget", Width = 180, PlaceholderText = "Restore target profile", Text = "development-on-demand" };
    readonly TextBox _validationProfile = new() { Name = "BackupValidationProfile", Width = 160, PlaceholderText = "Restore copy name", Text = "development-restore" };
    readonly Dictionary<BackupSource, PolicyEditor> _policyEditors = [];
    readonly Dictionary<BackupSource, string> _policyIdentifiers = [];
    BackupSource _setupSource = BackupSource.LocalWorkstation;
    readonly Dictionary<BackupSource, (string RestorePoint, string Target, string Validation, object? Mode)> _sourceInputs = [];
    /// <summary>Builds typed policy and restore editors; host settings are explicitly referenced rather than silently rewritten.</summary>
    void ConfigureSetupEditor()
    {
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, BackColor = Color.Black };
        layout.RowStyles.Add(new(SizeType.Absolute, 40)); layout.RowStyles.Add(new(SizeType.Absolute, 40)); layout.RowStyles.Add(new(SizeType.Percent, 65)); layout.RowStyles.Add(new(SizeType.Percent, 35)); layout.RowStyles.Add(new(SizeType.Absolute, 75));
        var tools = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true };
        var load = new Button { Name = "BackupLoadPolicy", Text = "Load policy", AutoSize = true };
        var save = new Button { Name = "BackupSavePolicy", Text = "Save policy", AutoSize = true };
        tools.Controls.Add(_policyId); tools.Controls.Add(load); tools.Controls.Add(save); tools.Controls.Add(_setupStatus);
        var restore = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true };
        var drill = new Button { Name = "BackupRestoreDrill", Text = "Restore Backup (fresh copy)", AutoSize = true };
        restore.Controls.Add(_restorePoint); restore.Controls.Add(_targetProfile); restore.Controls.Add(_validationProfile); restore.Controls.Add(drill);
        var host = new TextBox { Name = "BackupHostSettings", Dock = DockStyle.Fill, ForeColor = Color.White, BackColor = Color.Black, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = false,
            Text = "Host-owned configuration: DatabaseBackup.Sources.LocalWorkstation / AwsCloud; PostgreSql / Scylla; Publication.\nLocal: vault, staging, offline replica, native tools and chain limits. AWS: accounts, regions, vaults, role/KMS references and replication. Configure profiles on the backup host; secrets are never entered here. Schedules are managed under System Admin > Scheduled Tasks." };
        pnlBackupStatus.Controls.Clear(); layout.Controls.Add(pnlCommands, 0, 0); layout.Controls.Add(tools, 0, 1); layout.Controls.Add(_policyGrid, 0, 2); layout.Controls.Add(lbStatusMessages, 0, 3); layout.Controls.Add(restore, 0, 4);
        lbStatusMessages.Dock = DockStyle.Fill; pnlCommands.Dock = DockStyle.Fill; pnlBackupStatus.Controls.Add(layout);
        clbDatabases.Dock = DockStyle.Fill;
        pnlBackupDatabases.Panel1.Controls.Add(host); host.Dock = DockStyle.Bottom; host.Height = 150;
        load.Click += async (_, _) => await RunSetupAsync(async token =>
        {
            var result = await _viewModel.Service.LoadPolicyAsync(_policyId.Text.Trim(), token);
            if (!result.IsSuccess || result.Value is null) throw new InvalidOperationException(result.Error?.Message);
            var editor = new PolicyEditor(result.Value); _policyEditors[_setupSource] = editor; _policyIdentifiers[_setupSource] = _policyId.Text; _policyGrid.SelectedObject = editor;
            _setupStatus.Text = $"Accepted revision {result.Value.Revision}; applied: {result.Value.Enforced}";
        });
        save.Click += async (_, _) => await RunSetupAsync(async token =>
        {
            if (!_policyEditors.TryGetValue(_setupSource, out var editor)) throw new InvalidOperationException("Load the current policy before saving a revision-matched change.");
            var result = await _viewModel.Service.SavePolicyAsync(editor.Build(), token);
            if (!result.IsSuccess) throw new InvalidOperationException(result.Error?.Message);
            _setupStatus.Text = "Policy update accepted; reload to confirm applied revision.";
        });
        drill.Click += async (_, _) => await RunSetupAsync(async token =>
        {
            var protectionSet = clbDatabases.SelectedItem?.ToString() ?? throw new InvalidOperationException("Select a protection set.");
            var revision = _viewModel.State.ProtectionSets.First(item => item.Id == protectionSet).PolicyRevision;
            var result = await _viewModel.Service.RequestRestoreDrillAsync(_setupSource, protectionSet, _restorePoint.Text.Trim(), _targetProfile.Text.Trim(), _validationProfile.Text.Trim(), revision, token);
            if (!result.IsSuccess) throw new InvalidOperationException(result.Error?.Message);
            _setupStatus.Text = $"Restore accepted: {result.Value?.OperationId}; awaiting validation.";
            await RefreshLogsAsync();
        });
    }
    /// <summary>Shows source readiness and restores that source's editor without mixing configurations.</summary>
    async Task RefreshSetupHealthAsync(BackupSource source)
    {
        _policyIdentifiers[_setupSource] = _policyId.Text;
        _sourceInputs[_setupSource] = (_restorePoint.Text, _targetProfile.Text, _validationProfile.Text, _backupMode.SelectedItem);
        var inputs = _sourceInputs.GetValueOrDefault(source);
        _restorePoint.Text = inputs.RestorePoint ?? ""; _targetProfile.Text = inputs.Target ?? ""; _validationProfile.Text = inputs.Validation ?? "";
        _backupMode.SelectedItem = inputs.Mode ?? DatabaseBackupMode.Full;
        _setupSource = source; _policyId.Text = _policyIdentifiers.GetValueOrDefault(source, "");
        _policyGrid.SelectedObject = _policyEditors.GetValueOrDefault(source);
        await RunSetupAsync(async token =>
        {
            var metadata = await _viewModel.Service.LoadSetupAsync(source, token);
            var host = pnlBackupDatabases.Panel1.Controls.Find("BackupHostSettings", true).OfType<TextBox>().Single();
            host.Text = metadata.IsSuccess && metadata.Value is { Available: true } setup
                ? string.Join(Environment.NewLine, setup.BackupHostSettings.Select(item => $"{item.Key}: {item.Value}"))
                : "Host setup metadata unavailable. Configure the host output mount; host settings are read-only here. Schedules: System Admin > Scheduled Tasks.";
            var result = await _viewModel.Service.LoadHealthAsync(source, token);
            if (!result.IsSuccess || result.Value is null) { _setupStatus.Text = result.Error?.Message ?? "Readiness unavailable"; return; }
            _setupStatus.Text = result.Value.Length == 0 ? "No host readiness observation" : string.Join("; ", result.Value.Select(item => $"{item.HostId}: {item.Capability}, ready={item.Ready}"));
        });
    }
    /// <summary>Runs a bounded setup operation and displays failures without async-void propagation.</summary>
    async Task RunSetupAsync(Func<CancellationToken, Task> action)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(_logsCancellation.Token); deadline.CancelAfter(TimeSpan.FromSeconds(15));
        try { await action(deadline.Token); }
        catch (OperationCanceledException) { _setupStatus.Text = "Setup operation cancelled or timed out."; }
        catch (Exception exception) { _setupStatus.Text = exception.Message; }
    }
    /// <summary>Editable business policy values; host deployment secrets are excluded.</summary>
    sealed class PolicyEditor
    {
        readonly DatabaseBackupPolicyUiModel _policy;
        public PolicyEditor(DatabaseBackupPolicyUiModel policy) { _policy = policy; Daily = policy.Daily; Weekly = policy.Weekly; Monthly = policy.Monthly; RpoMinutes = policy.Rpo.TotalMinutes; RtoMinutes = policy.Rto.TotalMinutes; MaximumVerificationHours = policy.MaximumVerificationAge.TotalHours; }
        [ReadOnly(true)] public string Policy => _policy.Id;
        [ReadOnly(true)] public long AcceptedRevision => _policy.Revision;
        [ReadOnly(true)] public bool Applied => _policy.Enforced;
        [ReadOnly(true)] public string EnabledSources => string.Join(", ", _policy.EnabledSources);
        [ReadOnly(true)] public string ProtectedSets => string.Join(", ", _policy.ProtectionSets);
        [ReadOnly(true)] public string Verification => string.Join(", ", _policy.VerificationLevels);
        public int Daily { get; set; }
        public int Weekly { get; set; }
        public int Monthly { get; set; }
        public double RpoMinutes { get; set; }
        public double RtoMinutes { get; set; }
        public double MaximumVerificationHours { get; set; }
        /// <summary>Builds a validated proposal while preserving source and protected-set membership.</summary>
        public DatabaseBackupPolicyUiModel Build()
        {
            if (Daily < 0 || Weekly < 0 || Monthly < 0 || !double.IsFinite(RpoMinutes) || !double.IsFinite(RtoMinutes) || !double.IsFinite(MaximumVerificationHours) || RpoMinutes <= 0 || RtoMinutes <= 0 || MaximumVerificationHours <= 0) throw new InvalidOperationException("Retention must be non-negative and recovery/verification durations positive.");
            return _policy with { Daily = Daily, Weekly = Weekly, Monthly = Monthly, Rpo = TimeSpan.FromMinutes(RpoMinutes), Rto = TimeSpan.FromMinutes(RtoMinutes), MaximumVerificationAge = TimeSpan.FromHours(MaximumVerificationHours) };
        }
    }
}
