using System.Collections;
using System.ComponentModel;
using System.Globalization;
using System.Diagnostics;
using TomasAI.IFM.UI.Net.Views.Presentation;
using TomasAI.IFM.UI.Net.ViewModels.Operations;

namespace TomasAI.IFM.UI.Net.Views.Strategy;

/// <summary>Browses immutable workflow, ITI signal and pipeline objects without allowing edits.</summary>
public sealed class StrategyWorkflowDetailsAccordion : DarkTradingView
{
    readonly TreeView _tree = new WorkflowDetailsTreeView() { Name = "WorkflowDetailsTree", Dock = DockStyle.Fill, HideSelection = false, BackColor = Color.Black, ForeColor = Color.White, AccessibleName = "Workflow objects" };
    readonly PropertyGrid _properties = new() { Name = "WorkflowDetailsProperties", Dock = DockStyle.Fill, ToolbarVisible = false, HelpVisible = false, PropertySort = PropertySort.NoSort, AccessibleName = "Selected workflow object properties" };
    readonly Label _message = new() { Dock = DockStyle.Top, AutoSize = true, ForeColor = Color.White, Padding = new(4) };
    StrategyWorkflowDetails? _details;
    CancellationTokenSource? _preparation;
    long _generation;
    volatile PreparedView? _pendingView;
    readonly object _cacheGate = new();
    readonly System.Windows.Forms.Timer _responsiveness = new() { Interval = 250 };
    long _lastUiTick;
    bool _binding;
    object? _inspectedValue;

    /// <summary>Creates the resizable object tree and read-only property inspector.</summary>
    public StrategyWorkflowDetailsAccordion()
    {
        Dock = DockStyle.Fill;
        var split = new SplitContainer { Name = "WorkflowDetailsSplit", Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, Size = new(700, 300), SplitterDistance = 140 };
        split.Panel1.Controls.Add(_tree);
        split.Panel2.Controls.Add(_properties);
        Controls.Add(split);
        Controls.Add(_message);
        _responsiveness.Tick += (_, _) =>
        {
            var now = Stopwatch.GetTimestamp();
            var elapsed = Stopwatch.GetElapsedTime(_lastUiTick, now).TotalMilliseconds;
            _lastUiTick = now;
            if (elapsed > 1000) ReportDelay("UiMessageLoop", _tree.SelectedNode?.FullPath ?? "", elapsed);
        };
        VisibleChanged += (_, _) =>
        {
            _lastUiTick = Stopwatch.GetTimestamp();
            _responsiveness.Enabled = Visible;
        };
        HandleCreated += (_, _) => ApplyPendingView();
        _tree.BeforeExpand += (_, e) => Populate(e.Node!);
        _tree.AfterSelect += (_, _) => RefreshInspector();
        _properties.HandleCreated += (_, _) => RefreshInspector();
        _properties.VisibleChanged += (_, _) => RefreshInspector();
        ShowMessage("Select a strategy workflow to inspect its pipeline results.");
    }

    /// <summary>Refreshes typed objects while retaining selection and expanded paths for the same workflow.</summary>
    public void Bind(StrategyWorkflowDetails? details)
    {
        if (details is null) { ShowMessage("Select a strategy workflow to inspect its pipeline results."); return; }
        if (_details?.WorkflowId == details.WorkflowId && _details.WorkflowRevision == details.WorkflowRevision) return;
        var sameWorkflow = _details?.WorkflowId == details.WorkflowId;
        _preparation?.Cancel();
        _preparation?.Dispose();
        _preparation = new CancellationTokenSource();
        var token = _preparation.Token;
        var generation = Interlocked.Increment(ref _generation);
        _pendingView = null;
        _details = details;
        Tag = details;
        _message.Text = "Preparing details...";
        _message.Visible = true;
        if (!sameWorkflow)
        {
            _tree.Nodes.Clear();
            _inspectedValue = null;
            if (_properties.IsHandleCreated) _properties.SelectedObject = null;
        }
        _ = PrepareAsync(details, sameWorkflow, generation, token);
    }

    async Task PrepareAsync(StrategyWorkflowDetails details, bool sameWorkflow, long generation, CancellationToken token)
    {
        try
        {
            var roots = await Task.Run(() => PrepareRoots(details, token), token).ConfigureAwait(false);
            if (token.IsCancellationRequested) return;
            lock (_cacheGate)
            {
                if (token.IsCancellationRequested || generation != Interlocked.Read(ref _generation)) return;
                _pendingView = new PreparedView(generation, sameWorkflow, roots);
            }
            if (IsHandleCreated && !IsDisposed)
                BeginInvoke((Action)ApplyPendingView);
        }
        catch (OperationCanceledException) { }
        catch (ObjectDisposedException) { }
        catch (InvalidOperationException) when (IsDisposed || !IsHandleCreated) { }
        catch (Exception exception)
        {
            if (token.IsCancellationRequested || generation != Interlocked.Read(ref _generation)) return;
            // Keep failures visible without leaving an unobserved background task.
            var error = new ObjectNode("Details unavailable", new ReadOnlyObject(exception.Message), []);
            lock (_cacheGate)
            {
                if (generation != Interlocked.Read(ref _generation)) return;
                _pendingView = new PreparedView(generation, sameWorkflow, [error], exception.Message);
            }
            try { if (IsHandleCreated && !IsDisposed) BeginInvoke((Action)ApplyPendingView); }
            catch (InvalidOperationException) { }
        }
    }

    void ApplyPendingView()
    {
        var prepared = _pendingView;
        if (prepared is null || prepared.Generation != _generation || IsDisposed) return;
        _pendingView = null;
        // Capture navigation now, including clicks made while preparation was running.
        var selected = prepared.SameWorkflow ? _tree.SelectedNode?.FullPath : null;
        var expanded = prepared.SameWorkflow ? AllNodes(_tree.Nodes).Where(n => n.IsExpanded).Select(n => n.FullPath).ToHashSet() : [];
        _message.Text = prepared.Error ?? "";
        _message.Visible = prepared.Error is not null;
        var applyStarted = Stopwatch.GetTimestamp();
        _binding = true;
        _tree.BeginUpdate();
        try
        {
            _tree.Nodes.Clear();
            foreach (var root in prepared.Roots) Add(_tree.Nodes, root);
            var pipeline = _tree.Nodes.Count > 2 ? _tree.Nodes[2] : null;
            if (pipeline is not null) Populate(pipeline);
            _tree.ShowNodeToolTips = false;
            Restore(_tree.Nodes, expanded, selected);
            if (!prepared.SameWorkflow) pipeline?.Expand();
            _tree.SelectedNode ??= _tree.Nodes[Math.Min(1, _tree.Nodes.Count - 1)];
        }
        finally { _tree.EndUpdate(); _binding = false; ReportDelay(nameof(ApplyPendingView), selected ?? "", Stopwatch.GetElapsedTime(applyStarted).TotalMilliseconds); }
        RefreshInspector();
    }

    /// <summary>Clears stale objects and displays the current loading or error message.</summary>
    public void ShowMessage(string message)
    {
        _preparation?.Cancel();
        Interlocked.Increment(ref _generation);
        _pendingView = null;
        _details = null;
        Tag = null;
        _tree.Nodes.Clear();
        _inspectedValue = null;
        if (_properties.IsHandleCreated) _properties.SelectedObject = null;
        _message.Text = message;
        _message.Visible = true;
    }

    void RefreshInspector()
    {
        // Hidden tabs may not yet own a native property-grid handle. Do not force
        // handle creation or bind while rebuilding nodes; inspect the final selection.
        if (_binding || !_properties.IsHandleCreated || !_properties.Visible) return;
        var item = _tree.SelectedNode?.Tag as ObjectNode;
        if (ReferenceEquals(item, _inspectedValue) && _properties.SelectedObject is not null) return;
        _inspectedValue = item;
        var started = Stopwatch.GetTimestamp();
        _properties.SelectedObject = item is not null ? item.Properties : null;
        ReportDelay(nameof(RefreshInspector), _tree.SelectedNode?.FullPath ?? "", Stopwatch.GetElapsedTime(started).TotalMilliseconds);
    }

    static TreeNode Add(TreeNodeCollection nodes, ObjectNode cached)
    {
        var node = new TreeNode(cached.Name) { Tag = cached, Name = cached.Name, ForeColor = cached.Color };
        nodes.Add(node);
        if (cached.Children.Length > 0) node.Nodes.Add(new TreeNode { Name = "pending" });
        return node;
    }

    static bool IsScalar(Type type) => type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal) || type == typeof(Guid) || type == typeof(DateTime) || type == typeof(DateTimeOffset) || type == typeof(TimeSpan) || type == typeof(DateOnly);

    void Populate(TreeNode node)
    {
        if (node.Nodes.Count != 1 || node.Nodes[0].Name != "pending") return;
        var started = Stopwatch.GetTimestamp();
        _tree.BeginUpdate();
        try
        {
            node.Nodes.Clear();
            if (node.Tag is ObjectNode cached)
                foreach (var child in cached.Children) Add(node.Nodes, child);
        }
        finally
        {
            _tree.EndUpdate();
            ReportDelay(nameof(Populate), node.FullPath, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        }
    }

    void ReportDelay(string method, string nodePath, double elapsedMs)
    {
        if (elapsedMs < 100) return;
        UiExceptionReporter.ReportUiDelay(method, nodePath, _details?.WorkflowId.Value.ToString() ?? "", _details?.WorkflowRevision ?? 0, elapsedMs);
    }

    static ObjectNode[] PrepareRoots(StrategyWorkflowDetails details, CancellationToken token)
    {
        var budget = 10000;
        var view = details.Workflow;
        var workflow = PrepareNode("Workflow", (object?)view ?? details, 0, [], ref budget, token, workflowRoot: true);
        var signal = PrepareNode("ITI Signal", (object?)view?.TriggerEvent ?? details.Sections.FirstOrDefault(s => s.Key == "iti"), 0, [], ref budget, token);
        object?[] stages = [view?.RegimeDiscovery, view?.MarketCondition, view?.TradeSelection, view?.OrderComposition, view?.RiskManagement];
        var sections = details.Sections.Where(s => s.Key != "iti").ToArray();
        var children = new List<ObjectNode>();
        for (var i = 0; i < sections.Length; i++)
        {
            var section = sections[i];
            var stage = PrepareNode(section.Title, i < stages.Length ? stages[i] ?? section : section, 1, [], ref budget, token);
            children.Add(stage with { Color = StateColor(section.State) });
        }
        return [workflow, signal, new ObjectNode("Workflow Pipeline", null, children.ToArray())];
    }

    static ObjectNode PrepareNode(string name, object? value, int depth, List<object> ancestors, ref int budget, CancellationToken token, bool workflowRoot = false)
    {
        token.ThrowIfCancellationRequested();
        if (--budget < 0) return new ObjectNode(name + " (display limit)", null, []);
        var properties = new ReadOnlyObject(value);
        var children = new List<ObjectNode>();
        if (value is not null && !IsScalar(value.GetType()) && depth < 16 && !ancestors.Any(a => ReferenceEquals(a, value)))
        {
            ancestors.Add(value);
            if (value is IEnumerable sequence && value is not string)
            {
                var index = 0;
                foreach (var item in sequence)
                {
                    token.ThrowIfCancellationRequested();
                    if (index == 200 || budget <= 0) { children.Add(new ObjectNode("Additional items omitted (display limit)", null, [])); break; }
                    children.Add(PrepareNode($"[{index++}]", item, depth + 1, ancestors, ref budget, token));
                }
            }
            else
            {
                foreach (PropertyDescriptor property in TypeDescriptor.GetProperties(value))
                {
                    token.ThrowIfCancellationRequested();
                    if (!property.IsBrowsable) continue;
                    if (workflowRoot && property.Name is "RegimeDiscovery" or "MarketCondition" or "TradeSelection" or "OrderComposition" or "RiskManagement" or "TriggerEvent" or "Sections") continue;
                    var child = ReadValue(property, value);
                    if (child is null || IsScalar(child.GetType()) || ancestors.Any(a => ReferenceEquals(a, child))) continue;
                    if (budget <= 0) { children.Add(new ObjectNode("Additional objects omitted (display limit)", null, [])); break; }
                    children.Add(PrepareNode(property.DisplayName, child, depth + 1, ancestors, ref budget, token));
                }
            }
            ancestors.RemoveAt(ancestors.Count - 1);
        }
        return new ObjectNode(value is null ? $"{name} (unavailable)" : name, properties, children.ToArray());
    }

    /// <summary>Cancels display preparation when the browser is disposed.</summary>
    protected override void Dispose(bool disposing)
    {
        if (disposing) { _responsiveness.Dispose(); _preparation?.Cancel(); _preparation?.Dispose(); _preparation = null; _pendingView = null; Interlocked.Increment(ref _generation); }
        base.Dispose(disposing);
    }

    static object? ReadValue(PropertyDescriptor property, object value)
    {
        try { return property.GetValue(value); }
        catch (Exception exception) { return $"Unavailable: {exception.GetBaseException().Message}"; }
    }

    static IEnumerable<TreeNode> AllNodes(TreeNodeCollection nodes)
    {
        foreach (TreeNode node in nodes)
        {
            yield return node;
            foreach (var child in AllNodes(node.Nodes)) yield return child;
        }
    }

    void Restore(TreeNodeCollection nodes, HashSet<string> expanded, string? selected)
    {
        foreach (TreeNode node in nodes)
        {
            if (expanded.Contains(node.FullPath)) { Populate(node); node.Expand(); }
            if (node.FullPath == selected) _tree.SelectedNode = node;
            Restore(node.Nodes, expanded, selected);
        }
    }

    static Color StateColor(StrategyWorkflowDetailState state) => state switch
    {
        StrategyWorkflowDetailState.Failed => Color.Salmon,
        StrategyWorkflowDetailState.Processing => Color.Gold,
        StrategyWorkflowDetailState.Completed => Color.LightGreen,
        StrategyWorkflowDetailState.Stopped => Color.Khaki,
        _ => Color.Gainsboro
    };

    // Native TreeView hover tips stalled in Windows text shaping during live clicks.
    // Suppress both custom node tips and automatic clipped-label tips.
    sealed class WorkflowDetailsTreeView : TreeView
    {
        protected override CreateParams CreateParams
        {
            get
            {
                var parameters = base.CreateParams;
                parameters.Style |= 0x0080; // TVS_NOTOOLTIPS
                parameters.Style &= ~0x0800; // TVS_INFOTIP
                return parameters;
            }
        }
    }

    sealed record PreparedView(long Generation, bool SameWorkflow, ObjectNode[] Roots, string? Error = null);
    sealed record ObjectNode(string Name, ReadOnlyObject? Properties, ObjectNode[] Children)
    {
        public Color Color { get; init; } = Color.White;

    }

    sealed class ReadOnlyObject : CustomTypeDescriptor
    {
        readonly object? value;
        readonly PropertyDescriptorCollection _propertyValues;
        public ReadOnlyObject(object? value)
        {
            this.value = value;
            _propertyValues = CreateProperties(value);
        }
        public override object GetPropertyOwner(PropertyDescriptor? property) => this;
        public override string GetClassName() => value?.GetType().Name ?? "Unavailable";
        public override PropertyDescriptorCollection GetProperties() => _propertyValues;
        public override PropertyDescriptorCollection GetProperties(Attribute[]? attributes) => _propertyValues;
        static PropertyDescriptorCollection CreateProperties(object? value)
        {
            if (value is null) return new([new DisplayProperty("Availability", "Object unavailable")]);
            if (IsScalar(value.GetType())) return new([new DisplayProperty("Value", Format(value))]);
            return new(TypeDescriptor.GetProperties(value).Cast<PropertyDescriptor>().Where(p => p.IsBrowsable)
                .Select(p => (PropertyDescriptor)new DisplayProperty(p.DisplayName, Format(ReadValue(p, value)), p.Description)).ToArray());
        }
        static string Format(object? item) => item switch
        {
            null => "Unavailable",
            DateTime time => time.ToString("O", CultureInfo.InvariantCulture),
            DateTimeOffset time => time.ToString("O", CultureInfo.InvariantCulture),
            _ when IsScalar(item.GetType()) => Convert.ToString(item, CultureInfo.InvariantCulture) ?? "",
            ICollection collection => $"{item.GetType().Name} ({collection.Count} items) - select in tree",
            _ => $"{item.GetType().Name} - select in tree"
        };
    }

    sealed class DisplayProperty(string name, string value, string description = "") : PropertyDescriptor(name, [new ReadOnlyAttribute(true), new DescriptionAttribute(description)])
    {
        public override Type ComponentType => typeof(ReadOnlyObject);
        public override bool IsReadOnly => true;
        public override Type PropertyType => typeof(string);
        public override bool CanResetValue(object component) => false;
        public override object GetValue(object? component) => value;
        public override void ResetValue(object component) { }
        public override void SetValue(object? component, object? value) { }
        public override bool ShouldSerializeValue(object component) => false;
    }
}
