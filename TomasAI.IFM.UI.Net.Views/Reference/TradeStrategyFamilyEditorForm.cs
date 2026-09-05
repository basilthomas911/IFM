using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Reference.Shared.ServiceApi;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.UI.Net.Views.Portfolio;

namespace TomasAI.IFM.UI.Net.Views.Reference;

public sealed class TradeStrategyFamilyEditorForm : Form
{
    readonly IReferenceQueryApi _queries;
    readonly IReferenceCommandApi _commands;
    readonly ComboBox _family = PortfolioUiStyle.Combo("Strategy family type");
    readonly ComboBox _strategy = PortfolioUiStyle.Combo("Strategy type");
    readonly ComboBox _timeFrame = PortfolioUiStyle.StrategyTimeFrameCombo("Strategy timeframe");
    readonly ComboBox _product = PortfolioUiStyle.Combo("Trade strategy product");
    readonly TextBox _currency = PortfolioUiStyle.TextBox("Product currency", true);
    readonly TextBox _exchange = PortfolioUiStyle.TextBox("Product exchange", true);
    readonly TextBox _systemKey = PortfolioUiStyle.TextBox("Strategy system key", true);
    readonly TextBox _description = PortfolioUiStyle.TextBox("Strategy description");
    readonly Label _status = PortfolioUiStyle.Caption("Select a family to load product metadata.");
    readonly Button _save = PortfolioUiStyle.Button("Create", "Save new trade strategy family");
    CancellationTokenSource? _lookup;
    int _generation;
    bool _saving;
    CreateTradeStrategyFamilyRequest? _lastRequest;

    public TradeStrategyFamilyEditorForm(IReferenceQueryApi queries, IReferenceCommandApi commands)
    {
        _queries = queries; _commands = commands;
        Text = "Create Trade Strategy Family"; Name = nameof(TradeStrategyFamilyEditorForm); AccessibleName = Text;
        Width = 940; Height = 560; MinimizeBox = MaximizeBox = false; PortfolioUiStyle.Apply(this);
        _family.Items.AddRange([TradeStrategyFamilyType.Futures, TradeStrategyFamilyType.FuturesOption]);
        _description.MaxLength = 512; _product.DropDownWidth = 750;
        var body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(12), AutoScroll = true };
        body.ColumnStyles.Add(new(SizeType.Absolute, 210)); body.ColumnStyles.Add(new(SizeType.Percent, 100));
        var fields = new (string, Control)[] { ("Family", _family), ("Strategy", _strategy), ("TimeFrame", _timeFrame), ("Symbol / Product", _product), ("Currency", _currency), ("Exchange", _exchange), ("SystemKey", _systemKey), ("Description", _description) };
        foreach (var (caption, control) in fields)
        {
            var row = body.RowCount++; body.RowStyles.Add(new(SizeType.Absolute, 44));
            body.Controls.Add(PortfolioUiStyle.Caption(caption), 0, row); body.Controls.Add(control, 1, row);
        }
        _status.Dock = DockStyle.Bottom; _status.Height = 58;
        var actions = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 50, FlowDirection = FlowDirection.RightToLeft };
        var cancel = PortfolioUiStyle.Button("Cancel", "Cancel family creation");
        cancel.Click += (_, _) => Close(); actions.Controls.Add(cancel); actions.Controls.Add(_save);
        Controls.Add(body); Controls.Add(_status); Controls.Add(actions);
        _save.Enabled = false;
        _family.SelectedIndexChanged += async (_, _) => await LoadProductsAsync();
        _product.SelectedIndexChanged += (_, _) => { BindProduct(); UpdateSave(); };
        _strategy.SelectedIndexChanged += (_, _) => { UpdateSystemKey(); UpdateSave(); };
        _timeFrame.SelectedIndexChanged += (_, _) => UpdateSave();
        _description.TextChanged += (_, _) => UpdateSave();
        _save.Click += async (_, _) => await SaveAsync();
        FormClosing += (_, e) => { if (_saving) { e.Cancel = true; return; } ++_generation; _lookup?.Cancel(); };
        FormClosed += (_, _) => _lookup?.Dispose();
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
        UpdateSystemKey(); _save.Enabled = false; _status.Text = "Loading Databento product metadata...";
        try
        {
            var result = await _queries.GetTradeStrategySymbolsAsync(family, token);
            if (token.IsCancellationRequested || generation != _generation || IsDisposed) return;
            if (!result.Success || result.Value is null) { _status.Text = result.ErrorMessage ?? "Product catalog unavailable."; return; }
            if (result.Value.Any(x => x is null || x.Validate().Count != 0) || result.Value.Select(x => x.Id).Distinct().Count() != result.Value.Length)
            { _status.Text = "Incomplete or ambiguous product metadata was returned. Creation is blocked."; return; }
            _product.Items.AddRange(result.Value.Select(x => new ProductChoice(x)).Cast<object>().ToArray());
            _status.Text = result.Value.Length == 0 ? "No supported products for this family." : "Select a product, strategy and timeframe.";
            UpdateSave();
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex) { if (generation == _generation && !IsDisposed) _status.Text = ex.Message; }
    }
    void BindProduct()
    {
        var product = (_product.SelectedItem as ProductChoice)?.Value;
        _currency.Text = product?.Currency ?? ""; _exchange.Text = product?.Exchange ?? "";
    }
    void UpdateSystemKey() => _systemKey.Text = _family.SelectedItem is TradeStrategyFamilyType family && _strategy.SelectedItem is TradeStrategyType strategy
        ? TradeStrategyFamilyReadModel.ComposeSystemKey(family, strategy) : "";
    CreateTradeStrategyFamilyRequest? Request() => _family.SelectedItem is TradeStrategyFamilyType family && _strategy.SelectedItem is TradeStrategyType strategy &&
        _timeFrame.SelectedItem is TimeFrameType timeFrame && _product.SelectedItem is ProductChoice product ?
        new() { Family = family, Strategy = strategy, TimeFrame = timeFrame, TradeStrategySymbolId = product.Value.Id, Description = _description.Text.Trim() } : null;
    void UpdateSave() => _save.Enabled = !_saving && Request() is { } request && (request with { OperationId = Guid.NewGuid() }).Validate().Count == 0;
    async Task SaveAsync()
    {
        if (_saving || Request() is not { } request) return;
        // Preserve the operation ID for an identical retry after timeout; changed input starts a new operation.
        if (_lastRequest is null || (_lastRequest with { OperationId = Guid.Empty }) != request) _lastRequest = request with { OperationId = Guid.NewGuid() };
        if (_lastRequest.Validate().Count != 0) return;
        _saving = true; SetEditing(false); _status.Text = "Creating family definition...";
        try
        {
            var result = await _commands.CreateTradeStrategyFamilyAsync(_lastRequest);
            if (!result.Success) { _status.Text = result.ErrorMessage ?? "Creation failed; retry uses the same operation ID."; return; }
            _saving = false; DialogResult = DialogResult.OK; Close();
        }
        catch (Exception ex) { _status.Text = ex.Message; }
        finally { _saving = false; if (!IsDisposed) { SetEditing(true); UpdateSave(); } }
    }
    void SetEditing(bool enabled)
    {
        foreach (Control control in new Control[] { _family, _strategy, _timeFrame, _product, _description }) control.Enabled = enabled;
        _save.Enabled = enabled;
    }
    sealed record ProductChoice(TradeStrategySymbolReadModel Value)
    {
        public override string ToString() => $"{Value.Symbol} — {Value.Exchange} / {Value.Currency} — {Value.Description}";
    }
}
