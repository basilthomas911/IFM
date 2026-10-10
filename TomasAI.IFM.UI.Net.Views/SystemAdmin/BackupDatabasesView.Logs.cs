using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.UI.Net.Models.SystemAdmin;
namespace TomasAI.IFM.UI.Net.Views.SystemAdmin;
/// <summary>Provides dated backup runs with final database leaves and retained output.</summary>
public partial class BackupDatabasesView
{
    readonly TabControl _tabs = new() { Name = "BackupTabs", Dock = DockStyle.Fill };
    readonly TabControl _sources = new() { Name = "BackupSourceSetupTabs", Dock = DockStyle.Fill };
    readonly TreeView _tree = new() { Name = "BackupLogTree", Dock = DockStyle.Fill, BackColor = Color.Black, ForeColor = Color.White, HideSelection = false };
    readonly TextBox _output = new() { Name = "BackupStandardOutput", Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, WordWrap = false, ScrollBars = ScrollBars.Both, BackColor = Color.Black, ForeColor = Color.White };
    readonly Button _moreOutputButton = new() { Name = "BackupMoreOutput", Text = "Refresh logs / output", AutoSize = true };
    readonly Button _cancelBackup = new() { Name = "BackupCancel", Text = "Cancel selected backup", AutoSize = true, Enabled = false };
    readonly Label _logStatus = new() { AutoSize = true, ForeColor = Color.White };
    readonly ImageList _statusImages = new() { ImageSize = new(16, 16), ColorDepth = ColorDepth.Depth32Bit };
    readonly Dictionary<BackupSource, Dictionary<Guid, DatabaseBackupOperationUiModel>> _history = [];
    readonly Dictionary<BackupSource, string> _continuations = [];
    readonly CancellationTokenSource _logsCancellation = new();
    CancellationTokenSource _outputCancellation = new();
    readonly SemaphoreSlim _refreshLogs = new(1, 1);
    readonly SemaphoreSlim _readLog = new(1, 1);
    long _offset, _phaseRevision;
    bool _bindingTree;
    sealed record DatabaseLeaf(BackupSource Source, DatabaseEngine Engine, DatabaseBackupOperationUiModel? Operation);
    sealed record OlderRuns(BackupSource Source);

    /// <summary>Builds Logs and source-specific Setup tabs around existing backup controls.</summary>
    void CreateBackupTabs()
    {
        Controls.Remove(pnlBackupDatabases);
        var originals = new List<Bitmap>();
        foreach (var (name, color) in new[] { ("Scheduled", Color.DodgerBlue), ("Unscheduled", Color.Gray), ("Running", Color.Yellow), ("Succeeded", Color.LimeGreen), ("Failed", Color.Red) })
        {
            var bitmap = new Bitmap(16, 16); originals.Add(bitmap);
            using var graphics = Graphics.FromImage(bitmap);
            using var brush = new SolidBrush(color);
            graphics.FillEllipse(brush, 2, 2, 12, 12); _statusImages.Images.Add(name, bitmap);
        }
        // ImageList creates its native handle lazily, so originals must remain alive until then.
        _ = _statusImages.Handle;
        foreach (var original in originals) original.Dispose();
        components ??= new System.ComponentModel.Container();
        components.Add(_statusImages);
        _tree.ImageList = _statusImages;
        var logs = new TabPage("Logs") { BackColor = Color.Black };
        var setup = new TabPage("Setup") { BackColor = Color.Black };
        var split = new SplitContainer { Dock = DockStyle.Fill, BackColor = Color.Black };
        split.SizeChanged += (_, _) => { if (split.Width > 300) split.SplitterDistance = split.Width / 2; };
        split.Panel1.Controls.Add(_tree);
        var right = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1, BackColor = Color.Black };
        right.ColumnStyles.Add(new(SizeType.Percent, 100));
        right.RowStyles.Add(new(SizeType.Percent, 100)); right.RowStyles.Add(new(SizeType.Absolute, 42)); right.RowStyles.Add(new(SizeType.Absolute, 40));
        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Fill }; toolbar.Controls.Add(_moreOutputButton); toolbar.Controls.Add(_cancelBackup);
        _logStatus.Dock = DockStyle.Fill; _logStatus.AutoSize = false;
        right.Controls.Add(_output, 0, 0); right.Controls.Add(toolbar, 0, 1); right.Controls.Add(_logStatus, 0, 2); split.Panel2.Controls.Add(right);
        logs.Controls.Add(split); setup.Controls.Add(_sources);
        _sources.TabPages.Add(new TabPage("AWS Backup") { Tag = BackupSource.AwsCloud, BackColor = Color.Black });
        _sources.TabPages.Add(new TabPage("Local Workstation") { Tag = BackupSource.LocalWorkstation, BackColor = Color.Black });
        _sources.SelectedIndex = 1; _sources.SelectedTab!.Controls.Add(pnlBackupDatabases);
        _sources.SelectedIndexChanged += async (_, _) =>
        {
            var source = (BackupSource)_sources.SelectedTab!.Tag!;
            _sources.SelectedTab.Controls.Add(pnlBackupDatabases);
            radFullBackup.Checked = source == BackupSource.AwsCloud; radDiffBackup.Checked = source == BackupSource.LocalWorkstation;
            _viewModel.SelectSource(source); await _viewModel.RefreshAsync(); await RefreshSetupHealthAsync(source);
        };
        radFullBackup.Visible = false; radDiffBackup.Visible = false;
        _tabs.TabPages.Add(logs); _tabs.TabPages.Add(setup); Controls.Add(_tabs);
        ConfigureSetupEditor();
        _tree.AfterSelect += async (_, args) =>
        {
            if (_bindingTree) return;
            _cancelBackup.Enabled = args.Node?.Tag is DatabaseLeaf { Operation: { Kind: DatabaseRecoveryOperationKind.Backup, Outcome: DatabaseRecoveryOutcome.None } };
            if (args.Node?.Tag is OlderRuns more) { await LoadOlderRunsAsync(more.Source); return; }
            _outputCancellation.Cancel(); _outputCancellation.Dispose(); _outputCancellation = new(); _offset = 0; _phaseRevision = 0; _output.Clear();
            if (args.Node?.Tag is DatabaseLeaf leaf && leaf.Operation is not null) await ReadLogAsync(_outputCancellation.Token);
            else _logStatus.Text = "Select a database leaf. Gray means no operation for this database in the selected run.";
        };
        _moreOutputButton.Click += async (_, _) => await RefreshLogsAsync();
        _cancelBackup.Click += async (_, _) =>
        {
            if (_tree.SelectedNode?.Tag is not DatabaseLeaf { Operation: { Kind: DatabaseRecoveryOperationKind.Backup } operation }) return;
            _cancelBackup.Enabled = false;
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(_logsCancellation.Token); deadline.CancelAfter(TimeSpan.FromSeconds(15));
            try
            {
                var result = await _viewModel.Service.CancelBackupAsync(operation, deadline.Token);
                _logStatus.Text = result.IsSuccess ? "Cancellation accepted; awaiting terminal status." : result.Error?.Message;
                await RefreshLogsAsync();
            }
            catch (OperationCanceledException) { _logStatus.Text = "Cancellation request timed out; refresh status before retrying."; }
            catch (Exception exception) { _logStatus.Text = exception.Message; }
        };
        BuildLogTree();
    }
    /// <summary>Refreshes bounded history without overlapping requests or discarding older pages.</summary>
    async Task RefreshLogsAsync()
    {
        if (!_refreshLogs.Wait(0)) return;
        try
        {
            foreach (var source in new[] { BackupSource.AwsCloud, BackupSource.LocalWorkstation })
            {
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(_logsCancellation.Token); deadline.CancelAfter(TimeSpan.FromSeconds(15));
                var result = await _viewModel.Service.LoadHistoryAsync(source, "", deadline.Token);
                if (!result.IsSuccess || result.Value is null) { _logStatus.Text = result.Error?.Message; continue; }
                if (!_history.TryGetValue(source, out var operations)) _history[source] = operations = [];
                foreach (var item in result.Value.Operations) operations[item.OperationId] = item;
                if (!_continuations.ContainsKey(source)) _continuations[source] = result.Value.Continuation;
            }
            BuildLogTree();
            if (!_history.Values.Any(operations => operations.Count > 0))
                _logStatus.Text = "No recorded backup runs. Open Setup > Local Workstation to request a backup. Disposable test runs are separate from this history.";
            else if (_tree.SelectedNode is null)
                _logStatus.Text = "Select a PostgreSQL or ScyllaDB leaf to see phases and output. Manual backup and restore: Setup > Local Workstation.";
            if (_tree.SelectedNode?.Tag is DatabaseLeaf { Operation: not null }) await ReadLogAsync(_outputCancellation.Token);
        }
        catch (OperationCanceledException) { }
        catch (Exception exception) { _logStatus.Text = exception.Message; }
        finally { _refreshLogs.Release(); }
    }
    /// <summary>Loads the next history page from the source's continuation.</summary>
    async Task LoadOlderRunsAsync(BackupSource source)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(_logsCancellation.Token); deadline.CancelAfter(TimeSpan.FromSeconds(15));
        try
        {
            var result = await _viewModel.Service.LoadHistoryAsync(source, _continuations.GetValueOrDefault(source, ""), deadline.Token);
            if (!result.IsSuccess || result.Value is null) { _logStatus.Text = result.Error?.Message; return; }
            if (!_history.TryGetValue(source, out var operations)) _history[source] = operations = [];
            foreach (var item in result.Value.Operations) operations[item.OperationId] = item;
            _continuations[source] = result.Value.Continuation; BuildLogTree();
        }
        catch (Exception exception) { _logStatus.Text = exception.Message; }
    }
    /// <summary>Builds exactly two source roots and two final engine leaves under each run.</summary>
    void BuildLogTree()
    {
        var selected = (_tree.SelectedNode?.Tag as DatabaseLeaf)?.Operation?.OperationId;
        _bindingTree = true; _tree.BeginUpdate();
        try
        {
            _tree.Nodes.Clear();
            foreach (var source in new[] { BackupSource.AwsCloud, BackupSource.LocalWorkstation })
            {
                var root = new TreeNode(source == BackupSource.AwsCloud ? "AWS Backup" : "Local Workstation Backup") { ImageKey = "Unscheduled", SelectedImageKey = "Unscheduled" }; _tree.Nodes.Add(root);
                if (!_history.TryGetValue(source, out var operations)) continue;
                var latest = operations.Values.OrderByDescending(item => item.CreatedUtc).FirstOrDefault();
                if (latest is not null) root.ImageKey = root.SelectedImageKey = Status(latest);
                foreach (var run in operations.Values.OrderByDescending(item => item.CreatedUtc).GroupBy(item => item.BackupSetId ?? item.OperationId))
                {
                    var first = run.First(); var time = first.CreatedUtc.ToLocalTime();
                    var year = Group(root, time.Year.ToString()); var month = Group(year, time.ToString("MM MMMM")); var day = Group(month, time.ToString("dd dddd"));
                    var node = new TreeNode($"{first.ProtectionSet} | {first.Kind} {first.RequestedMode}/{first.ResolvedMode} | {run.Key:N} | ran {time:yyyy-MM-dd HH:mm:ss zzz} | scheduled: unavailable") { ImageKey = Status(first), SelectedImageKey = Status(first) }; day.Nodes.Add(node);
                    foreach (var engine in new[] { DatabaseEngine.ScyllaDb, DatabaseEngine.PostgreSql })
                    {
                        var operation = run.FirstOrDefault(item => item.Engine == engine);
                        var label = engine == DatabaseEngine.ScyllaDb ? "ScyllaDB" : "PostgreSQL";
                        var leaf = new TreeNode(operation is null ? $"{label} | no identified operation" : $"{label} | {operation.Phase} | {operation.ProgressPercent}% milestone | {operation.Outcome}") { Tag = new DatabaseLeaf(source, engine, operation), ImageKey = operation is null ? "Unscheduled" : Status(operation), SelectedImageKey = operation is null ? "Unscheduled" : Status(operation) };
                        node.Nodes.Add(leaf); if (operation?.OperationId == selected) _tree.SelectedNode = leaf;
                    }
                    node.Expand(); day.Expand(); month.Expand(); year.Expand();
                }
                if (!string.IsNullOrEmpty(_continuations.GetValueOrDefault(source))) root.Nodes.Add(new TreeNode("Load older runs...") { Tag = new OlderRuns(source) });
                root.Expand();
            }
        }
        finally { _tree.EndUpdate(); _bindingTree = false; }
    }
    static TreeNode Group(TreeNode parent, string text)
    {
        var node = parent.Nodes.Cast<TreeNode>().FirstOrDefault(item => item.Text == text);
        if (node is null) { node = new TreeNode(text); parent.Nodes.Add(node); } return node;
    }
    static string Status(DatabaseBackupOperationUiModel operation) => operation.Outcome switch
    {
        DatabaseRecoveryOutcome.Succeeded => "Succeeded",
        DatabaseRecoveryOutcome.Failed or DatabaseRecoveryOutcome.Cancelled or DatabaseRecoveryOutcome.Rejected or DatabaseRecoveryOutcome.Degraded => "Failed",
        _ when operation.Phase is DatabaseRecoveryPhase.Requested or DatabaseRecoveryPhase.Authorized => "Scheduled",
        _ => "Running"
    };
    /// <summary>Reads one bounded output/phase page and fences results by selection cancellation.</summary>
    async Task ReadLogAsync(CancellationToken cancellationToken)
    {
        if (!_readLog.Wait(0)) return;
        if (_tree.SelectedNode?.Tag is not DatabaseLeaf { Operation: { } operation } leaf) { _readLog.Release(); return; }
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _logsCancellation.Token); deadline.CancelAfter(TimeSpan.FromSeconds(15));
        try
        {
            var result = await _viewModel.Service.LoadLogAsync(leaf.Source, operation.OperationId, _offset, _phaseRevision, deadline.Token);
            if (cancellationToken.IsCancellationRequested || !result.IsSuccess || result.Value is null) { if (!cancellationToken.IsCancellationRequested) _logStatus.Text = result.Error?.Message; return; }
            var page = result.Value;
            foreach (var phase in page.Phases) { _output.AppendText($"{phase.ObservedUtc:O} {phase.Phase} | {phase.ProgressPercent}% milestone | {phase.Outcome}{Environment.NewLine}"); _phaseRevision = Math.Max(_phaseRevision, phase.Revision); }
            _output.AppendText(page.Output); _offset = page.NextOffset;
            _logStatus.Text = page.Available ? $"Output bytes read: {_offset}" : "Retained host output unavailable; persisted phases shown.";
        }
        catch (OperationCanceledException) { }
        catch (Exception exception) { _logStatus.Text = exception.Message; }
        finally { _readLog.Release(); }
    }
}
