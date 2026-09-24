using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Application.TradeBroker.Contracts;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.UI.Net.Models.Portfolio;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Views.Presentation;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;
using TomasAI.IFM.Domain.MarketData.Shared.Queries;
using TomasAI.IFM.Shared.EventSourcing;
using AppBrokerAlgorithm = TomasAI.IFM.Application.TradeBroker.Contracts.BrokerAlgorithm;
using AppBrokerEnvironment = TomasAI.IFM.Application.TradeBroker.Contracts.BrokerEnvironment;
using AppBrokerOrderType = TomasAI.IFM.Application.TradeBroker.Contracts.BrokerOrderType;

namespace TomasAI.IFM.UI.Net.Views.Trade;

/// <summary>Trade blotter with Market Selection and a preview-only iron-condor Broker Order/Fills tab.</summary>
public class EsTradeBlotterControl : DarkTradingView, ITradeOrderControl, IAsyncFormControl
{
    public const int VisibleChainRowCapacity = 14;
    private const double DefaultOuterDelta = 0.16;
    private const decimal DefaultWingWidth = 50m;
    private static readonly Color ShortColor = Color.FromArgb(110, 24, 30);
    private static readonly Color LongColor = Color.FromArgb(20, 54, 105);
    private readonly BrokerExecutionEvidenceControl _evidence;
    private readonly DataGridView _marketGrid;
    private readonly DataGridView _selectedLegGrid;
    private readonly DataGridView _legGrid;
    private readonly ComboBox _strategySelector;
    private readonly Label _directionValue;
    private readonly Label _brokerModeValue;
    private readonly ComboBox _algorithmSelector;
    private readonly ComboBox _orderTypeSelector;
    private readonly Label _strategyValue;
    private readonly Label _sourceLabel;
    private readonly Label _marketContextLabel;
    private readonly Label _marketSelectionLabel;
    private readonly Label _lastPriceValue;
    private readonly Label _changeValue;
    private readonly Label _ivValue;
    private readonly Label _ivRankValue;
    private readonly Label _dteValue;
    private readonly Label _multiplierValue;
    private readonly Label _expectedMoveValue;
    private readonly Label _quoteAgeValue;
    private readonly Label _tickValue;
    private readonly Label _settlementValue;
    private readonly ComboBox _expirationSelector;
    private readonly ComboBox _presetSelector;
    private readonly ComboBox _wingSelector;
    private readonly ComboBox _liquiditySelector;
    private readonly TabPage _stagingTab;
    private readonly Control? _workflowControl;
    private readonly ITradeOrderControl? _workflow;
    private readonly BrokerCapabilities _capabilities;
    private readonly bool _readOnly;
    private readonly TradeType _tradeType;
    private readonly VolatilityContextHistoryControl _volatilityContext;
    private readonly TradeBlotterStrategy _strategy;
    private readonly IAppRoot _appRoot;
    private readonly List<OptionChainDisplayRow> _optionChainRows = [];
    private readonly List<OptionChainDisplayRow> _selectedLegRows = [];
    private decimal? _nearestUnderlyingStrike;
    private readonly HashSet<string> _selectedMarketContracts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _selectedMarketRoles = new(StringComparer.Ordinal);
    private readonly Dictionary<DateOnly, string[]> _providerRootsByExpiry = new();
    private string[] _selectedMarketContractIds = [];
    private FuturesOptionContractReadModel[] _availableOptionContracts = [];
    private OptionContractExpiryReadModel[] _availableExpiryRows = [];
    private string _underlyingSymbol = string.Empty;
    private TradeBlotterStagingResult? _staging;
    private DateOnly _tradeDate;
    private DateOnly _requestedMaturityDate;
    private bool _optionDefinitionsLoaded;
    private int _optionChainLoadVersion;
    private string _underlyingContractId = string.Empty;
    private bool _liveFeedEnabled;
    private bool _chainRefreshInProgress;
    private CancellationTokenSource? _chainRequestCancellation;
    private decimal? _standardDeviationAmount;
    private DateOnly? _activeEvaluatedExpiry;
    private DateOnly? _displayedEvaluatedExpiry;
    private DateOnly? _defaultSelectionExpiry;
    private bool _marketSelectionEditedManually;
    private readonly System.Windows.Forms.Timer _chainRefreshTimer = new() { Interval = 1000 };

    public event EventHandler? SubmitOpeningRequested;
    public event EventHandler? SubmitClosingRequested;
    public event EventHandler? EndOfDayRequested;
    public event EventHandler? RecalculateRequested;

    /// <summary>Initializes a canonical Portfolio Fund trade blotter control.</summary>
    /// <param name="appRoot">The application service root.</param>
    /// <param name="fund">The selected canonical Fund.</param>
    /// <param name="order">The selected canonical order.</param>
    /// <param name="trade">The selected canonical trade.</param>
    /// <param name="valueDate">The optional historical value date.</param>
    /// <param name="baseContracts">The available futures contracts.</param>
    public EsTradeBlotterControl(IAppRoot appRoot, PortfolioFundEditorModel fund, PortfolioFundOrderEditorModel order,
        PortfolioFundOrderTradeEditorModel trade, int portfolioId, bool historicalReadOnly,
        BrokerCapabilities? capabilities = null, Control? workflowControl = null)
    {
        ArgumentNullException.ThrowIfNull(appRoot);
        _appRoot = appRoot;
        _tradeType = trade.TradeType;
        Name = "esTradeBlotter";
        AccessibleName = "ES trade blotter";
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
        _strategy = strategy switch
        {
            "Iron Condor" => TradeBlotterStrategy.IronCondor,
            "Futures Outright" => TradeBlotterStrategy.FuturesOutright,
            _ => TradeBlotterStrategy.VerticalSpread
        };
        capabilities ??= BrokerCapabilities.Emulator("IFM-EMULATOR-PAPER");
        _capabilities = capabilities;
        _workflowControl = workflowControl;
        _workflow = workflowControl as ITradeOrderControl;
        _readOnly = historicalReadOnly || trade.TradeState != TradeState.NewTrade || _workflow is null;

        var header = new TableLayoutPanel
        {
            Name = "tradeBlotterHeader", Dock = DockStyle.Fill, ColumnCount = 7,
            BackColor = Color.FromArgb(32, 32, 32), Padding = new Padding(6, 5, 6, 3)
        };
        var directionWidth = Enum.GetNames<TradeBlotterDirection>()
            .Select(value => TextRenderer.MeasureText(value, Font).Width).Max() + 24;
        var brokerModeWidth = Enum.GetNames<AppBrokerEnvironment>()
            .Select(value => TextRenderer.MeasureText(value, Font).Width).Max() + 12;
        header.ColumnStyles.Add(new(SizeType.AutoSize)); header.ColumnStyles.Add(new(SizeType.Absolute, 170));
        header.ColumnStyles.Add(new(SizeType.Absolute, 16));
        header.ColumnStyles.Add(new(SizeType.AutoSize)); header.ColumnStyles.Add(new(SizeType.Absolute, directionWidth));
        header.ColumnStyles.Add(new(SizeType.AutoSize)); header.ColumnStyles.Add(new(SizeType.Absolute, brokerModeWidth));
        header.ColumnStyles.Add(new(SizeType.Percent, 100));
        _strategySelector = Selector("strategySelector", ["Iron Condor", "Vertical Spread", "Futures Outright"], strategy);
        _strategySelector.Visible = false;
        _strategyValue = new Label
        {
            Name = "tradeStrategyValue", Text = strategy, Dock = DockStyle.Fill,
            ForeColor = Color.White, BackColor = Color.Black, BorderStyle = BorderStyle.FixedSingle,
            TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(6, 0, 4, 0)
        };
        _directionValue = ReadOnlyValue("directionValue", direction);
        _brokerModeValue = ReadOnlyValue("brokerModeValue", capabilities.Environment.ToString());
        header.Controls.Add(HeaderLabel("Trade Strategy"), 0, 0); header.Controls.Add(_strategyValue, 1, 0);
        header.Controls.Add(HeaderLabel("Direction"), 3, 0); header.Controls.Add(_directionValue, 4, 0);
        header.Controls.Add(HeaderLabel("Broker Mode"), 5, 0); header.Controls.Add(_brokerModeValue, 6, 0);
        _sourceLabel = HeaderLabel(historicalReadOnly ? "Historical read-only" : "Manual submitted / evidence");
        _sourceLabel.Name = "sourceModeLabel";
        _sourceLabel.AutoSize = false;
        _sourceLabel.Dock = DockStyle.Fill;
        _sourceLabel.TextAlign = ContentAlignment.MiddleLeft;
        _sourceLabel.AutoEllipsis = true;

        var tabs = new TomasAI.IFM.UI.Net.Views.App.DarkTabControl
        {
            Name = "tradeBlotterTabs", Dock = DockStyle.Fill, Appearance = TabAppearance.Normal
        };
        var market = new TabPage("Market Selection") { Name = "marketSelectionTab", BackColor = Color.Black, ForeColor = Color.White };
        _stagingTab = new TabPage("Leg Staging") { Name = "legStagingTab", BackColor = Color.Black, ForeColor = Color.White };
        var orders = new TabPage("Orders and Fills") { Name = "ordersAndFillsTab", BackColor = Color.Black, ForeColor = Color.White };
        if (_strategy == TradeBlotterStrategy.IronCondor)
        {
            var brokerPreview = new TabPage("Broker Order/Fills")
            {
                Name = "brokerOrderFillsTab", BackColor = Color.Black, ForeColor = Color.White
            };
            brokerPreview.Controls.Add(new BrokerOrderFillsPreviewControl(portfolioId, fund, order, trade));
            tabs.TabPages.AddRange([market, brokerPreview]);
        }
        else
            tabs.TabPages.AddRange([market, _stagingTab, orders]);
        _volatilityContext = new VolatilityContextHistoryControl();

        _marketGrid = Grid("marketSelectionGrid");
        _marketGrid.VirtualMode = true;
        foreach (var (name, text) in new[]
                 {
                     ("CallSelected", "Selected"), ("CallDelta", "Delta"), ("CallOi", "OI"), ("CallVolume", "Vol"),
                     ("CallBid", "Bid"), ("CallAsk", "Ask"), ("Strike", "Strike"),
                     ("PutBid", "Bid"), ("PutAsk", "Ask"), ("PutVolume", "Vol"),
                     ("PutOi", "OI"), ("PutDelta", "Delta"), ("PutSelected", "Selected")
                 })
            _marketGrid.Columns.Add(name, text);
        _marketGrid.Columns["Strike"]!.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        _marketGrid.Columns["Strike"]!.DefaultCellStyle.ForeColor = Color.White;
        _marketGrid.Columns["Strike"]!.DefaultCellStyle.Font = new Font(_marketGrid.Font, FontStyle.Bold);
        _marketGrid.SelectionMode = DataGridViewSelectionMode.CellSelect;
        _marketGrid.CellValueNeeded += MarketCellValueNeeded;
        _marketGrid.CellClick += MarketGridCellClick;
        _marketGrid.CellFormatting += MarketGridCellFormatting;
        _marketGrid.RowPostPaint += MarketGridRowPostPaint;
        _selectedLegGrid = Grid("selectedStrategyLegsGrid");
        _selectedLegGrid.VirtualMode = true;
        _selectedLegGrid.ColumnHeadersVisible = false;
        _selectedLegGrid.ScrollBars = ScrollBars.None;
        _selectedLegGrid.SelectionMode = DataGridViewSelectionMode.CellSelect;
        foreach (DataGridViewColumn column in _marketGrid.Columns)
            _selectedLegGrid.Columns.Add(column.Name, column.HeaderText);
        _selectedLegGrid.Columns["Strike"]!.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        _selectedLegGrid.Columns["Strike"]!.DefaultCellStyle.Font = new Font(_selectedLegGrid.Font, FontStyle.Bold);
        _selectedLegGrid.CellValueNeeded += SelectedLegCellValueNeeded;
        _selectedLegGrid.CellFormatting += SelectedLegCellFormatting;
        _marketContextLabel = MarketValueLabel("-");
        _lastPriceValue = MarketValueLabel("5,420.50");
        _changeValue = MarketValueLabel("-");
        _ivValue = MarketValueLabel("-");
        _ivRankValue = MarketValueLabel("-");
        _dteValue = MarketValueLabel("-");
        _multiplierValue = MarketValueLabel("$50");
        _expectedMoveValue = MarketValueLabel("-");
        _quoteAgeValue = MarketValueLabel("120 ms");
        _tickValue = MarketValueLabel("0.25");
        _settlementValue = MarketValueLabel("AM");
        _expirationSelector = Selector("expirationSelector", [], "");
        _expirationSelector.Width = 130;
        _expirationSelector.SelectedIndexChanged += ExpirationSelectorSelectedIndexChanged;
        _presetSelector = Selector("presetSelector", ["16 Delta Condor"], "16 Delta Condor");
        _wingSelector = Selector("wingSelector", ["50"], "50");
        _liquiditySelector = Selector("liquiditySelector", ["Not evaluated"], "Not evaluated");
        _marketSelectionLabel = MarketValueLabel(
            $"Selected legs: 0 / {MaximumSelectedLegs}     Click Call or Put side to stage a leg");
        _marketSelectionLabel.Name = "marketSelectionStatus";
        var marketLayout = BuildMarketSelectionLayout();
        SeedOptionChainPreview();
        market.Controls.Add(marketLayout);

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
        _stagingTab.Controls.Add(_legGrid); _stagingTab.Controls.Add(stagingActions);

        var orderActions = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 42, FlowDirection = FlowDirection.RightToLeft, BackColor = Color.FromArgb(32, 32, 32) };
        orderActions.Controls.Add(ActionButton("endOfDay", "End Of Day...", (_, _) => EndOfDayRequested?.Invoke(this, EventArgs.Empty), _readOnly));
        orderActions.Controls.Add(new Label { AutoSize = true, ForeColor = Color.Silver, Padding = new Padding(8, 9, 8, 0), Text = "Cancel/replace is enabled only by revision-bound application workflow capability." });
        var tradeOrderId = new TradeOrderId(portfolioId, fund.FundId, order.OrderId);
        _evidence = new BrokerExecutionEvidenceControl(appRoot, tradeOrderId) { Name = "ordersAndFillsEvidence" };
        orders.Controls.Add(_evidence); orders.Controls.Add(orderActions);
        var separator = new DarkHeaderSeparator
        {
            Name = "tradeBlotterTabSeparator",
            Margin = new Padding(4, 0, 4, 0)
        };
        var blotterLayout = new TableLayoutPanel
        {
            Name = "threeTabTradeBlotterLayout", Dock = DockStyle.Fill,
            ColumnCount = 1, RowCount = 4, BackColor = Color.Black,
            Margin = new Padding(0), Padding = new Padding(4, 4, 4, 1)
        };
        blotterLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        blotterLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        blotterLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        blotterLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 3));
        blotterLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _sourceLabel.Padding = new Padding(8, 3, 8, 0);
        blotterLayout.Controls.Add(header, 0, 0);
        blotterLayout.Controls.Add(_sourceLabel, 0, 1);
        blotterLayout.Controls.Add(separator, 0, 2);
        blotterLayout.Controls.Add(tabs, 0, 3);
        Controls.Add(blotterLayout);

        // A committed Trade identity and all automated/historical evidence are immutable in this control.
        SetSelectorsReadOnly(_readOnly);
        BindTradeContracts(trade);
        _underlyingContractId = trade.BaseContractId?.Trim() ?? string.Empty;
        _chainRefreshTimer.Tick += async (_, _) => await RefreshEvaluatedChainAsync();
        HandleCreated += async (_, _) => await LoadOptionDefinitionsAsync(appRoot, trade);
    }

    public bool IsReadOnly => _readOnly;
    public TradeBlotterStagingResult? Staging => _staging;
    public VolatilityContextHistoryControl VolatilityContext => _volatilityContext;
    private int MaximumSelectedLegs => _strategy switch
    {
        TradeBlotterStrategy.IronCondor => 4,
        TradeBlotterStrategy.VerticalSpread => 2,
        _ => 1
    };

    public Task BindVolatilityContextAsync(IOptionVolatilityQueryApi query, VolatilityContextLoadRequest request,
        CancellationToken cancellationToken = default) =>
        _volatilityContext.LoadAsync(query, request, cancellationToken);

    public void BindSnapshot(MarketCompositionSnapshot snapshot, TradeBlotterStagingRequest request, string sourceLabel)
    {
        _sourceLabel.Text = $"{sourceLabel}; As-of {snapshot.EvaluatedAtUtc:O}; Generation {snapshot.GenerationId:N}";
        BindOptionChain(snapshot);
        BindStaging(TradeBlotterLegStager.Stage(snapshot, request));
    }

    public void BindStaging(TradeBlotterStagingResult? staging)
    {
        _staging = staging;
        _selectedMarketContracts.Clear();
        _selectedMarketRoles.Clear();
        _marketSelectionEditedManually = staging is not null;
        if (staging is not null)
            foreach (var leg in staging.Legs)
                _selectedMarketContracts.Add(leg.ContractId);
        UpdateMarketSelectionStatus();
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

    private TableLayoutPanel BuildMarketSelectionLayout()
    {
        var layout = new TableLayoutPanel
        {
            Name = "marketSelectionLayout", Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5,
            BackColor = Color.Black, Padding = new Padding(0)
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 96));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 96));

        var information = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3,
            BackColor = Color.FromArgb(20, 20, 20), Padding = new Padding(5, 3, 5, 2)
        };
        information.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        information.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        information.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        var primary = MarketInformationRow();
        AddMarketMetric(primary, "EXPIRY:", _expirationSelector, 130);
        AddMarketMetric(primary, "DTE:", _dteValue, 42);
        AddMarketMetric(primary, "UNDERLYING:", _marketContextLabel, 92);
        AddMarketMetric(primary, "MULTIPLIER:", _multiplierValue, 62);
        AddMarketMetric(primary, "TICK:", _tickValue, 52);
        AddMarketMetric(primary, "SETTLEMENT:", _settlementValue, 52);
        var secondary = MarketInformationRow();
        AddMarketMetric(secondary, "EXPECTED MOVE:", _expectedMoveValue, 82);
        AddMarketMetric(secondary, "LAST:", _lastPriceValue, 90);
        AddMarketMetric(secondary, "CHG:", _changeValue, 145);
        AddMarketMetric(secondary, "IV:", _ivValue, 62);
        AddMarketMetric(secondary, "IV RANK:", _ivRankValue, 50);
        AddMarketMetric(secondary, "QUOTE AGE:", _quoteAgeValue, 74);
        var tertiary = MarketInformationRow();
        AddMarketMetric(tertiary, "PRESET:", _presetSelector, 170);
        AddMarketMetric(tertiary, "WING:", _wingSelector, 72);
        AddMarketMetric(tertiary, "LIQUIDITY:", _liquiditySelector, 115);
        information.Controls.Add(primary, 0, 0);
        information.Controls.Add(secondary, 0, 1);
        information.Controls.Add(tertiary, 0, 2);

        layout.Controls.Add(information, 0, 0);
        layout.Controls.Add(BuildOptionSideHeader(), 0, 1);
        layout.Controls.Add(_marketGrid, 0, 2);
        _marketSelectionLabel.Dock = DockStyle.Fill;
        _marketSelectionLabel.Padding = new Padding(8, 0, 0, 0);
        layout.Controls.Add(_marketSelectionLabel, 0, 3);
        layout.Controls.Add(_selectedLegGrid, 0, 4);
        return layout;
    }

    private static FlowLayoutPanel MarketInformationRow() => new()
    {
        Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false,
        BackColor = Color.FromArgb(20, 20, 20), Margin = new Padding(0)
    };

    private static void AddMarketMetric(FlowLayoutPanel row, string caption, Control value, int width)
    {
        row.Controls.Add(new Label
        {
            Text = caption, AutoSize = true, ForeColor = Color.Silver,
            TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(7, 5, 3, 0)
        });
        value.Width = width;
        value.Height = 25;
        value.Margin = new Padding(0, 1, 10, 1);
        row.Controls.Add(value);
    }

    private static TableLayoutPanel BuildOptionSideHeader()
    {
        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 13, RowCount = 1,
            BackColor = Color.FromArgb(38, 38, 38), Margin = new Padding(0)
        };
        for (var index = 0; index < 13; index++)
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 13f));
        var calls = MarketValueLabel("CALLS");
        calls.Dock = DockStyle.Fill;
        calls.TextAlign = ContentAlignment.MiddleCenter;
        var strike = MarketValueLabel("STRIKE");
        strike.Dock = DockStyle.Fill;
        strike.TextAlign = ContentAlignment.MiddleCenter;
        var puts = MarketValueLabel("PUTS");
        puts.Dock = DockStyle.Fill;
        puts.TextAlign = ContentAlignment.MiddleCenter;
        header.Controls.Add(calls, 0, 0); header.SetColumnSpan(calls, 6);
        header.Controls.Add(strike, 6, 0);
        header.Controls.Add(puts, 7, 0); header.SetColumnSpan(puts, 6);
        return header;
    }

    private static Label MarketValueLabel(string text) => new()
    {
        Text = text, ForeColor = Color.White, BackColor = Color.Black,
        BorderStyle = BorderStyle.FixedSingle, TextAlign = ContentAlignment.MiddleLeft,
        AutoEllipsis = true, Margin = new Padding(0)
    };

    private void SeedOptionChainPreview()
    {
        _optionChainRows.Clear();
        _optionChainRows.AddRange(
        [
            OptionChainDisplayRow.Preview(5950m, "", "0.02", "1.2K", "310", "1.25", "1.75",
                "-0.98", "95", "12", "528.00", "531.00", ""),
            OptionChainDisplayRow.Preview(5900m, "", "0.03", "1.8K", "460", "2.00", "2.50",
                "-0.97", "130", "20", "479.00", "482.00", ""),
            OptionChainDisplayRow.Preview(5850m, "", "0.04", "2.4K", "620", "3.00", "3.50",
                "-0.96", "175", "31", "431.00", "434.00", ""),
            OptionChainDisplayRow.Preview(5800m, "", "0.05", "3.0K", "810", "4.25", "4.75",
                "-0.95", "220", "44", "383.00", "386.00", ""),
            OptionChainDisplayRow.Preview(5750m, "", "0.06", "3.8K", "1.0K", "5.75", "6.25",
                "-0.94", "275", "55", "335.00", "338.00", ""),
            OptionChainDisplayRow.Preview(5700m, "", "0.07", "4.6K", "1.3K", "7.25", "7.75",
                "-0.93", "320", "66", "287.00", "290.00", ""),
            OptionChainDisplayRow.Preview(5650m, "", "0.08", "5.3K", "1.7K", "9.00", "9.50",
                "-0.92", "370", "77", "239.00", "242.00", ""),
            OptionChainDisplayRow.Preview(5600m, "+LC", "0.10", "6.1K", "2.1K", "11.00", "11.50",
                "-0.90", "420", "88", "69.00", "70.50", ""),
            OptionChainDisplayRow.Preview(5550m, "-SC", "0.16", "12.4K", "5.6K", "19.50", "20.00",
                "-0.84", "1.4K", "510", "41.00", "42.50", ""),
            OptionChainDisplayRow.Preview(5500m, "", "0.22", "14.5K", "8.9K", "28.50", "29.00",
                "-0.78", "2.3K", "880", "21.25", "22.25", ""),
            OptionChainDisplayRow.Marker("UPPER EXPECTED MOVE  5,465.50"),
            OptionChainDisplayRow.Preview(5450m, "", "0.35", "19.0K", "12.1K", "44.00", "44.75",
                "-0.65", "4.1K", "1.1K", "8.50", "9.25", ""),
            OptionChainDisplayRow.Marker("LAST UNDERLYING PRICE  5,420.50"),
            OptionChainDisplayRow.Preview(5400m, "", "0.55", "22.1K", "15.0K", "65.25", "66.00",
                "-0.45", "14.2K", "9.2K", "3.10", "3.50", ""),
            OptionChainDisplayRow.Marker("LOWER EXPECTED MOVE  5,375.50"),
            OptionChainDisplayRow.Preview(5350m, "", "0.72", "16.4K", "9.5K", "94.00", "95.25",
                "-0.28", "21.0K", "13.8K", "8.75", "9.25", ""),
            OptionChainDisplayRow.Preview(5300m, "", "0.84", "8.9K", "4.2K", "124.50", "126.00",
                "-0.16", "19.5K", "11.2K", "22.00", "22.50", "-SP"),
            OptionChainDisplayRow.Preview(5250m, "", "0.90", "4.0K", "1.1K", "159.00", "161.00",
                "-0.10", "12.1K", "6.4K", "14.25", "14.75", "+LP"),
            OptionChainDisplayRow.Preview(5200m, "", "0.93", "3.6K", "980", "197.00", "199.00",
                "-0.07", "9.8K", "4.9K", "9.50", "10.00", ""),
            OptionChainDisplayRow.Preview(5150m, "", "0.95", "3.1K", "820", "238.00", "241.00",
                "-0.05", "7.4K", "3.4K", "6.25", "6.75", ""),
            OptionChainDisplayRow.Preview(5100m, "", "0.96", "2.7K", "690", "281.00", "284.00",
                "-0.04", "5.8K", "2.5K", "4.00", "4.50", ""),
            OptionChainDisplayRow.Preview(5050m, "", "0.97", "2.2K", "540", "326.00", "329.00",
                "-0.03", "4.4K", "1.8K", "2.75", "3.25", ""),
            OptionChainDisplayRow.Preview(5000m, "", "0.98", "1.8K", "420", "372.00", "375.00",
                "-0.02", "3.2K", "1.2K", "1.75", "2.25", ""),
            OptionChainDisplayRow.Preview(4950m, "", "0.98", "1.4K", "330", "419.00", "422.00",
                "-0.02", "2.3K", "850", "1.25", "1.75", ""),
            OptionChainDisplayRow.Preview(4900m, "", "0.99", "1.1K", "250", "467.00", "470.00",
                "-0.01", "1.7K", "610", "0.75", "1.25", "")
        ]);
        _marketGrid.RowCount = _optionChainRows.Count;
        _nearestUnderlyingStrike = 5400m;
        RefreshSelectedLegRows();
    }

    private void BindOptionChain(MarketCompositionSnapshot snapshot)
    {
        _optionChainRows.Clear();
        foreach (var strikeGroup in snapshot.Instruments
                     .Where(value => value.Instrument.Strike is not null && value.Instrument.IsCall is not null)
                     .GroupBy(value => value.Instrument.Strike!.Value)
                     .OrderByDescending(group => group.Key))
        {
            _optionChainRows.Add(new OptionChainDisplayRow(
                strikeGroup.Key,
                strikeGroup.FirstOrDefault(value => value.Instrument.IsCall == true),
                strikeGroup.FirstOrDefault(value => value.Instrument.IsCall == false)));
        }
        _marketGrid.RowCount = _optionChainRows.Count;
        var underlying = snapshot.Instruments.Select(value => value.Instrument.Underlying)
            .FirstOrDefault(value => value is not null);
        if (underlying is not null)
            _nearestUnderlyingStrike = NearestStrike(_optionChainRows, (underlying.Bid + underlying.Ask) / 2m);
        RefreshSelectedLegRows();
        var midpoint = underlying is null ? "N/A" : ((underlying.Bid + underlying.Ask) / 2m).ToString("0.00");
        _marketContextLabel.Text = snapshot.ScopeId;
        _lastPriceValue.Text = midpoint;
        var latestQuote = snapshot.Instruments
            .Where(value => value.Instrument.Quote is not null)
            .Select(value => value.Instrument.Quote!.EventAtUtc)
            .DefaultIfEmpty(snapshot.EvaluatedAtUtc)
            .Max();
        _quoteAgeValue.Text = $"{Math.Max(0, (snapshot.EvaluatedAtUtc - latestQuote).TotalMilliseconds):0} ms";
        _marketGrid.Invalidate();
    }

    private void SetSelectorsReadOnly(bool readOnly)
    {
        SetSelectorReadOnly(_strategySelector, readOnly);
        SetSelectorReadOnly(_algorithmSelector, readOnly);
        SetSelectorReadOnly(_orderTypeSelector, readOnly);
    }

    private void BindTradeContracts(PortfolioFundOrderTradeEditorModel trade)
    {
        var contracts = trade.GetContractIds();
        _sourceLabel.Text += $"; Trade {trade.TradeId}; State {trade.TradeState}; Contracts {string.Join(", ", contracts)}";
        _marketContextLabel.Text = string.IsNullOrWhiteSpace(trade.BaseContractId)
            ? trade.BaseContractSymbol
            : trade.BaseContractId;
        var requestedMaturity = trade.RequestedMaturityDate ?? trade.RequestedTradeDate;
        _tradeDate = trade.RequestedTradeDate;
        _standardDeviationAmount = null;
        _requestedMaturityDate = requestedMaturity;
        SetExpiryState("Loading Databento expiries...");
    }

    private async Task LoadOptionDefinitionsAsync(IAppRoot appRoot, PortfolioFundOrderTradeEditorModel trade)
    {
        if (_optionDefinitionsLoaded)
            return;
        _optionDefinitionsLoaded = true;
        _underlyingSymbol = ResolveUnderlyingSymbol(trade);
        if (string.IsNullOrWhiteSpace(_underlyingSymbol))
        {
            SetExpiryState("Underlying symbol unavailable");
            return;
        }
        if (!string.IsNullOrWhiteSpace(_underlyingContractId))
        {
            var eod = await appRoot.Services.MarketDataQueries.QueryFuturesEodDataAsync(
                _underlyingContractId, _tradeDate);
            if (eod.Success && eod.Value is { ClosePrice: > 0, DailyStdDevAmount: > 0 } value
                && double.IsFinite(value.DailyStdDevAmount))
            {
                _standardDeviationAmount = (decimal)value.DailyStdDevAmount;
            }
        }
        var result = await appRoot.Services.MarketDataQueries.QueryDatabentoOptionChainRangeAsync(
            _underlyingSymbol,
            _tradeDate,
            _requestedMaturityDate);
        if (!result.Success)
        {
            SetExpiryState("Databento expiry load failed");
            _marketSelectionLabel.Text = $"Databento expiry load failed: {result.ErrorMessage}";
            return;
        }
        _providerRootsByExpiry.Clear();
        _availableExpiryRows = (result.Value ?? [])
            .Where(row => row.ExpiryDate >= _tradeDate)
            .OrderBy(row => row.ExpiryDate)
            .ThenBy(row => row.ProviderRoot, StringComparer.Ordinal)
            .ToArray();
        BindAvailableExpiries(
            _availableExpiryRows.Select(row => row.ExpiryDate),
            _tradeDate,
            _requestedMaturityDate);
    }

    private static string ResolveUnderlyingSymbol(PortfolioFundOrderTradeEditorModel trade)
    {
        if (!string.IsNullOrWhiteSpace(trade.BaseContractId))
        {
            try { return new FuturesContractIdParser(trade.BaseContractId.Trim()).Symbol; }
            catch (InvalidOperationException) { }
            catch (ArgumentException) { }
        }
        if (!string.IsNullOrWhiteSpace(trade.UnderlyingRoot))
            return trade.UnderlyingRoot.Trim();
        return trade.BaseContractSymbol.Trim();
    }

    /// <summary>
    /// Binds the listed expiries shown by Market Selection. Every listed expiry from the trade
    /// date through the requested maturity is retained.
    /// </summary>
    public void BindAvailableExpiries(
        IEnumerable<DateOnly> availableExpiries,
        DateOnly tradeDate,
        DateOnly requestedMaturityDate)
    {
        ArgumentNullException.ThrowIfNull(availableExpiries);
        _tradeDate = tradeDate;
        var available = availableExpiries
            .Where(expiry => expiry >= tradeDate)
            .Distinct()
            .OrderBy(expiry => expiry)
            .ToArray();
        var throughMaturity = available.Where(expiry => expiry <= requestedMaturityDate);
        var nearestAfter = available.Where(expiry => expiry > requestedMaturityDate).Take(1);
        var displayed = throughMaturity.Concat(nearestAfter).ToArray();

        _expirationSelector.BeginUpdate();
        try
        {
            _expirationSelector.Items.Clear();
            foreach (var expiry in displayed)
                _expirationSelector.Items.Add(new ExpiryChoice(expiry));
            if (_expirationSelector.Items.Count > 0)
                _expirationSelector.SelectedIndex = 0;
            else
            {
                _expirationSelector.Items.Add("No Databento expiries in range");
                _expirationSelector.SelectedIndex = 0;
            }
        }
        finally
        {
            _expirationSelector.EndUpdate();
        }
        _expirationSelector.Enabled = displayed.Length > 0;
        _ = UpdateSelectedExpiryDteAsync();
    }

    private void SetExpiryState(string text)
    {
        _expirationSelector.BeginUpdate();
        try
        {
            _expirationSelector.Items.Clear();
            _expirationSelector.Items.Add(text);
            _expirationSelector.SelectedIndex = 0;
        }
        finally
        {
            _expirationSelector.EndUpdate();
        }
        _expirationSelector.Enabled = false;
        _dteValue.Text = "-";
        _availableOptionContracts = [];
        _providerRootsByExpiry.Clear();
        _availableExpiryRows = [];
        _displayedEvaluatedExpiry = null;
        _defaultSelectionExpiry = null;
        _selectedMarketRoles.Clear();
        _selectedMarketContracts.Clear();
        _marketSelectionEditedManually = false;
        _optionChainRows.Clear();
        _marketGrid.RowCount = 0;
    }

    private async void ExpirationSelectorSelectedIndexChanged(object? sender, EventArgs e)
    {
        try { await UpdateSelectedExpiryDteAsync(); }
        catch (OperationCanceledException) { return; }
        catch (Exception error)
        {
            if (!IsDisposed) _marketSelectionLabel.Text = $"Option chain load failed: {error.Message}";
        }
    }

    private async Task UpdateSelectedExpiryDteAsync()
    {
        _dteValue.Text = _expirationSelector.SelectedItem is ExpiryChoice expiry
            ? Math.Max(0, expiry.Value.DayNumber - _tradeDate.DayNumber).ToString()
            : "-";
        if (_expirationSelector.SelectedItem is not ExpiryChoice selected
            || string.IsNullOrWhiteSpace(_underlyingSymbol))
            return;
        if (_displayedEvaluatedExpiry is { } displayed && displayed != selected.Value)
        {
            _displayedEvaluatedExpiry = null;
            _defaultSelectionExpiry = null;
            _selectedMarketRoles.Clear();
            _selectedMarketContracts.Clear();
            _marketSelectionEditedManually = false;
            _optionChainRows.Clear();
            _marketGrid.RowCount = 0;
        }
        ResetChainRequestCancellation();
        var requestToken = _chainRequestCancellation!.Token;
        if (_activeEvaluatedExpiry is { } active && active != selected.Value)
            await ReleaseEvaluatedChainAsync(active);
        var roots = _availableExpiryRows
            .Where(row => row.ExpiryDate == selected.Value)
            .Select(row => row.ProviderRoot)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (roots.Length == 0) return;
        var loadVersion = ++_optionChainLoadVersion;
        _marketSelectionLabel.Text = $"Loading {selected.Value:dd MMM yy} option chain...";
        ServiceResult<FuturesOptionContractReadModel[]>[] results;
        try
        {
            results = await Task.WhenAll(roots.Select(root =>
                _appRoot.Services.MarketDataQueries.QueryDatabentoOptionChainAsync(
                    _underlyingSymbol, root, selected.Value, requestToken)));
        }
        catch (OperationCanceledException) when (requestToken.IsCancellationRequested) { return; }
        if (IsDisposed || loadVersion != _optionChainLoadVersion
            || _expirationSelector.SelectedItem is not ExpiryChoice current
            || current.Value != selected.Value)
            return;
        var failed = results.FirstOrDefault(result => !result.Success);
        if (failed is not null)
        {
            _marketSelectionLabel.Text = $"Option chain load failed: {failed.ErrorMessage}";
            return;
        }
        _availableOptionContracts = results
            .SelectMany(result => result.Value ?? [])
            .DistinctBy(contract => contract.ContractId)
            .ToArray();
        BindSelectedOptionChain();
        if (_liveFeedEnabled)
            await RefreshEvaluatedChainAsync();
    }

    private void BindSelectedOptionChain()
    {
        if (_expirationSelector.SelectedItem is not ExpiryChoice expiry
            || _availableOptionContracts.Length == 0)
            return;
        _optionChainRows.Clear();
        foreach (var strikeGroup in _availableOptionContracts
                     .Where(contract => GetOptionExpiry(contract) == expiry.Value)
                     .GroupBy(contract => (decimal)contract.StrikePrice)
                     .OrderByDescending(group => group.Key))
        {
            var call = strikeGroup.FirstOrDefault(contract =>
                contract.OptionType.StartsWith("C", StringComparison.OrdinalIgnoreCase));
            var put = strikeGroup.FirstOrDefault(contract =>
                contract.OptionType.StartsWith("P", StringComparison.OrdinalIgnoreCase));
            _optionChainRows.Add(OptionChainDisplayRow.Definition(
                strikeGroup.Key,
                call?.ContractId,
                put?.ContractId));
        }
        _selectedMarketContracts.Clear();
        _selectedMarketRoles.Clear();
        _defaultSelectionExpiry = null;
        _marketSelectionEditedManually = false;
        _marketGrid.RowCount = _optionChainRows.Count;
        UpdateMarketSelectionStatus();
    }

    private async Task RefreshEvaluatedChainAsync()
    {
        if (!_liveFeedEnabled || _chainRefreshInProgress
            || string.IsNullOrWhiteSpace(_underlyingContractId)
            || _expirationSelector.SelectedItem is not ExpiryChoice expiry) return;
        _chainRefreshInProgress = true;
        var requestToken = _chainRequestCancellation?.Token ?? CancellationToken.None;
        try
        {
            var selectedExpiry = expiry.Value;
            var result = await _appRoot.Services.MarketDataQueries.QueryEvaluatedOptionChainAsync(
                CreateChainQuery(selectedExpiry), requestToken);
            if (IsDisposed || _expirationSelector.SelectedItem is not ExpiryChoice current
                || current.Value != selectedExpiry) return;
            if (!result.Success || result.Value is null) { SetSelectionText(_liquiditySelector, "Unavailable"); return; }
            _activeEvaluatedExpiry = selectedExpiry;
            BindEvaluatedChain(result.Value);
        }
        catch (OperationCanceledException) when (requestToken.IsCancellationRequested) { return; }
        catch (Exception)
        {
            if (!IsDisposed) SetSelectionText(_liquiditySelector, "Unavailable");
        }
        finally { _chainRefreshInProgress = false; }
    }

    private void ResetChainRequestCancellation()
    {
        _chainRequestCancellation?.Cancel();
        _chainRequestCancellation?.Dispose();
        _chainRequestCancellation = new CancellationTokenSource();
    }

    private GetEvaluatedOptionChainQuery CreateChainQuery(DateOnly expiry) => new()
    {
        UnderlyingContractId = _underlyingContractId, UnderlyingSymbol = _underlyingSymbol,
        ProviderRoots = ProviderRootsFor(expiry),
        ExpiryDate = expiry, StandardDeviationAmount = _standardDeviationAmount,
        StandardDeviationMultiplier = 2.5,
        RequiredContractIds = _selectedMarketContractIds
    };

    private string[] ProviderRootsFor(DateOnly expiry)
    {
        if (_providerRootsByExpiry.TryGetValue(expiry, out var roots)) return roots;
        roots = _availableExpiryRows.Where(x => x.ExpiryDate == expiry)
            .Select(x => x.ProviderRoot).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        _providerRootsByExpiry[expiry] = roots;
        return roots;
    }

    private async Task ReleaseEvaluatedChainAsync()
    {
        if (_activeEvaluatedExpiry is not { } expiry) return;
        await ReleaseEvaluatedChainAsync(expiry);
    }

    private async Task ReleaseEvaluatedChainAsync(DateOnly expiry)
    {
        var request = CreateChainQuery(expiry) with { ReleaseOnly = true };
        await _appRoot.Services.MarketDataQueries.QueryEvaluatedOptionChainAsync(request);
        if (_activeEvaluatedExpiry == expiry) _activeEvaluatedExpiry = null;
    }

    private void BindEvaluatedChain(EvaluatedOptionChainReadModel chain)
    {
        if (_displayedEvaluatedExpiry != chain.ExpiryDate)
        {
            _displayedEvaluatedExpiry = chain.ExpiryDate;
            _defaultSelectionExpiry = null;
            _selectedMarketContracts.Clear();
            _selectedMarketRoles.Clear();
            _marketSelectionEditedManually = false;
            _optionChainRows.Clear();
            _nearestUnderlyingStrike = null;
        }
        var byStrike = new Dictionary<decimal, (EvaluatedOptionContractReadModel? Call, EvaluatedOptionContractReadModel? Put)>();
        foreach (var row in _optionChainRows)
        {
            if (row.Strike is not { } strike) continue;
            byStrike[strike] = (row.CallEvaluated, row.PutEvaluated);
        }
        foreach (var contract in chain.Contracts)
        {
            byStrike.TryGetValue(contract.Strike, out var pair);
            if (contract.IsCall) pair.Call = MergeEvaluatedContract(pair.Call, contract);
            else pair.Put = MergeEvaluatedContract(pair.Put, contract);
            byStrike[contract.Strike] = pair;
        }
        TryApplyDefaultCondorSelection(chain);
        var strikes = byStrike.Keys.ToArray();
        Array.Sort(strikes);
        var nextRows = new List<OptionChainDisplayRow>(strikes.Length);
        for (var index = strikes.Length - 1; index >= 0; index--)
        {
            var strike = strikes[index];
            var pair = byStrike[strike];
            nextRows.Add(new(strike,null,null,
                CallDelta:FormatDelta(pair.Call?.Delta),
                CallOi:FormatCount(pair.Call?.OpenInterest),
                CallVolume:FormatCount(pair.Call?.Volume),
                PutDelta:FormatDelta(pair.Put?.Delta),
                PutOi:FormatCount(pair.Put?.OpenInterest),
                PutVolume:FormatCount(pair.Put?.Volume),
                CallContractId:pair.Call?.ContractId,
                PutContractId:pair.Put?.ContractId,
                CallEvaluated:pair.Call,
                PutEvaluated:pair.Put));
        }
        var shapeChanged = _optionChainRows.Count != nextRows.Count;
        for (var index = 0; !shapeChanged && index < nextRows.Count; index++)
            shapeChanged = _optionChainRows[index].Strike != nextRows[index].Strike;
        if (shapeChanged)
        {
            _optionChainRows.Clear();
            _optionChainRows.AddRange(nextRows);
            _marketGrid.RowCount = nextRows.Count;
            _marketGrid.Invalidate();
        }
        else
        {
            Span<int> changedRows = stackalloc int[8];
            var changedCount = 0;
            for (var index = 0; index < nextRows.Count; index++)
            {
                if (Equals(_optionChainRows[index], nextRows[index])) continue;
                _optionChainRows[index] = nextRows[index];
                if (changedCount < changedRows.Length) changedRows[changedCount] = index;
                changedCount++;
            }
            if (changedCount > changedRows.Length) _marketGrid.Invalidate();
            else for (var index = 0; index < changedCount; index++) _marketGrid.InvalidateRow(changedRows[index]);
        }
        if (chain.UnderlyingPrice is { } currentPrice)
        {
            var nearest = NearestStrike(_optionChainRows, currentPrice);
            if (_nearestUnderlyingStrike != nearest)
            {
                _nearestUnderlyingStrike = nearest;
                _marketGrid.Invalidate();
            }
        }
        RefreshSelectedLegRows();
        if (chain.UnderlyingPrice is { } underlyingPrice)
            SetTextIfChanged(_lastPriceValue, underlyingPrice.ToString("0.00"));
        var representativeIv = RepresentativeImpliedVolatility(chain);
        if (representativeIv is not null)
            SetTextIfChanged(_ivValue, representativeIv.Value.ToString("P1", System.Globalization.CultureInfo.InvariantCulture));
        DateTimeOffset? latestQuote = null;
        foreach (var pair in byStrike.Values)
        {
            if (pair.Call?.QuoteAtUtc is { } callQuote && (latestQuote is null || callQuote > latestQuote))
                latestQuote = callQuote;
            if (pair.Put?.QuoteAtUtc is { } putQuote && (latestQuote is null || putQuote > latestQuote))
                latestQuote = putQuote;
        }
        if (latestQuote is not null)
            SetTextIfChanged(_quoteAgeValue, $"{Math.Max(0, (chain.AsOfUtc - latestQuote.Value).TotalMilliseconds):0} ms");
        _expectedMoveValue.Text = _standardDeviationAmount is { } expectedMove
            ? $"±{expectedMove:0.00}"
            : "-";
        SetSelectionText(_liquiditySelector, chain.WindowMethod);
    }

    private void TryApplyDefaultCondorSelection(EvaluatedOptionChainReadModel chain)
    {
        if (_tradeType is not (TradeType.ShortIronCondor or TradeType.LongIronCondor)
            || _defaultSelectionExpiry == chain.ExpiryDate
            || _marketSelectionEditedManually || _selectedMarketContracts.Count != 0)
            return;
        var call = chain.Contracts
            .Where(value => value.IsCall && value.Delta is > 0 and < 0.5)
            .OrderBy(value => Math.Abs(value.Delta!.Value - DefaultOuterDelta))
            .ThenBy(value => chain.UnderlyingPrice is { } price
                ? Math.Abs(value.Strike - price) : 0m)
            .FirstOrDefault();
        var put = chain.Contracts
            .Where(value => !value.IsCall && value.Delta is < 0 and > -0.5)
            .OrderBy(value => Math.Abs(Math.Abs(value.Delta!.Value) - DefaultOuterDelta))
            .ThenBy(value => chain.UnderlyingPrice is { } price
                ? Math.Abs(value.Strike - price) : 0m)
            .FirstOrDefault();
        if (call is null || put is null) return;
        var callWing = FindWingContract(chain, call.Strike + DefaultWingWidth, true);
        var putWing = FindWingContract(chain, put.Strike - DefaultWingWidth, false);
        if (callWing is null || putWing is null) return;

        var shortCondor = _tradeType == TradeType.ShortIronCondor;
        Add(call.ContractId, shortCondor ? "-SC" : "+LC");
        Add(callWing, shortCondor ? "+LC" : "-SC");
        Add(put.ContractId, shortCondor ? "-SP" : "+LP");
        Add(putWing, shortCondor ? "+LP" : "-SP");
        _defaultSelectionExpiry = chain.ExpiryDate;
        UpdateMarketSelectionStatus();

        void Add(string contractId, string label)
        {
            _selectedMarketContracts.Add(contractId);
            _selectedMarketRoles[contractId] = label;
        }
    }

    private string? FindWingContract(EvaluatedOptionChainReadModel chain, decimal strike, bool isCall)
    {
        var live = chain.Contracts.FirstOrDefault(value => value.IsCall == isCall && value.Strike == strike);
        if (live is not null) return live.ContractId;
        return _availableOptionContracts
            .Where(value => GetOptionExpiry(value) == chain.ExpiryDate
                && (decimal)value.StrikePrice == strike
                && value.OptionType.StartsWith(isCall ? "C" : "P", StringComparison.OrdinalIgnoreCase))
            .OrderBy(value => value.ContractId, StringComparer.Ordinal)
            .Select(value => value.ContractId)
            .FirstOrDefault();
    }

    private string SelectedLabel(OptionChainDisplayRow row, bool callSide)
    {
        var contract = ContractKey(row, callSide);
        if (contract is not null && _selectedMarketRoles.TryGetValue(contract, out var role)
            && _selectedMarketContracts.Contains(contract))
            return role;
        if (contract is not null && _selectedMarketContracts.Contains(contract))
            return "SELECTED";
        return callSide ? row.CallSelected : row.PutSelected;
    }

    private static EvaluatedOptionContractReadModel MergeEvaluatedContract(
        EvaluatedOptionContractReadModel? previous, EvaluatedOptionContractReadModel incoming)
    {
        if (previous is null || previous.ContractId != incoming.ContractId) return incoming;
        return incoming with
        {
            Bid = incoming.Bid ?? previous.Bid,
            Ask = incoming.Ask ?? previous.Ask,
            BidSize = incoming.BidSize ?? previous.BidSize,
            AskSize = incoming.AskSize ?? previous.AskSize,
            Last = incoming.Last ?? previous.Last,
            LastSize = incoming.LastSize ?? previous.LastSize,
            ImpliedVolatility = incoming.ImpliedVolatility ?? previous.ImpliedVolatility,
            TheoreticalPrice = incoming.TheoreticalPrice ?? previous.TheoreticalPrice,
            Delta = incoming.Delta ?? previous.Delta,
            Gamma = incoming.Gamma ?? previous.Gamma,
            Vega = incoming.Vega ?? previous.Vega,
            Theta = incoming.Theta ?? previous.Theta,
            Rho = incoming.Rho ?? previous.Rho,
            Volume = incoming.Volume ?? previous.Volume,
            OpenInterest = incoming.OpenInterest ?? previous.OpenInterest,
            GreeksValid = incoming.GreeksValid || previous.GreeksValid,
            QuoteAtUtc = incoming.QuoteAtUtc ?? previous.QuoteAtUtc,
            TradeAtUtc = incoming.TradeAtUtc ?? previous.TradeAtUtc,
            EvaluatedAtUtc = incoming.EvaluatedAtUtc ?? previous.EvaluatedAtUtc
        };
    }

    private static void SetSelectionText(ComboBox selector, string value)
    {
        if (!selector.Items.Contains(value)) selector.Items.Add(value);
        if (!Equals(selector.SelectedItem, value)) selector.SelectedItem = value;
    }

    private static void SetTextIfChanged(Control control, string value)
    {
        if (!string.Equals(control.Text, value, StringComparison.Ordinal)) control.Text = value;
    }

    private static double? RepresentativeImpliedVolatility(EvaluatedOptionChainReadModel chain)
    {
        if (chain.UnderlyingPrice is not { } underlying) return null;
        decimal? nearestStrike = null;
        var nearestDistance = decimal.MaxValue;
        foreach (var value in chain.Contracts)
        {
            if (value.ImpliedVolatility is not > 0 || !double.IsFinite(value.ImpliedVolatility.Value)) continue;
            var distance = Math.Abs(value.Strike - underlying);
            if (distance > nearestDistance || distance == nearestDistance && value.Strike >= nearestStrike) continue;
            nearestStrike = value.Strike;
            nearestDistance = distance;
        }
        if (nearestStrike is null) return null;
        var sum = 0d;
        var count = 0;
        foreach (var value in chain.Contracts)
        {
            if (value.Strike != nearestStrike || value.ImpliedVolatility is not > 0
                || !double.IsFinite(value.ImpliedVolatility.Value)) continue;
            sum += value.ImpliedVolatility.Value;
            count++;
        }
        return count == 0 ? null : sum / count;
    }

    private static DateOnly GetOptionExpiry(FuturesOptionContractReadModel contract) =>
        contract.ExpirationUtc is { } expiration
            ? DateOnly.FromDateTime(expiration.UtcDateTime)
            : contract.ContractMonth;

    private sealed record ExpiryChoice(DateOnly Value)
    {
        public override string ToString() => Value.ToString("dd MMM yy", System.Globalization.CultureInfo.InvariantCulture);
    }

    private void MarketCellValueNeeded(object? sender, DataGridViewCellValueEventArgs e)
    {
        if (e.RowIndex < 0 || e.RowIndex >= _optionChainRows.Count) return;
        e.Value = ChainCellValue(_optionChainRows[e.RowIndex], e.ColumnIndex);
    }

    private void SelectedLegCellValueNeeded(object? sender, DataGridViewCellValueEventArgs e)
    {
        if (e.RowIndex < 0 || e.RowIndex >= _selectedLegRows.Count) return;
        e.Value = ChainCellValue(_selectedLegRows[e.RowIndex], e.ColumnIndex);
    }

    private object? ChainCellValue(OptionChainDisplayRow row, int columnIndex)
    {
        if (row.MarkerText is not null)
            return null;
        if (columnIndex < 6 && row.Call is null && row.CallEvaluated is null
            && row.CallContractId is null && row.CallSelected.Length == 0)
            return null;
        if (columnIndex > 6 && row.Put is null && row.PutEvaluated is null
            && row.PutContractId is null && row.PutSelected.Length == 0)
            return null;
        var call = row.Call?.Instrument;
        var put = row.Put?.Instrument;
        var callEvaluated = row.CallEvaluated;
        var putEvaluated = row.PutEvaluated;
        return columnIndex switch
        {
            0 => SelectedLabel(row, true),
            1 => row.CallDelta.Length == 0 ? FormatDelta(callEvaluated?.Delta ?? call?.Selection?.Delta) : row.CallDelta,
            2 => row.CallOi.Length == 0 ? FormatCount(callEvaluated?.OpenInterest ?? call?.OpenInterest) : row.CallOi,
            3 => row.CallVolume.Length == 0 ? FormatCount(callEvaluated?.Volume ?? call?.SessionVolume) : row.CallVolume,
            4 => PreviewOr(row.CallBid, callEvaluated?.Bid ?? call?.Quote?.Bid),
            5 => PreviewOr(row.CallAsk, callEvaluated?.Ask ?? call?.Quote?.Ask),
            6 => row.Strike,
            7 => PreviewOr(row.PutBid, putEvaluated?.Bid ?? put?.Quote?.Bid),
            8 => PreviewOr(row.PutAsk, putEvaluated?.Ask ?? put?.Quote?.Ask),
            9 => row.PutVolume.Length == 0 ? FormatCount(putEvaluated?.Volume ?? put?.SessionVolume) : row.PutVolume,
            10 => row.PutOi.Length == 0 ? FormatCount(putEvaluated?.OpenInterest ?? put?.OpenInterest) : row.PutOi,
            11 => row.PutDelta.Length == 0 ? FormatDelta(putEvaluated?.Delta ?? put?.Selection?.Delta) : row.PutDelta,
            12 => SelectedLabel(row, false),
            _ => null
        };
    }

    private static object? PreviewOr(string preview, object? live) =>
        string.IsNullOrEmpty(preview) ? live : preview;

    private static string FormatDelta(double? value) =>
        value?.ToString("+0.000;-0.000;0.000", System.Globalization.CultureInfo.InvariantCulture) ?? "-";

    private static string FormatCount(long? value) => value switch
    {
        >= 1_000_000 => $"{value.Value / 1_000_000d:0.0}M",
        >= 1_000 => $"{value.Value / 1_000d:0.0}K",
        not null => value.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
        _ => "-"
    };

    private void MarketGridCellClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (_readOnly || e.RowIndex < 0 || e.RowIndex >= _optionChainRows.Count || e.ColumnIndex == 6)
            return;
        var row = _optionChainRows[e.RowIndex];
        if (row.MarkerText is not null)
            return;
        var callSide = e.ColumnIndex < 6;
        var contract = ContractKey(row, callSide);
        if (string.IsNullOrWhiteSpace(contract))
            return;
        _marketSelectionEditedManually = true;
        if (!_selectedMarketContracts.Remove(contract))
        {
            if (_selectedMarketContracts.Count >= MaximumSelectedLegs)
            {
                _marketSelectionLabel.Text =
                    $"Selected legs: {_selectedMarketContracts.Count} / {MaximumSelectedLegs} - maximum reached";
                return;
            }
            _selectedMarketContracts.Add(contract);
        }
        else _selectedMarketRoles.Remove(contract);
        UpdateMarketSelectionStatus();
    }

    private void MarketGridCellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0 || e.RowIndex >= _optionChainRows.Count)
            return;
        FormatChainCell(_optionChainRows[e.RowIndex], e, _marketGrid, true);
    }

    private void SelectedLegCellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0 || e.RowIndex >= _selectedLegRows.Count)
            return;
        FormatChainCell(_selectedLegRows[e.RowIndex], e, _selectedLegGrid, false);
    }

    private void FormatChainCell(OptionChainDisplayRow row, DataGridViewCellFormattingEventArgs e,
        DataGridView grid, bool highlightNearest)
    {
        if (row.MarkerText is not null)
        {
            e.CellStyle.BackColor = Color.FromArgb(45, 45, 45);
            e.CellStyle.ForeColor = Color.Gainsboro;
            return;
        }
        if (highlightNearest && row.Strike == _nearestUnderlyingStrike)
        {
            e.CellStyle.BackColor = Color.Yellow;
            e.CellStyle.ForeColor = Color.Black;
            if (e.ColumnIndex == 6)
            {
                e.CellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                e.CellStyle.Font = grid.Columns["Strike"]!.DefaultCellStyle.Font;
            }
            return;
        }
        var contract = e.ColumnIndex < 6 ? ContractKey(row, true)
            : e.ColumnIndex > 6 ? ContractKey(row, false) : null;
        var role = e.ColumnIndex < 6 ? SelectedLabel(row, true)
            : e.ColumnIndex > 6 ? SelectedLabel(row, false) : "";
        if (!string.IsNullOrEmpty(role))
        {
            e.CellStyle.BackColor = role.StartsWith("-", StringComparison.Ordinal) ? ShortColor : LongColor;
            e.CellStyle.ForeColor = Color.White;
        }
        else if (contract is not null && _selectedMarketContracts.Contains(contract))
        {
            e.CellStyle.BackColor = LongColor;
            e.CellStyle.ForeColor = Color.White;
        }
        else if (e.ColumnIndex == 6)
        {
            e.CellStyle.BackColor = Color.FromArgb(45, 45, 45);
            e.CellStyle.ForeColor = Color.White;
            e.CellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            e.CellStyle.Font = grid.Columns["Strike"]!.DefaultCellStyle.Font;
        }
    }

    private static string? ContractKey(OptionChainDisplayRow row, bool callSide)
    {
        var live = callSide ? row.Call?.Instrument.ContractId : row.Put?.Instrument.ContractId;
        var definition = callSide ? row.CallContractId : row.PutContractId;
        return live ?? definition
            ?? (row.Strike is null ? null : $"preview:{(callSide ? "C" : "P")}:{row.Strike:0.########}");
    }

    private void MarketGridRowPostPaint(object? sender, DataGridViewRowPostPaintEventArgs e)
    {
        if (e.RowIndex < 0 || e.RowIndex >= _optionChainRows.Count)
            return;
        var marker = _optionChainRows[e.RowIndex].MarkerText;
        if (string.IsNullOrWhiteSpace(marker))
            return;
        var bounds = new Rectangle(0, e.RowBounds.Top, _marketGrid.ClientSize.Width, e.RowBounds.Height);
        using var background = new SolidBrush(Color.FromArgb(45, 45, 45));
        e.Graphics.FillRectangle(background, bounds);
        TextRenderer.DrawText(e.Graphics, marker, _marketGrid.Font, bounds, Color.Gainsboro,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
    }

    private void UpdateMarketSelectionStatus()
    {
        _selectedMarketContractIds = _selectedMarketContracts.ToArray();
        _stagingTab.Text = "Leg Staging";
        _marketSelectionLabel.Text =
            $"Selected legs: {_selectedMarketContracts.Count} / {MaximumSelectedLegs} - Select a Call or Put quote to stage a leg";
        RefreshSelectedLegRows();
        _marketGrid.Invalidate();
    }

    private void RefreshSelectedLegRows()
    {
        _selectedLegRows.Clear();
        foreach (var row in _optionChainRows)
        {
            if (row.Strike is null) continue;
            if (_selectedMarketContracts.Contains(ContractKey(row, true) ?? ""))
                _selectedLegRows.Add(row with
                {
                    Put = null, PutEvaluated = null, PutContractId = null,
                    PutSelected = "", PutDelta = "", PutOi = "", PutVolume = "",
                    PutBid = "", PutAsk = ""
                });
            if (_selectedMarketContracts.Contains(ContractKey(row, false) ?? ""))
                _selectedLegRows.Add(row with
                {
                    Call = null, CallEvaluated = null, CallContractId = null,
                    CallSelected = "", CallDelta = "", CallOi = "", CallVolume = "",
                    CallBid = "", CallAsk = ""
                });
        }
        _selectedLegGrid.RowCount = _selectedLegRows.Count;
        // The default row height grows with DPI; a fixed 96px slot can hide the fourth leg.
        // Reserve space for the strategy's maximum rows using the grid's actual row height.
        if (_selectedLegGrid.Parent is TableLayoutPanel layout)
        {
            var rowHeight = _selectedLegGrid.RowCount > 0
                ? _selectedLegGrid.Rows.Cast<DataGridViewRow>().Max(row => row.Height)
                : _selectedLegGrid.RowTemplate.Height;
            var visibleHeight = _selectedLegGrid.Rows.Cast<DataGridViewRow>().Sum(row => row.Height);
            layout.RowStyles[4].Height = Math.Max(112,
                Math.Max(visibleHeight, MaximumSelectedLegs * rowHeight) + 16);
        }
        _selectedLegGrid.Invalidate();
    }

    private static decimal? NearestStrike(IEnumerable<OptionChainDisplayRow> rows, decimal underlyingPrice) =>
        rows.Where(row => row.Strike is not null)
            .OrderBy(row => Math.Abs(row.Strike!.Value - underlyingPrice))
            .ThenBy(row => row.Strike)
            .Select(row => row.Strike)
            .FirstOrDefault();

    private static DataGridView Grid(string name) => new()
    {
        Name = name, Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false,
        AllowUserToDeleteRows = false, AllowUserToOrderColumns = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        BackgroundColor = Color.Black, ForeColor = Color.White, GridColor = Color.FromArgb(70, 70, 70),
        RowHeadersVisible = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        EnableHeadersVisualStyles = false, ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing
    };

    private static Label HeaderLabel(string text) => new() { AutoSize = true, ForeColor = Color.White, Text = text, Padding = new Padding(4, 4, 4, 0) };
    private static Label ReadOnlyValue(string name, string text) => new()
    {
        Name = name, Text = text, Dock = DockStyle.Fill,
        ForeColor = Color.White, BackColor = Color.Black,
        BorderStyle = BorderStyle.FixedSingle,
        TextAlign = ContentAlignment.MiddleLeft,
        Padding = new Padding(6, 0, 4, 0)
    };
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

    private sealed record OptionChainDisplayRow(
        decimal? Strike,
        CompositionInstrumentSnapshot? Call,
        CompositionInstrumentSnapshot? Put,
        string? MarkerText = null,
        string CallSelected = "",
        string CallDelta = "",
        string CallOi = "",
        string CallVolume = "",
        string CallBid = "",
        string CallAsk = "",
        string PutDelta = "",
        string PutOi = "",
        string PutVolume = "",
        string PutBid = "",
        string PutAsk = "",
        string PutSelected = "",
        string? CallContractId = null,
        string? PutContractId = null,
        EvaluatedOptionContractReadModel? CallEvaluated = null,
        EvaluatedOptionContractReadModel? PutEvaluated = null)
    {
        public static OptionChainDisplayRow Preview(decimal strike, string callSelected, string callDelta,
            string callOi, string callVolume, string callBid, string callAsk, string putDelta,
            string putOi, string putVolume, string putBid, string putAsk, string putSelected) =>
            new(strike, null, null, null, callSelected, callDelta, callOi, callVolume, callBid, callAsk,
                putDelta, putOi, putVolume, putBid, putAsk, putSelected,
                CallContractId: $"preview:C:{strike:0.########}",
                PutContractId: $"preview:P:{strike:0.########}");

        public static OptionChainDisplayRow Marker(string text) => new(null, null, null, text);

        public static OptionChainDisplayRow Definition(
            decimal strike,
            string? callContractId,
            string? putContractId) =>
            new(strike, null, null, CallContractId: callContractId, PutContractId: putContractId);
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
    public async Task SetLiveFeedAsync(bool enabled)
    {
        if (enabled && _readOnly)
            throw new InvalidOperationException("Live market selection is unavailable for a read-only trade blotter.");
        // Market Selection owns its expiry-wide evaluated chain. The hosted trade workflow
        // may use a separate feed for chosen legs, but it must not gate this feed.
        _liveFeedEnabled = enabled;
        if (enabled)
        {
            ResetChainRequestCancellation();
            _chainRefreshTimer.Start();
            await RefreshEvaluatedChainAsync();
        }
        else
        {
            _chainRefreshTimer.Stop();
            _chainRequestCancellation?.Cancel();
            await ReleaseEvaluatedChainAsync();
        }
    }
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
        _chainRefreshTimer.Stop();
        _liveFeedEnabled = false;
        _chainRequestCancellation?.Cancel();
        await ReleaseEvaluatedChainAsync();
        if (_workflowControl is IAsyncFormControl asyncControl)
            await asyncControl.CloseAsync();
        else if (_workflowControl is IFormControl formControl)
            formControl.Close();
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _chainRefreshTimer.Stop();
            _chainRefreshTimer.Dispose();
            var cancellation = _chainRequestCancellation;
            _chainRequestCancellation = null;
            if (cancellation is not null)
            {
                cancellation.Cancel();
                cancellation.Dispose();
            }
        }
        base.Dispose(disposing);
    }
    void IFormControl.Resize(Control parentControl) => Bounds = parentControl.ClientRectangle;
}

/// <summary>Compatibility name retained for existing factory and automation callers.</summary>
public sealed class BrokerTradeBlotterView : EsTradeBlotterControl
{
    /// <summary>Initializes the compatibility-named canonical Portfolio Fund trade blotter.</summary>
    /// <param name="appRoot">The application service root.</param>
    /// <param name="fund">The selected canonical Fund.</param>
    /// <param name="order">The selected canonical order.</param>
    /// <param name="trade">The selected canonical trade.</param>
    /// <param name="valueDate">The optional historical value date.</param>
    /// <param name="baseContracts">The available futures contracts.</param>
    public BrokerTradeBlotterView(IAppRoot appRoot, PortfolioFundEditorModel fund, PortfolioFundOrderEditorModel order,
        PortfolioFundOrderTradeEditorModel trade, int portfolioId, bool historicalReadOnly,
        Control? workflowControl = null, BrokerCapabilities? capabilities = null)
        : base(appRoot, fund, order, trade, portfolioId, historicalReadOnly, capabilities, workflowControl)
    {
        Name = trade.TradeType == TradeType.FuturesOutright ? "FuturesView" : "VerticalSpreadView";
    }
}
