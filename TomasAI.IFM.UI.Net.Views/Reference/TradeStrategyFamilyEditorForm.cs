// LEGACY: retained for migration/replay and UI comparison only. Active authoring uses ConfigurationDb.
// Removal criteria: Domain.Reference/Docs/Strategy-Catalog-Legacy-Retirement.md.
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Reference.Shared.ServiceApi;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.UI.Net.Views.Portfolio;
using TomasAI.IFM.UI.Net.Services.MarketData;

namespace TomasAI.IFM.UI.Net.Views.Reference;

public sealed class TradeStrategyFamilyEditorForm : DarkTradingForm
{
    public TradeStrategyFamilyEditorForm(MarketDataQueryService queries, IReferenceCommandApi commands)
    {
        Text = "Create Trade Strategy Family"; Width = 940; Height = 560;
        MinimizeBox = MaximizeBox = false; PortfolioUiStyle.Apply(this);
        var editor = new TradeStrategyFamilyEditorControl(queries, commands) { Dock = DockStyle.Fill };
        var actions = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 50, FlowDirection = FlowDirection.RightToLeft };
        var save = PortfolioUiStyle.Button("Create", "Save new trade strategy family");
        var cancel = PortfolioUiStyle.Button("Cancel", "Cancel family creation");
        save.Enabled = false;
        editor.StateChanged += (_, _) => save.Enabled = editor.CanSave;
        editor.Created += (_, _) => { DialogResult = DialogResult.OK; Close(); };
        save.Click += async (_, _) => await editor.SaveAsync();
        cancel.Click += (_, _) => Close();
        FormClosing += (_, e) => e.Cancel = editor.IsSaving;
        actions.Controls.Add(cancel); actions.Controls.Add(save);
        Controls.Add(editor); Controls.Add(actions);
    }
}

/// <summary>Reusable inline editor; its host owns Add/Save/Cancel controls.</summary>
public sealed class TradeStrategyFamilyEditorControl : DarkTradingView
{
    readonly MarketDataQueryService _queries;
    readonly IReferenceCommandApi _commands;
    readonly ComboBox _family = PortfolioUiStyle.Combo("Strategy family type");
    readonly ComboBox _strategy = PortfolioUiStyle.Combo("Strategy type");
    readonly ComboBox _timeFrame = PortfolioUiStyle.StrategyTimeFrameCombo("Strategy timeframe");
    readonly ComboBox _product = PortfolioUiStyle.Combo("Symbol");
    readonly TextBox _currency = PortfolioUiStyle.TextBox("Product currency", true);
    readonly TextBox _exchange = PortfolioUiStyle.TextBox("Product exchange", true);
    readonly TextBox _systemKey = PortfolioUiStyle.TextBox("Strategy system key", true);
    readonly TextBox _description = PortfolioUiStyle.TextBox("Strategy description");
    readonly Label _status = PortfolioUiStyle.Caption("");
    public event EventHandler? StateChanged;
    public event EventHandler? Created;
    public bool IsSaving => _saving;
    public bool HasCreated { get; private set; }
    public bool CanSave { get; private set; }
    CancellationTokenSource? _lookup;
    int _generation;
    bool _saving;
    CreateTradeStrategyFamilyRequest? _lastRequest;
    string _suggestedDescription = string.Empty;
    TradeStrategyFamilyReadModel? _original;
    Task _pendingLookup = Task.CompletedTask;
    bool _initializing;

    public TradeStrategyFamilyEditorControl(MarketDataQueryService queries, IReferenceCommandApi commands)
    {
        _queries = queries; _commands = commands;
        Name = nameof(TradeStrategyFamilyEditorControl); AccessibleName = "New trade strategy family details";
        BackColor = PortfolioUiStyle.Surface; ForeColor = Color.White; Font = PortfolioUiStyle.BodyFont;
        _family.Items.AddRange([TradeStrategyFamilyType.Futures, TradeStrategyFamilyType.FuturesOption]);
        _description.MaxLength = 512; _description.Multiline = true; _description.AcceptsReturn = true;
        _description.ReadOnly = false; _description.Enabled = true; _description.ScrollBars = ScrollBars.Vertical;
        _description.BorderStyle = BorderStyle.Fixed3D;
        _product.DropDownWidth = 750;
        var body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(12), AutoScroll = true };
        body.ColumnStyles.Add(new(SizeType.Absolute, 140)); body.ColumnStyles.Add(new(SizeType.Percent, 100));
        var fields = new (string, Control)[] { ("Family", _family), ("Symbol", _product), ("Strategy", _strategy), ("TimeFrame", _timeFrame), ("Currency", _currency), ("Exchange", _exchange), ("SystemKey", _systemKey), ("Description", _description) };
        foreach (var (caption, control) in fields)
        {
            control.BackColor = Color.Black; control.ForeColor = Color.White;
            if (control is ComboBox combo)
            {
                combo.DrawMode = DrawMode.OwnerDrawFixed;
                combo.DrawItem += DrawBlackComboBoxItem;
            }
            var row = body.RowCount++; body.RowStyles.Add(new(SizeType.Absolute, caption == "Description" ? 76 : 36));
            var label = PortfolioUiStyle.Caption(caption);
            label.TextAlign = ContentAlignment.TopRight;
            label.Padding = new Padding(3, 3, 3, 0);
            label.Margin = control.Margin;
            body.Controls.Add(label, 0, row); body.Controls.Add(control, 1, row);
        }
        body.RowCount++; body.RowStyles.Add(new(SizeType.Percent, 100));
        _status.Visible = false;
        _status.TextChanged += (_, _) => _status.Visible = !string.IsNullOrEmpty(_status.Text);
        _status.AutoSize = false; _status.Dock = DockStyle.Bottom; _status.Height = 58;
        _status.TextAlign = ContentAlignment.TopLeft; _status.Padding = new Padding(12, 6, 12, 0);
        Controls.Add(body); Controls.Add(_status);
        _family.SelectedIndexChanged += (_, _) => _pendingLookup = LoadProductsAsync();
        _product.SelectedIndexChanged += (_, _) => { BindProduct(); UpdateSave(); };
        _strategy.SelectedIndexChanged += (_, _) => { UpdateSystemKey(); UpdateSave(); };
        _timeFrame.SelectedIndexChanged += (_, _) => UpdateSave();
        _description.TextChanged += (_, _) => UpdateSave();
    }

    // Match the Trade Orders blotter, including the closed selection and disabled text.
    static void DrawBlackComboBoxItem(object? sender, DrawItemEventArgs e)
    {
        if (sender is not ComboBox combo || e.Bounds.Width <= 0 || e.Bounds.Height <= 0)
            return;

        var selected = combo.Enabled && (e.State & DrawItemState.Selected) != 0;
        using var background = new SolidBrush(selected ? SystemColors.Highlight : Color.Black);
        e.Graphics.FillRectangle(background, e.Bounds);
        var text = e.Index >= 0 && e.Index < combo.Items.Count
            ? combo.GetItemText(combo.Items[e.Index])
            : combo.Text;
        TextRenderer.DrawText(e.Graphics, text, combo.Font, e.Bounds,
            combo.Enabled ? Color.White : Color.Gray,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter
            | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        if (combo.Enabled && (e.State & DrawItemState.Focus) != 0)
            e.DrawFocusRectangle();
    }

    public async Task InitializeChangeAsync(TradeStrategyFamilyReadModel original)
    {
        _original = original; _initializing = true;
        SetEditing(false);
        AccessibleName = "Change trade strategy family details";
        _description.Text = original.Description;
        try
        {
            _family.SelectedItem = original.Family;
            await _pendingLookup;
            if (IsDisposed) return;
            var matches = _product.Items.Cast<ProductChoice>().Where(x => original.TradeStrategySymbolId > 0
                ? x.Value.Id == original.TradeStrategySymbolId
                : x.Value.Symbol == original.Symbol && x.Value.Currency == original.Currency &&
                    (string.IsNullOrEmpty(original.Exchange) || x.Value.Exchange == original.Exchange)).ToArray();
            if (matches.Length == 1) _product.SelectedItem = matches[0];
            _strategy.SelectedItem = original.Strategy;
            _timeFrame.SelectedItem = original.TimeFrame;
            _description.Text = original.Description;
        }
        finally { _initializing = false; if (!IsDisposed) SetEditing(true); }
    }

    async Task LoadProductsAsync()
    {
        var generation = ++_generation;
        _lookup?.Cancel(); _lookup?.Dispose(); _lookup = new(); var token = _lookup.Token;
        _product.Items.Clear(); _product.SelectedIndex = -1; BindProduct();
        _strategy.Items.Clear();
        if (_family.SelectedItem is not TradeStrategyFamilyType family) { UpdateSave(); return; }
        _strategy.Items.AddRange(family == TradeStrategyFamilyType.Futures ? [TradeStrategyType.Futures] : [TradeStrategyType.IronCondor, TradeStrategyType.VerticalSpread]);
        if (_strategy.Items.Count == 1) _strategy.SelectedIndex = 0;
        UpdateSystemKey(); UpdateSave(); _status.Text = "Loading symbols...";
        try
        {
            var result = await _queries.GetTradeStrategySymbolsAsync(family, token);
            if (token.IsCancellationRequested || generation != _generation || IsDisposed) return;
            if (!result.Success || result.Value is null) { _status.Text = result.ErrorMessage ?? "Product catalog unavailable."; return; }
            if (result.Value.Any(x => x is null || x.Validate().Count != 0) || result.Value.Select(x => x.Id).Distinct().Count() != result.Value.Length)
            { _status.Text = "Incomplete or ambiguous product metadata was returned. Creation is blocked."; return; }
            _product.Items.AddRange(result.Value.Select(x => new ProductChoice(x)).Cast<object>().ToArray());
            if (_product.Items.Count == 1) _product.SelectedIndex = 0;
            _status.Text = result.Value.Length == 0 ? "No supported products for this family." : "";
            UpdateSave();
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex) { if (generation == _generation && !IsDisposed) _status.Text = ex.Message; }
    }
    void BindProduct()
    {
        var product = (_product.SelectedItem as ProductChoice)?.Value;
        _currency.Text = product?.Currency ?? ""; _exchange.Text = product?.Exchange ?? "";
        if (string.IsNullOrWhiteSpace(_description.Text) || _description.Text == _suggestedDescription)
            _description.Text = product?.Description ?? "";
        _suggestedDescription = product?.Description ?? "";
    }
    void UpdateSystemKey() => _systemKey.Text = _family.SelectedItem is TradeStrategyFamilyType family && _strategy.SelectedItem is TradeStrategyType strategy
        ? TradeStrategyFamilyReadModel.ComposeSystemKey(family, strategy) : "";
    CreateTradeStrategyFamilyRequest? Request() => _family.SelectedItem is TradeStrategyFamilyType family && _strategy.SelectedItem is TradeStrategyType strategy &&
        _timeFrame.SelectedItem is TimeFrameType timeFrame && _product.SelectedItem is ProductChoice product ?
        new() { Family = family, Strategy = strategy, TimeFrame = timeFrame, TradeStrategySymbolId = product.Value.Id, Description = _description.Text.Trim() } : null;
    void UpdateSave()
    {
        CanSave = !_initializing && !_saving && !HasCreated && Request() is { } request && (request with { OperationId = Guid.NewGuid() }).Validate().Count == 0;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
    public async Task SaveAsync()
    {
        if (_initializing || _saving || HasCreated || Request() is not { } request) return;
        // Preserve the operation ID for an identical retry after timeout; changed input starts a new operation.
        if (_lastRequest is null || (_lastRequest with { OperationId = Guid.Empty }) != request) _lastRequest = request with { OperationId = Guid.NewGuid() };
        if (_lastRequest.Validate().Count != 0) return;
        _saving = true; SetEditing(false); _status.Text = _original is null ? "Creating family definition..." : "Saving family changes...";
        try
        {
            var result = _original is null ? await _commands.CreateTradeStrategyFamilyAsync(_lastRequest)
                : await _commands.ChangeTradeStrategyFamilyAsync(new() { OperationId = _lastRequest.OperationId,
                    Target = TradeStrategyFamilyReference.From(_original), Definition = _lastRequest });
            if (IsDisposed) return;
            if (!result.Success) { _status.Text = result.ErrorMessage ?? "Save failed; retry uses the same operation ID."; return; }
            _saving = false; HasCreated = true; Created?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex) { if (!IsDisposed) _status.Text = ex.Message; }
        finally { _saving = false; if (!IsDisposed) { SetEditing(true); UpdateSave(); } }
    }
    void SetEditing(bool enabled)
    {
        foreach (Control control in new Control[] { _family, _strategy, _timeFrame, _product, _description }) control.Enabled = enabled;
        UpdateSave();
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing) { ++_generation; _lookup?.Cancel(); _lookup?.Dispose(); _lookup = null; }
        base.Dispose(disposing);
    }
    sealed record ProductChoice(TradeStrategySymbolReadModel Value)
    {
        public override string ToString() => $"{Value.Symbol} — {Value.Exchange} / {Value.Currency} — {Value.Description}";
    }
}
