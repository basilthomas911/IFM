using TomasAI.IFM.Domain.Reference.Shared.ServiceApi;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Views.Portfolio;
using TomasAI.IFM.UI.Net.Services.MarketData;

namespace TomasAI.IFM.UI.Net.Views.Reference;

/// <summary>Lookup-style list/detail editor with versioned changes and retirement.</summary>
public sealed class TradeStrategyFamilyReferenceView : UserControl, IControlCommand, IAsyncFormControl
{
    readonly IReferenceQueryApi _queries;
    readonly IReferenceCommandApi? _commands;
    readonly MarketDataQueryService? _marketData;
    readonly Func<string, DialogResult> _confirmRemove;
    readonly ListBox _families = List("Family");
    readonly ListBox _strategies = List("Strategy");
    readonly Panel _detailHost = new() { Dock = DockStyle.Fill };
    readonly TableLayoutPanel _details = new() { Dock = DockStyle.Fill, ColumnCount = 2, AutoScroll = true, Padding = new Padding(8) };
    readonly Dictionary<string, TextBox> _fields = [];
    readonly Label _error = new() { Dock = DockStyle.Top, AutoSize = true, ForeColor = Color.White, Visible = false };
    readonly CancellationTokenSource _lifetime = new();
    TradeStrategyFamilyReadModel[] _rows = [];
    TradeStrategyFamilyEditorControl? _editor;
    Task _pendingSave = Task.CompletedTask;
    bool _loaded, _closing, _saving;
    bool _changing;
    RemoveTradeStrategyFamilyRequest? _removeRequest;
    int _loadGeneration;

    public event EventHandler? StateChanged;
    public bool CanAdd => _loaded && _commands is not null && _marketData is not null && !_closing && !IsEditing && !IsSaving;
    public bool IsEditing => _editor is not null;
    public bool IsChanging => IsEditing && _changing;
    public bool IsSaving => _saving || _editor?.IsSaving == true;
    public bool CanSave => _editor?.CanSave == true && !IsSaving;
    public bool CanChangeRemove => CanAdd && _strategies.SelectedItem is StrategyChoice;
    public bool CanImport => false;

    public TradeStrategyFamilyReferenceView(IReferenceQueryApi queries, IReferenceCommandApi? commands = null, MarketDataQueryService? marketData = null,
        Func<string, DialogResult>? confirmRemove = null)
    {
        _queries = queries; _commands = commands; _marketData = marketData;
        _confirmRemove = confirmRemove ?? (message => MessageBox.Show(this, message, "Remove Trade Strategy Family",
            MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2));
        Name = nameof(TradeStrategyFamilyReferenceView); BackColor = PortfolioUiStyle.Surface; ForeColor = Color.White; Font = PortfolioUiStyle.BodyFont;
        // Bounded table panels preserve the Lookup Type layout without SplitContainer's
        // first-paint GDI failure (also avoided by the Portfolio risk-policy screen).
        var split = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, Margin = Padding.Empty, Padding = Padding.Empty };
        split.ColumnStyles.Add(new(SizeType.Percent, 37));
        split.ColumnStyles.Add(new(SizeType.Absolute, 5));
        split.ColumnStyles.Add(new(SizeType.Percent, 63));
        split.RowStyles.Add(new(SizeType.Percent, 100));
        var lists = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Margin = Padding.Empty };
        lists.ColumnStyles.Add(new(SizeType.Percent, 100));
        lists.RowStyles.Add(new(SizeType.Absolute, 30)); lists.RowStyles.Add(new(SizeType.Percent, 42));
        lists.RowStyles.Add(new(SizeType.Absolute, 30)); lists.RowStyles.Add(new(SizeType.Percent, 58));
        lists.Controls.Add(Header("Family"), 0, 0); lists.Controls.Add(_families, 0, 1);
        lists.Controls.Add(Header("Strategy"), 0, 2); lists.Controls.Add(_strategies, 0, 3);
        _detailHost.Margin = Padding.Empty;
        split.Controls.Add(lists, 0, 0); split.Controls.Add(_detailHost, 2, 0);
        _details.ColumnStyles.Add(new(SizeType.Absolute, 135)); _details.ColumnStyles.Add(new(SizeType.Percent, 100));
        _details.RowCount = 0;
        foreach (var name in new[] { "ID", "Version", "SystemKey", "Family", "Strategy", "TimeFrame", "Symbol", "Currency", "Exchange", "Description", "State", "Created UTC", "Created By" })
        {
            var row = _details.RowCount++; _details.RowStyles.Add(new(SizeType.Absolute, name == "Description" ? 65 : 34));
            var text = PortfolioUiStyle.TextBox(name, true);
            text.BackColor = Color.Black; text.ForeColor = Color.White; text.BorderStyle = BorderStyle.FixedSingle;
            if (name == "Description") { text.Multiline = true; text.Dock = DockStyle.Fill; }
            var label = PortfolioUiStyle.Caption(name + ":");
            label.TextAlign = ContentAlignment.TopRight;
            label.Padding = new Padding(3, 2, 3, 0);
            label.Margin = text.Margin;
            _fields.Add(name, text); _details.Controls.Add(label, 0, row); _details.Controls.Add(text, 1, row);
        }
        _detailHost.Controls.Add(_details);
        _detailHost.Controls.Add(_error);
        Controls.Add(split);
        _families.SelectedIndexChanged += (_, _) => BindStrategies();
        _strategies.SelectedIndexChanged += (_, _) => BindDetails();
    }

    static ListBox List(string name) => new()
    {
        Name = name.Replace(" ", ""), AccessibleName = name, Dock = DockStyle.Fill,
        BackColor = Color.Black, ForeColor = Color.White,
        BorderStyle = BorderStyle.FixedSingle, IntegralHeight = false, HorizontalScrollbar = true, Margin = Padding.Empty
    };
    static Label Header(string caption) => new()
    {
        Text = caption, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter,
        BackColor = Color.Gray, ForeColor = Color.Black, Margin = Padding.Empty
    };

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        if (_closing || IsDisposed) return;
        var generation = ++_loadGeneration; _loaded = false; _error.Visible = false; _details.Visible = true; NotifyState();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token, cancellationToken);
        try
        {
            var result = await _queries.GetTradeStrategyFamiliesAsync(linked.Token);
            if (_closing || IsDisposed || generation != _loadGeneration || linked.IsCancellationRequested) return;
            if (!result.Success || result.Value is null) throw new InvalidOperationException(result.ErrorMessage ?? "Family catalog unavailable.");
            if (result.Value.Any(x => x is null || x.Validate().Count != 0) ||
                result.Value.Select(TradeStrategyFamilyReference.From).Distinct().Count() != result.Value.Length)
                throw new InvalidOperationException("Invalid or duplicate family identities were returned.");
            _rows = result.Value.GroupBy(x => x.TradeStrategyFamilyId).Select(x => x.MaxBy(v => v.DefinitionVersion)!)
                .Where(x => x.State == TradeStrategyFamilyState.Active).OrderBy(x => x.SystemKey).ThenBy(x => x.TradeStrategyFamilyId).ToArray();
            _loaded = true; BindFamilies();
        }
        catch (OperationCanceledException) when (linked.IsCancellationRequested) { }
        catch (Exception ex)
        {
            if (_closing || IsDisposed || generation != _loadGeneration) return;
            _rows = []; BindFamilies();
            _details.Visible = false; _error.Text = $"Family catalog unavailable: {ex.Message}"; _error.Visible = true; _error.BringToFront();
        }
        finally { if (!_closing && !IsDisposed && generation == _loadGeneration) NotifyState(); }
    }

    void BindFamilies()
    {
        _families.Items.Clear();
        _families.Items.AddRange(_rows.Select(x => x.Family).Distinct().OrderBy(x => x).Cast<object>().ToArray());
        if (_families.Items.Count > 0) _families.SelectedIndex = 0; else BindStrategies();
    }
    void BindStrategies()
    {
        _strategies.Items.Clear();
        if (_families.SelectedItem is TradeStrategyFamilyType family)
        {
            var rows = _rows.Where(x => x.Family == family).OrderBy(x => x.Strategy).ToArray();
            _strategies.Items.AddRange(rows.Select(x => new StrategyChoice(x,
                rows.Count(other => other.Strategy == x.Strategy) > 1)).Cast<object>().ToArray());
        }
        if (_strategies.Items.Count > 0) _strategies.SelectedIndex = 0; else BindDetails();
    }
    void BindDetails()
    {
        var row = (_strategies.SelectedItem as StrategyChoice)?.Value;
        var values = row is null ? [] : new string[] { row.TradeStrategyFamilyId.ToString(), row.DefinitionVersion.ToString(), row.SystemKey,
            row.Family.ToString(), row.Strategy.ToString(), row.TimeFrame.ToString(), row.Symbol, row.Currency, row.Exchange, row.Description,
            row.State.ToString(), row.CreatedOnUtc.ToString("yyyy-MM-dd HH:mm:ss 'UTC'"), row.CreatedBy };
        var index = 0;
        foreach (var field in _fields.Values) field.Text = row is null ? "" : values[index++];
        NotifyState();
    }

    public void Add(Action<bool> addAction)
    {
        if (_closing || IsSaving || IsChanging) return;
        if (_editor is null)
        {
            if (!CanAdd) return;
            _changing = false;
            _error.Visible = false;
            _editor = new(_marketData!, _commands!) { Dock = DockStyle.Fill };
            _editor.StateChanged += (_, _) => NotifyState();
            _details.Visible = false; _detailHost.Controls.Add(_editor); _editor.BringToFront();
            _families.Enabled = _strategies.Enabled = false;
            addAction(false); NotifyState();
        }
        else if (_editor.CanSave)
        {
            _saving = true;
            _pendingSave = SaveAndReloadAsync(addAction);
            NotifyState();
        }
    }
    async Task SaveAndReloadAsync(Action<bool> addAction)
    {
        try
        {
            var editor = _editor!;
            await editor.SaveAsync();
            if (_closing || IsDisposed) return;
            if (editor.HasCreated)
            {
                var changedId = _changing ? (_strategies.SelectedItem as StrategyChoice)?.Value.TradeStrategyFamilyId : null;
                EndEditing(); await LoadAsync();
                if (changedId is { } id && _rows.SingleOrDefault(x => x.TradeStrategyFamilyId == id) is { } changed)
                {
                    _families.SelectedItem = changed.Family;
                    _strategies.SelectedItem = _strategies.Items.Cast<StrategyChoice>().Single(x => x.Value.TradeStrategyFamilyId == id);
                }
                addAction(true);
            }
        }
        finally { _saving = false; if (!_closing && !IsDisposed) NotifyState(); }
    }
    void EndEditing()
    {
        if (_editor is not null) { _detailHost.Controls.Remove(_editor); _editor.Dispose(); _editor = null; }
        _changing = false;
        _details.Visible = true; _families.Enabled = _strategies.Enabled = true; BindDetails();
    }
    public bool Close(Action<bool> closeAction)
    {
        if (IsSaving) return false;
        if (_editor is null) return true;
        EndEditing(); closeAction(true); NotifyState(); return false;
    }
    public void Change(Action<bool> changeAction)
    {
        if (_closing || IsSaving) return;
        if (_editor is not null)
        {
            if (!_changing || !_editor.CanSave) return;
            _saving = true; _pendingSave = SaveAndReloadAsync(changeAction); NotifyState(); return;
        }
        if (!CanChangeRemove || _strategies.SelectedItem is not StrategyChoice selected) return;
        _changing = true; _saving = true;
        _error.Visible = false;
        _editor = new(_marketData!, _commands!) { Dock = DockStyle.Fill };
        _editor.StateChanged += (_, _) => NotifyState();
        _details.Visible = false; _detailHost.Controls.Add(_editor); _editor.BringToFront();
        _families.Enabled = _strategies.Enabled = false;
        changeAction(false); NotifyState();
        _pendingSave = InitializeChangeAsync(selected.Value);
    }
    async Task InitializeChangeAsync(TradeStrategyFamilyReadModel selected)
    {
        try { await _editor!.InitializeChangeAsync(selected); }
        catch (Exception ex) { if (!_closing && !IsDisposed) ShowError(ex.Message); }
        finally { _saving = false; if (!_closing && !IsDisposed) NotifyState(); }
    }
    public void Remove()
    {
        if (!CanChangeRemove || _strategies.SelectedItem is not StrategyChoice selected) return;
        if (_confirmRemove($"Remove {FormatStrategy(selected.Value)} ?") != DialogResult.Yes) return;
        var target = TradeStrategyFamilyReference.From(selected.Value);
        if (_removeRequest?.Target != target) _removeRequest = new() { OperationId = Guid.NewGuid(), Target = target };
        _saving = true; _families.Enabled = _strategies.Enabled = false; NotifyState();
        _pendingSave = RemoveAndReloadAsync(_removeRequest);
    }
    async Task RemoveAndReloadAsync(RemoveTradeStrategyFamilyRequest request)
    {
        try
        {
            var result = await _commands!.RemoveTradeStrategyFamilyAsync(request);
            if (_closing || IsDisposed) return;
            if (!result.Success) { ShowError(result.ErrorMessage ?? "Removal failed."); return; }
            _removeRequest = null; await LoadAsync();
        }
        catch (Exception ex) { if (!_closing && !IsDisposed) ShowError(ex.Message); }
        finally { _saving = false; if (!_closing && !IsDisposed) { _families.Enabled = _strategies.Enabled = true; NotifyState(); } }
    }
    void ShowError(string message) { _error.Text = message; _error.Visible = true; _error.BringToFront(); }
    public void Import() { }
    public void Load(IAppRoot appRoot, Action<bool> dataLoaded) => _ = LoadAndNotifyAsync(dataLoaded);
    async Task LoadAndNotifyAsync(Action<bool> dataLoaded) { await LoadAsync(); if (!_closing) dataLoaded(CanAdd); }
    public void Unload() => _ = CloseAsync();
    public async ValueTask CloseAsync()
    {
        if (IsDisposed) return;
        _closing = true; ++_loadGeneration; _lifetime.Cancel(); await _pendingSave; EndEditing();
    }
    public void Open() { }
    public void Resize(Control parentControl) => Dock = DockStyle.Fill;
    public void Close() => Unload();
    void NotifyState() => StateChanged?.Invoke(this, EventArgs.Empty);
    protected override void Dispose(bool disposing)
    {
        if (disposing && !IsDisposed) { _closing = true; ++_loadGeneration; _lifetime.Cancel(); _lifetime.Dispose(); }
        base.Dispose(disposing);
    }
    static string FormatStrategy(TradeStrategyFamilyReadModel value) => $"{value.Strategy}-{value.Symbol}-{value.Currency}-{value.TimeFrame} {value.Exchange}";
    sealed record StrategyChoice(TradeStrategyFamilyReadModel Value, bool ShowIdentity)
    {
        public override string ToString() => ShowIdentity
            ? FormatStrategy(Value)
            : Value.Strategy.ToString();
    }
}
