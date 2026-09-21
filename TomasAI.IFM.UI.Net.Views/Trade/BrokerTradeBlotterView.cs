using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Application.TradeBroker.Contracts;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Views.Presentation;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;
using AppBrokerAlgorithm = TomasAI.IFM.Application.TradeBroker.Contracts.BrokerAlgorithm;
using AppBrokerOrderType = TomasAI.IFM.Application.TradeBroker.Contracts.BrokerOrderType;

namespace TomasAI.IFM.UI.Net.Views.Trade;

/// <summary>Reusable Stage 3 three-tab blotter. It displays evidence and raises commands; application workflows own mutation.</summary>
public class EsTradeBlotterControl : DarkTradingView, ITradeOrderControl, IAsyncFormControl
{
    public const int VisibleChainRowCapacity = 14;
    private static readonly Color ShortColor = Color.FromArgb(110, 24, 30);
    private static readonly Color LongColor = Color.FromArgb(20, 54, 105);
    private readonly BrokerExecutionEvidenceControl _evidence;
    private readonly DataGridView _marketGrid;
    private readonly DataGridView _legGrid;
    private readonly ComboBox _strategySelector;
    private readonly ComboBox _directionSelector;
    private readonly ComboBox _brokerModeSelector;
    private readonly ComboBox _algorithmSelector;
    private readonly ComboBox _orderTypeSelector;
    private readonly Label _sourceLabel;
    private readonly Control? _workflowControl;
    private readonly ITradeOrderControl? _workflow;
    private readonly BrokerCapabilities _capabilities;
    private readonly bool _readOnly;
    private readonly VolatilityContextHistoryControl _volatilityContext;
    private TradeBlotterStagingResult? _staging;

    public event EventHandler? SubmitOpeningRequested;
    public event EventHandler? SubmitClosingRequested;
    public event EventHandler? EndOfDayRequested;
    public event EventHandler? RecalculateRequested;

    public EsTradeBlotterControl(IAppRoot appRoot, FundReadModel fund, FundOrderReadModel order,
        FundOrderTradeReadModel trade, int portfolioId, bool historicalReadOnly,
        BrokerCapabilities? capabilities = null, Control? workflowControl = null)
    {
        ArgumentNullException.ThrowIfNull(appRoot);
        Name = "esTradeBlotter";
        AccessibleName = "ES three tab trade blotter";
        Dock = DockStyle.Fill;
        MinimumSize = new Size(900, 410);
        BackColor = Color.Black;
        ForeColor = Color.White;
        Font = new Font("Microsoft Sans Serif", 9F);

        var strategy = trade.TradeType switch
        {
            TradeType.ShortIronCondor or TradeType.LongIronCondor => "Iron Condor",
            TradeType.FuturesOutright => "Futures Outright",
            _ => "Vertical Spread"
        };
        var direction = trade.TradeType is TradeType.LongIronCondor or TradeType.CallDebitSpread
            or TradeType.PutDebitSpread ? "Long" : "Short";
        capabilities ??= BrokerCapabilities.Emulator("IFM-EMULATOR-PAPER");
        _capabilities = capabilities;
        _workflowControl = workflowControl;
        _workflow = workflowControl as ITradeOrderControl;
        _readOnly = historicalReadOnly || trade.TradeState != TradeState.NewTrade || _workflow is null;

        var header = new TableLayoutPanel
        {
            Name = "tradeBlotterHeader", Dock = DockStyle.Top, Height = 38, ColumnCount = 9,
            BackColor = Color.FromArgb(32, 32, 32), Padding = new Padding(6, 5, 6, 3)
        };
        header.ColumnStyles.Add(new(SizeType.AutoSize)); header.ColumnStyles.Add(new(SizeType.Absolute, 180));
        header.ColumnStyles.Add(new(SizeType.AutoSize)); header.ColumnStyles.Add(new(SizeType.Absolute, 130));
        header.ColumnStyles.Add(new(SizeType.Percent, 100));
        header.ColumnStyles.Add(new(SizeType.AutoSize)); header.ColumnStyles.Add(new(SizeType.Absolute, 120));
        header.ColumnStyles.Add(new(SizeType.AutoSize)); header.ColumnStyles.Add(new(SizeType.Absolute, 205));
        _strategySelector = Selector("strategySelector", ["Iron Condor", "Vertical Spread", "Futures Outright"], strategy);
        _directionSelector = Selector("directionSelector", ["Short", "Long"], direction);
        _brokerModeSelector = Selector("brokerModeSelector", [capabilities.Environment.ToString()], capabilities.Environment.ToString());
        header.Controls.Add(HeaderLabel("Strategy"), 0, 0); header.Controls.Add(_strategySelector, 1, 0);
        header.Controls.Add(HeaderLabel("Direction"), 2, 0); header.Controls.Add(_directionSelector, 3, 0);
        header.Controls.Add(HeaderLabel("Broker Mode"), 5, 0); header.Controls.Add(_brokerModeSelector, 6, 0);
        _sourceLabel = HeaderLabel(historicalReadOnly ? "Historical read-only" : "Manual submitted / evidence");
        _sourceLabel.Name = "sourceModeLabel"; header.Controls.Add(_sourceLabel, 8, 0);

        var tabs = new TabControl { Name = "tradeBlotterTabs", Dock = DockStyle.Fill, Appearance = TabAppearance.Normal };
        var market = new TabPage("Market Selection") { Name = "marketSelectionTab", BackColor = Color.Black, ForeColor = Color.White };
        var staging = new TabPage("Leg Staging") { Name = "legStagingTab", BackColor = Color.Black, ForeColor = Color.White };
        var orders = new TabPage("Orders and Fills") { Name = "ordersAndFillsTab", BackColor = Color.Black, ForeColor = Color.White };
        var volatility = new TabPage("Volatility Context") { Name = "volatilityContextTab", BackColor = Color.Black, ForeColor = Color.White };
        tabs.TabPages.AddRange([market, staging, orders, volatility]);
        _volatilityContext = new VolatilityContextHistoryControl();
        volatility.Controls.Add(_volatilityContext);

        _marketGrid = Grid("marketSelectionGrid");
        _marketGrid.VirtualMode = true;
        _marketGrid.MaximumSize = new Size(0,
            _marketGrid.ColumnHeadersHeight + _marketGrid.RowTemplate.Height * VisibleChainRowCapacity + 2);
        _marketGrid.Columns.Add("Contract", "Contract"); _marketGrid.Columns.Add("Right", "Right");
        _marketGrid.Columns.Add("Strike", "Strike"); _marketGrid.Columns.Add("Bid", "Bid");
        _marketGrid.Columns.Add("Ask", "Ask"); _marketGrid.Columns.Add("Delta", "Delta");
        _marketGrid.Columns.Add("Age", "Quote Age / Source");
        _marketGrid.CellValueNeeded += MarketCellValueNeeded;
        if (_workflowControl is null)
        {
            market.Controls.Add(_marketGrid);
        }
        else
        {
            var marketLayout = new TableLayoutPanel
            {
                Name = "marketSelectionLayout", Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1,
                BackColor = Color.Black
            };
            marketLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 68));
            marketLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 32));
            _workflowControl.Name = string.IsNullOrWhiteSpace(_workflowControl.Name)
                ? "strategyWorkflowEditor"
                : _workflowControl.Name;
            _workflowControl.Dock = DockStyle.Fill;
            marketLayout.Controls.Add(_workflowControl, 0, 0);
            marketLayout.Controls.Add(_marketGrid, 0, 1);
            market.Controls.Add(marketLayout);
        }

        _legGrid = Grid("legStagingGrid");
        _legGrid.Columns.Add("Leg", "Leg"); _legGrid.Columns.Add("Delta", "Delta");
        _legGrid.Columns.Add("Side", "Broker Side"); _legGrid.Columns.Add("Contract", "Contract");
        _legGrid.Columns.Add("Right", "Right"); _legGrid.Columns.Add("Strike", "Strike");
        _legGrid.Columns.Add("Bid", "Bid"); _legGrid.Columns.Add("Ask", "Ask");
        _legGrid.Columns.Add("Quantity", "Quantity"); _legGrid.Columns.Add("Review", "Validation / Review");
        var stagingActions = new FlowLayoutPanel { Name = "stagingActions", Dock = DockStyle.Bottom, Height = 42, FlowDirection = FlowDirection.RightToLeft, BackColor = Color.FromArgb(32, 32, 32) };
        _algorithmSelector = Selector("algorithmSelector", capabilities.Algorithms.Select(x => x.ToString()).ToArray(), AppBrokerAlgorithm.None.ToString());
        _orderTypeSelector = Selector("orderTypeSelector", capabilities.OrderTypes.Select(x => x.ToString()).ToArray(), AppBrokerOrderType.Limit.ToString());
        stagingActions.Controls.Add(ActionButton("submitClosing", "Submit Closing Order", (_, _) => SubmitClosingRequested?.Invoke(this, EventArgs.Empty), _readOnly));
        stagingActions.Controls.Add(ActionButton("submitOpening", "Submit Opening Order", (_, _) => SubmitOpeningRequested?.Invoke(this, EventArgs.Empty), _readOnly));
        stagingActions.Controls.Add(_orderTypeSelector); stagingActions.Controls.Add(HeaderLabel("Order Type"));
        stagingActions.Controls.Add(_algorithmSelector); stagingActions.Controls.Add(HeaderLabel("Algorithm"));
        stagingActions.Controls.Add(ActionButton("recalculate", "Recalculate / Validate", (_, _) => RecalculateRequested?.Invoke(this, EventArgs.Empty), _readOnly));
        stagingActions.Controls.Add(ActionButton("clearStaged", "Clear Staged Legs", (_, _) => BindStaging(null), _readOnly));
        staging.Controls.Add(_legGrid); staging.Controls.Add(stagingActions);

        var orderActions = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 42, FlowDirection = FlowDirection.RightToLeft, BackColor = Color.FromArgb(32, 32, 32) };
        orderActions.Controls.Add(ActionButton("endOfDay", "End Of Day...", (_, _) => EndOfDayRequested?.Invoke(this, EventArgs.Empty), _readOnly));
        orderActions.Controls.Add(new Label { AutoSize = true, ForeColor = Color.Silver, Padding = new Padding(8, 9, 8, 0), Text = "Cancel/replace is enabled only by revision-bound application workflow capability." });
        var tradeOrderId = new TradeOrderId(portfolioId, fund.FundId, order.OrderId);
        _evidence = new BrokerExecutionEvidenceControl(appRoot, tradeOrderId) { Name = "ordersAndFillsEvidence" };
        orders.Controls.Add(_evidence); orders.Controls.Add(orderActions);
        Controls.Add(tabs); Controls.Add(header);

        // A committed Trade identity and all automated/historical evidence are immutable in this control.
        SetSelectorsReadOnly(_readOnly);
        BindTradeContracts(trade);
    }

    public bool IsReadOnly => !_strategySelector.Enabled && !_directionSelector.Enabled && !_brokerModeSelector.Enabled;
    public TradeBlotterStagingResult? Staging => _staging;
    public VolatilityContextHistoryControl VolatilityContext => _volatilityContext;

    public Task BindVolatilityContextAsync(IOptionVolatilityQueryApi query, VolatilityContextLoadRequest request,
        CancellationToken cancellationToken = default) =>
        _volatilityContext.LoadAsync(query, request, cancellationToken);

    public void BindSnapshot(MarketCompositionSnapshot snapshot, TradeBlotterStagingRequest request, string sourceLabel)
    {
        _sourceLabel.Text = $"{sourceLabel}; As-of {snapshot.EvaluatedAtUtc:O}; Generation {snapshot.GenerationId:N}";
        BindStaging(TradeBlotterLegStager.Stage(snapshot, request));
    }

    public void BindStaging(TradeBlotterStagingResult? staging)
    {
        _staging = staging;
        _marketGrid.RowCount = staging?.Legs.Length ?? 0;
        _legGrid.Rows.Clear();
        if (staging is null) { _marketGrid.Invalidate(); return; }
        foreach (var leg in staging.Legs)
        {
            var index = _legGrid.Rows.Add(leg.LegLabel, leg.DeltaLabel, leg.SideLabel, leg.ContractId,
                leg.Right?.ToString() ?? "Future", leg.Strike?.ToString("0.########"), leg.Bid, leg.Ask,
                leg.SignedQuantity, leg.ManualReviewRequired ? leg.ReviewReason : "Qualified");
            _legGrid.Rows[index].DefaultCellStyle.BackColor = leg.PositionIntent == TradeBlotterPositionIntent.Short ? ShortColor : LongColor;
            _legGrid.Rows[index].DefaultCellStyle.ForeColor = Color.White;
            _legGrid.Rows[index].Tag = leg.StagedLegId;
        }
        _marketGrid.Invalidate();
        if (staging is not null)
            SetSelectorsReadOnly(true);
    }

    private void SetSelectorsReadOnly(bool readOnly)
    {
        SetSelectorReadOnly(_strategySelector, readOnly);
        SetSelectorReadOnly(_directionSelector, readOnly);
        SetSelectorReadOnly(_brokerModeSelector, readOnly);
        SetSelectorReadOnly(_algorithmSelector, readOnly);
        SetSelectorReadOnly(_orderTypeSelector, readOnly);
    }

    private void BindTradeContracts(FundOrderTradeReadModel trade)
    {
        var contracts = trade.GetContractIds();
        _sourceLabel.Text += $"; Trade {trade.TradeId}; State {trade.TradeState}; Contracts {string.Join(", ", contracts)}";
    }

    private void MarketCellValueNeeded(object? sender, DataGridViewCellValueEventArgs e)
    {
        if (_staging is null || e.RowIndex < 0 || e.RowIndex >= _staging.Legs.Length) return;
        var leg = _staging.Legs[e.RowIndex];
        e.Value = e.ColumnIndex switch
        {
            0 => leg.ContractId, 1 => leg.Right?.ToString() ?? "Future", 2 => leg.Strike,
            3 => leg.Bid, 4 => leg.Ask, 5 => leg.DeltaLabel,
            6 => $"{leg.QuoteAtUtc:O} / {_staging.SnapshotDigest[..Math.Min(12, _staging.SnapshotDigest.Length)]}", _ => null
        };
    }

    private static DataGridView Grid(string name) => new()
    {
        Name = name, Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false,
        AllowUserToDeleteRows = false, AllowUserToOrderColumns = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        BackgroundColor = Color.Black, ForeColor = Color.White, GridColor = Color.FromArgb(70, 70, 70),
        RowHeadersVisible = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        EnableHeadersVisualStyles = false, ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing
    };

    private static Label HeaderLabel(string text) => new() { AutoSize = true, ForeColor = Color.White, Text = text, Padding = new Padding(4, 4, 4, 0) };
    private static ComboBox Selector(string name, string[] items, string selected)
    {
        var result = new ComboBox { Name = name, DropDownStyle = ComboBoxStyle.DropDownList, BackColor = Color.Black, ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        result.Items.AddRange(items); result.SelectedItem = selected;
        if (result.SelectedIndex < 0 && result.Items.Count > 0) result.SelectedIndex = 0;
        return result;
    }
    private static void SetSelectorReadOnly(ComboBox selector, bool readOnly = true) => selector.Enabled = !readOnly;
    private static Button ActionButton(string name, string text, EventHandler handler, bool readOnly)
    {
        var button = new Button { Name = name, Text = text, AutoSize = true, Enabled = !readOnly, ForeColor = Color.Black, Margin = new Padding(4, 6, 4, 4) };
        button.Click += handler; return button;
    }

    public DateOnly MaturityDate => RequiredWorkflow().MaturityDate;
    public Task RemoveTradeAsync(int fundId, int orderId, int tradeId) =>
        RequiredWorkflow().RemoveTradeAsync(fundId, orderId, tradeId);
    public Task<Guid> SubmitOrderAsync(DateOnly tradeDate, OrderActionType orderAction,
        ITradeOrderConfirmationService tradeOrderConfirmation)
    {
        if (_readOnly)
            throw new InvalidOperationException("This trade blotter is read-only.");
        ApplyExecutionSelection();
        return RequiredWorkflow().SubmitOrderAsync(tradeDate, orderAction, tradeOrderConfirmation);
    }
    public Task SetLiveFeedAsync(bool enabled) => RequiredWorkflow().SetLiveFeedAsync(enabled);
    public void SetNearestStrikePrices() => RequiredWorkflow().SetNearestStrikePrices();
    public Task OrderActionTypeChangedAsync(OrderActionType orderActionType) =>
        RequiredWorkflow().OrderActionTypeChangedAsync(orderActionType);

    private void ApplyExecutionSelection()
    {
        if (!Enum.TryParse<AppBrokerOrderType>(_orderTypeSelector.SelectedItem?.ToString(), out var orderType) ||
            !Enum.TryParse<AppBrokerAlgorithm>(_algorithmSelector.SelectedItem?.ToString(), out var algorithm) ||
            !_capabilities.OrderTypes.Contains(orderType) || !_capabilities.Algorithms.Contains(algorithm))
            throw new InvalidOperationException("The selected order type or algorithm is not supported by this broker.");
        if (_workflow is not ITradeExecutionSelectionControl selectionControl)
        {
            if (orderType != AppBrokerOrderType.Limit || algorithm != AppBrokerAlgorithm.None)
                throw new InvalidOperationException("This strategy workflow does not support the selected execution settings.");
            return;
        }
        selectionControl.SetExecutionSelection(
            orderType == AppBrokerOrderType.Market
                ? TomasAI.IFM.Domain.Trade.Shared.BrokerOrderType.Market
                : TomasAI.IFM.Domain.Trade.Shared.BrokerOrderType.Limit,
            algorithm == AppBrokerAlgorithm.Adaptive
                ? TomasAI.IFM.Domain.Trade.Shared.BrokerAlgorithm.Adaptive
                : TomasAI.IFM.Domain.Trade.Shared.BrokerAlgorithm.None);
    }

    private ITradeOrderControl RequiredWorkflow() => _workflow ??
        throw new InvalidOperationException("No strategy workflow editor is hosted by this read-only blotter.");

    public void Open()
    {
        if (_workflowControl is IFormControl formControl) formControl.Open();
        _ = _evidence.RefreshAsync();
    }
    public Task RefreshAsync() => _evidence.RefreshAsync();
    public void Close() => _ = CloseAsync();
    public async ValueTask CloseAsync()
    {
        if (_workflowControl is IAsyncFormControl asyncControl)
            await asyncControl.CloseAsync();
        else if (_workflowControl is IFormControl formControl)
            formControl.Close();
    }
    void IFormControl.Resize(Control parentControl) => Bounds = parentControl.ClientRectangle;
}

/// <summary>Compatibility name retained for existing factory and automation callers.</summary>
public sealed class BrokerTradeBlotterView : EsTradeBlotterControl
{
    public BrokerTradeBlotterView(IAppRoot appRoot, FundReadModel fund, FundOrderReadModel order,
        FundOrderTradeReadModel trade, int portfolioId, bool historicalReadOnly,
        Control? workflowControl = null, BrokerCapabilities? capabilities = null)
        : base(appRoot, fund, order, trade, portfolioId, historicalReadOnly, capabilities, workflowControl)
    {
        Name = trade.TradeType == TradeType.FuturesOutright ? "FuturesView" : "VerticalSpreadView";
    }
}
