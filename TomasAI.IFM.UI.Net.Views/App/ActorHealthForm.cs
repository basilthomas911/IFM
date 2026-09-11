using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Models.Operations;
using TomasAI.IFM.UI.Net.Services.Operations;

namespace TomasAI.IFM.UI.Net.Views.App;

/// <summary>Read-only actor-system health explorer. All values come from supervisor snapshots.</summary>
public sealed class ActorHealthForm : DarkTradingForm, IForm<ActorHealthForm>
{
    readonly IActorHealthQueryService service;
    readonly CancellationTokenSource closing = new();
    readonly System.Windows.Forms.Timer timer = new() { Interval = 15000 };
    readonly DateTimePicker from = Picker("actorHealthFrom");
    readonly DateTimePicker to = Picker("actorHealthTo");
    readonly Button refresh = new() { Name = "refreshActorHealth", Text = "Refresh", AutoSize = true };
    readonly Label summary = new() { Name = "actorHealthSummary", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
    readonly TreeView tree = new() { Name = "actorHealthTree", Dock = DockStyle.Fill, HideSelection = false };
    readonly Label selection = new() { Name = "actorHealthSelection", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
    readonly DataGridView details = Grid();
    ActorHealthSnapshot? snapshot;
    bool closeComplete;

    public ActorHealthForm(IActorHealthQueryService service)
    {
        this.service = service ?? throw new ArgumentNullException(nameof(service));
        Name = "ActorHealthForm";
        Text = "Actor Health (read-only)";
        ClientSize = new Size(1280, 760);
        MinimumSize = new Size(960, 600);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Color.FromArgb(25, 25, 25);
        ForeColor = Color.Gainsboro;
        Font = new Font("Segoe UI", 10);
        AutoScaleMode = AutoScaleMode.Dpi;

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1, Padding = new Padding(12) };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var filters = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        filters.Controls.AddRange([
            Caption("From (UTC)"), from,
            Caption("To (UTC)"), to,
            refresh,
            new Label { AutoSize = true, Text = "Read-only; refreshes every 15 seconds while open.", Padding = new Padding(12, 7, 0, 0) }
        ]);
        var split = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 390 };
        tree.BackColor = Color.FromArgb(30, 30, 30);
        tree.ForeColor = Color.Gainsboro;
        tree.LineColor = Color.DimGray;
        split.Panel1.Controls.Add(tree);
        var right = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        right.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        right.Controls.Add(selection, 0, 0);
        right.Controls.Add(details, 0, 1);
        split.Panel2.Controls.Add(right);
        root.Controls.Add(filters, 0, 0);
        root.Controls.Add(summary, 0, 1);
        root.Controls.Add(split, 0, 2);
        Controls.Add(root);

        var now = DateTime.UtcNow;
        from.Value = now.AddHours(-1);
        to.Value = now;
        refresh.BackColor = Color.FromArgb(60, 60, 60);
        refresh.ForeColor = Color.White;
        refresh.Click += Refresh_Click;
        tree.AfterSelect += Tree_AfterSelect;
        timer.Tick += Refresh_Click;
        Shown += Form_Shown;
        FormClosing += Form_Closing;
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, closing.Token);
        refresh.Enabled = false;
        try
        {
            var result = await service.GetAsync(from.Value.ToUniversalTime(), to.Value.ToUniversalTime(), linked.Token);
            snapshot = result.IsSuccess ? result.Value : null;
            if (snapshot is null)
            {
                summary.Text = $"Actor health unavailable: {result.Error?.Message ?? "No snapshot returned."}";
                summary.ForeColor = Color.Salmon;
                tree.Nodes.Clear();
                details.DataSource = null;
                return;
            }
            RenderSnapshot(snapshot);
        }
        catch (OperationCanceledException) when (linked.IsCancellationRequested) { }
        finally
        {
            if (!IsDisposed && !closing.IsCancellationRequested)
                refresh.Enabled = true;
        }
    }

    void RenderSnapshot(ActorHealthSnapshot value)
    {
        var status = StatusName(value.OverallStatus);
        var healthyWorkers = value.Workers.Count(worker => !worker.IsFaulted);
        summary.Text = $"{status} | Actors {value.RunningActorCount}/{value.ActorCount} running | "
            + $"Workers {healthyWorkers}/{value.Workers.Count} available | "
            + $"Mailboxes processing {value.ProcessingMailboxCount} | Messages waiting {value.QueuedMessageCount} | "
            + $"Observed {value.ObservedUtc.ToUniversalTime():yyyy-MM-dd HH:mm:ss} UTC";
        summary.ForeColor = StatusColor(value.OverallStatus);
        var selectedKey = tree.SelectedNode?.Name;
        tree.BeginUpdate();
        tree.Nodes.Clear();
        foreach (var domainGroup in value.Actors.GroupBy(actor => actor.Domain).OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            var domainNode = new TreeNode(domainGroup.Key) { Name = $"domain:{domainGroup.Key}" };
            foreach (var actor in domainGroup.OrderBy(actor => actor.ActorId.Name, StringComparer.Ordinal))
            {
                var actorNode = new TreeNode($"{StatusGlyph(actor.Status)} {actor.ActorId.Name} [{actor.ActorId.ActorType}]")
                {
                    Name = $"actor:{actor.ActorId.ActorType}:{actor.ActorId.Name}", Tag = actor,
                    ForeColor = StatusColor(actor.Status)
                };
                foreach (var mailbox in actor.Mailboxes.OrderBy(mailbox => mailbox.ThreadId.EntityId, StringComparer.Ordinal))
                {
                    actorNode.Nodes.Add(new TreeNode($"{StatusGlyph(MailboxStatus(mailbox))} {mailbox.ThreadId.EntityId} ({mailbox.QueueDepth} waiting)")
                    {
                        Name = $"mailbox:{actor.ActorId.ActorType}:{actor.ActorId.Name}:{mailbox.ThreadId.EntityId}",
                        Tag = mailbox, ForeColor = StatusColor(MailboxStatus(mailbox))
                    });
                }
                domainNode.Nodes.Add(actorNode);
            }
            tree.Nodes.Add(domainNode);
        }
        var failuresNode = new TreeNode($"Failure history ({value.Failures.Count})") { Name = "failures" };
        foreach (var failure in value.Failures)
        {
            failuresNode.Nodes.Add(new TreeNode(
                $"{StatusGlyph(2)} {failure.FailedUtc.ToUniversalTime():HH:mm:ss} {failure.ActorId.Name}.{failure.Verb}")
            {
                Name = $"failure:{failure.FailureId:D}", Tag = failure, ForeColor = Color.Salmon
            });
        }
        tree.Nodes.Add(failuresNode);
        var projectorsNode = new TreeNode($"Event projectors ({value.Projectors.Count})") { Name = "projectors" };
        foreach (var projector in value.Projectors)
        {
            projectorsNode.Nodes.Add(new TreeNode(
                $"{StatusGlyph(projector.IsReady ? 0 : 2)} {projector.ActorName} / {projector.ProjectorName}")
            {
                Name = $"projector:{projector.ActorName}:{projector.ProjectorName}", Tag = projector,
                ForeColor = projector.IsReady ? Color.LightGreen : Color.Salmon
            });
        }
        tree.Nodes.Add(projectorsNode);

        var workersNode = new TreeNode($"Shared workers ({value.Workers.Count})") { Name = "workers" };
        foreach (var worker in value.Workers.OrderBy(worker => worker.WorkerId))
        {
            var workerStatus = worker.IsFaulted ? 2 : worker.State == 3 ? 1 : 0;
            workersNode.Nodes.Add(new TreeNode(
                $"{StatusGlyph(workerStatus)} Worker {worker.WorkerId} [{WorkerStateName(worker.State)}]")
            {
                Name = $"worker:{worker.WorkerId}", Tag = worker, ForeColor = StatusColor(workerStatus)
            });
        }
        tree.Nodes.Add(workersNode);
        tree.EndUpdate();
        var restored = Find(tree.Nodes, selectedKey);
        if (restored is not null)
            tree.SelectedNode = restored;
        else if (tree.Nodes.Count > 0)
            tree.Nodes[0].Expand();
    }

    void Tree_AfterSelect(object? sender, TreeViewEventArgs args)
    {
        switch (args.Node.Tag)
        {
            case ActorHealthActorSnapshot actor:
                selection.Text = $"Actor: {actor.ActorId.Name} | {StatusName(actor.Status)} | lifecycle {LifecycleName(actor.LifecycleState)} | generation {actor.Generation} | {actor.Implementation}";
                details.DataSource = actor.Mailboxes.Select(MailboxRow).ToList();
                break;
            case ActorHealthMailboxSnapshot mailbox:
                selection.Text = $"Mailbox: {mailbox.ThreadId.EntityId} | {StatusName(MailboxStatus(mailbox))}";
                details.DataSource = new[] { MailboxRow(mailbox) };
                break;
            case ActorHealthFailureRecord failure:
                selection.Text = $"Failure: {failure.ActorId.Name}.{failure.Verb} | {failure.FailedUtc.ToUniversalTime():yyyy-MM-dd HH:mm:ss} UTC";
                details.DataSource = new[]
                {
                    new
                    {
                        failure.FailureId,
                        failure.PrimaryFailureId,
                        Actor = failure.ActorId.Name,
                        Entity = failure.ThreadId.EntityId,
                        failure.Verb,
                        failure.Stage,
                        failure.Severity,
                        failure.Outcome,
                        failure.DeliveryOutcome,
                        failure.HResult,
                        failure.TraceId,
                        failure.SpanId,
                        failure.ExceptionType,
                        failure.Error,
                        failure.ExceptionDetail
                    }
                };
                break;
            case ActorHealthProjectorSnapshot projector:
                selection.Text = $"Projector: {projector.ActorName} / {projector.ProjectorName} | {(projector.IsReady ? "Ready" : "Not ready")}";
                details.DataSource = new[]
                {
                    new
                    {
                        projector.ActorName,
                        projector.ProjectorName,
                        projector.DurableProcessQueue,
                        projector.DurableReplayQueue,
                        projector.IsReady,
                        projector.RecoveryEventsDiscovered,
                        projector.RecoveryEventsQueued,
                        UpdatedUtc = Utc(projector.UpdatedUtc),
                        projector.FailureReason
                    }
                };
                break;
            case ActorHealthWorkerSnapshot worker:
                selection.Text = $"Shared worker {worker.WorkerId} | {WorkerStateName(worker.State)}";
                details.DataSource = new[]
                {
                    new
                    {
                        worker.WorkerId,
                        State = WorkerStateName(worker.State),
                        worker.IsStarted,
                        worker.IsRunning,
                        worker.IsFaulted,
                        CurrentActor = worker.CurrentMailbox?.Name ?? string.Empty,
                        CurrentEntity = worker.CurrentMailbox?.EntityId ?? string.Empty,
                        worker.ExceptionType,
                        worker.FailureReason
                    }
                };
                break;
            default:
                selection.Text = args.Node.Text;
                details.DataSource = null;
                break;
        }
        FormatGrid();
    }

    static object MailboxRow(ActorHealthMailboxSnapshot mailbox) => new
    {
        Entity = mailbox.ThreadId.EntityId,
        Status = StatusName(MailboxStatus(mailbox)),
        Waiting = mailbox.QueueDepth,
        Admission = mailbox.IsAdmissionOpen ? "Open" : "Paused",
        Lifecycle = MailboxLifecycleName(mailbox.LifecycleState),
        mailbox.Generation,
        mailbox.CurrentVerb,
        mailbox.Accepted,
        mailbox.Dequeued,
        mailbox.Succeeded,
        mailbox.HandledFailures,
        mailbox.EscapedFailures,
        mailbox.Cancelled,
        mailbox.Rejected,
        LastStartedUtc = Utc(mailbox.LastStartedUtc),
        LastCompletedUtc = Utc(mailbox.LastCompletedUtc),
        LastFailedUtc = Utc(mailbox.LastFailedUtc),
        mailbox.LastExceptionType,
        mailbox.LastError
    };

    void FormatGrid()
    {
        foreach (DataGridViewColumn column in details.Columns)
            column.SortMode = DataGridViewColumnSortMode.NotSortable;
        foreach (DataGridViewRow row in details.Rows)
            if (details.Columns.Contains("Status"))
                row.Cells["Status"].Style.ForeColor = row.Cells["Status"].Value?.ToString() switch
                {
                    "Green" => Color.LightGreen, "Yellow" => Color.Khaki, _ => Color.Salmon
                };
    }

    async void Form_Shown(object? sender, EventArgs args)
    {
        await RefreshAsync();
        if (!closing.IsCancellationRequested) timer.Start();
    }

    async void Refresh_Click(object? sender, EventArgs args)
    {
        to.Value = DateTime.UtcNow;
        await RefreshAsync();
    }

    async void Form_Closing(object? sender, FormClosingEventArgs args)
    {
        if (closeComplete) return;
        args.Cancel = true;
        timer.Stop();
        closing.Cancel();
        closeComplete = true;
        await Task.Yield();
        Close();
    }

    static DateTimePicker Picker(string name) => new()
    {
        Name = name, Width = 190, Format = DateTimePickerFormat.Custom,
        CustomFormat = "yyyy-MM-dd HH:mm:ss", ShowUpDown = true
    };
    static Label Caption(string text) => new() { Text = text, AutoSize = true, Padding = new Padding(8, 7, 2, 0) };
    static DataGridView Grid() => new()
    {
        Name = "actorHealthDetails", Dock = DockStyle.Fill, ReadOnly = true,
        AllowUserToAddRows = false, AllowUserToDeleteRows = false, RowHeadersVisible = false,
        AutoGenerateColumns = true, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells,
        BackgroundColor = Color.FromArgb(25, 25, 25), GridColor = Color.DimGray,
        EnableHeadersVisualStyles = false,
        ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.FromArgb(50, 50, 50), ForeColor = Color.White },
        DefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.FromArgb(30, 30, 30), ForeColor = Color.Gainsboro },
        SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false
    };
    static int MailboxStatus(ActorHealthMailboxSnapshot value) => value.IsProcessing || value.QueueDepth > 0 ? 1
        : value.LastFailedUtc is not null && (value.LastCompletedUtc is null || value.LastFailedUtc >= value.LastCompletedUtc) ? 2 : 0;
    static string StatusName(int status) => status switch { 0 => "Green", 1 => "Yellow", _ => "Red" };
    static string LifecycleName(int state) => state switch
    {
        0 => "Registered", 1 => "Starting", 2 => "Running", 3 => "Draining",
        4 => "Stopped", 5 => "Restarting", 6 => "Faulted", 7 => "Timed out",
        8 => "Quarantined", _ => $"Unknown ({state})"
    };

    static string MailboxLifecycleName(int state) => state switch
    {
        0 => "Running",
        1 => "Draining",
        2 => "Paused",
        3 => "Quarantined",
        4 => "Retired",
        _ => $"Unknown ({state})"
    };
    static string WorkerStateName(int state) => state switch
    {
        0 => "Unknown", 1 => "Ready", 2 => "Started", 3 => "Processing",
        4 => "Waiting", 5 => "Stopped", 6 => "Faulted", 7 => "Timed out",
        _ => $"Unknown ({state})"
    };
    static string StatusGlyph(int status) => "\u25CF";
    static Color StatusColor(int status) => status switch { 0 => Color.LightGreen, 1 => Color.Khaki, _ => Color.Salmon };
    static string Utc(DateTime? value) => value?.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss 'UTC'") ?? "Never";
    static TreeNode? Find(TreeNodeCollection nodes, string? key)
    {
        if (key is null) return null;
        foreach (TreeNode node in nodes)
        {
            if (node.Name == key) return node;
            var child = Find(node.Nodes, key);
            if (child is not null) return child;
        }
        return null;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            timer.Dispose();
            closing.Dispose();
            if (service is IDisposable disposable) disposable.Dispose();
        }
        base.Dispose(disposing);
    }
}
