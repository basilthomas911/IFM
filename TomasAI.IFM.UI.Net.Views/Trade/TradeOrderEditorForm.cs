using TomasAI.IFM.UI.Net.Models.Portfolio;
using TomasAI.IFM.Domain.Trade.Shared;
using System.Data;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.UI.Net.Views.Trade.IronCondor;
using TomasAI.IFM.UI.Net.ViewModels.Trade;
using TomasAI.IFM.UI.Net.ViewModels.Trade.IronCondor;
using System.ComponentModel;
using TomasAI.IFM.UI.Net.ViewModels.Presentation;
using TomasAI.IFM.UI.Net.ViewModels.Operations;
using TomasAI.IFM.UI.Net.Models;
using TomasAI.IFM.UI.Net.Services.Reference;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.UI.Net.Views.Trade;

public partial class TradeOrderEditorForm 
    : DarkTradingForm, IForm<TradeOrderEditorForm>, IFormControl
{
    const int LeftLabelLeft = 30;
    const int ContentLeft = 112;
    const int ContentToCommandGap = 16;
    const int CommandRightMargin = 21;
    const int CommandButtonWidth = 140;
    const int CommandButtonHeight = 32;
    const int CommandButtonGap = 8;
    const int ListCommandButtonGap = 4;
    const int DefaultClientHeight = 980;
    const int MinimumClientHeight = 650;
    const int EmptyTradeBlotterHeight = 280;
    const int HostedControlBottomPadding = 8;
    const int TradeBlotterBottomPadding = 4;

    readonly IAppRoot _appRoot;
    readonly IReferenceDataService _referenceDataService;
    TradeOrderEditorViewModel _viewModel = null!;
    int? _lastTradeId;
    int? _lastTradeOrderId;
    CancellationTokenSource? _orderSelectionCancellation;
    Task _hostedCleanupTask = Task.CompletedTask;
    PortfolioFundOrderTradeEditorId? _displayedTradeId;
    long _lastErrorSequence;
    long _lastChangeSequence;
    bool _rendering;
    IReadOnlyList<PortfolioFundOrderEditorModel>? _orderDescriptionSource;
    IReadOnlyList<PortfolioFundOrderTradeEditorModel>? _tradeDescriptionSource;
    int _pendingRenderFlags;
    int _renderPosted;
    readonly ComboBox _portfolioSelector = new() { Name = "ddlPortfolio", AccessibleName = "Portfolio selector", DropDownStyle = ComboBoxStyle.DropDownList, BackColor = Color.FromArgb(64, 64, 64), ForeColor = Color.White, Font = new Font("Microsoft Sans Serif", 12F), Location = new Point(93, 8), Size = new Size(720, 28) };
    readonly ComboBox _sourceFilter = new() { Name = "ddlCompositionSource", AccessibleName = "Composition source filter", DropDownStyle = ComboBoxStyle.DropDownList, BackColor = Color.FromArgb(64, 64, 64), ForeColor = Color.White, Font = new Font("Microsoft Sans Serif", 12F), Location = new Point(1010, 8), Size = new Size(220, 28) };
    readonly ComboBox _historyModeSelector = new() { Name = "ddlTradeHistoryMode", AccessibleName = "Trade history mode", DropDownStyle = ComboBoxStyle.DropDownList, BackColor = Color.FromArgb(64, 64, 64), ForeColor = Color.White, Font = new Font("Microsoft Sans Serif", 10F), Location = new Point(1300, 8), Size = new Size(140, 28) };
    readonly Label _portfolioLabel = new() { Text = "Portfolio:", AutoSize = true, ForeColor = Color.White, Font = new Font("Microsoft Sans Serif", 12F), Location = new Point(LeftLabelLeft, 14) };
    readonly Label _sourceLabel = new() { Text = "Source:", AutoSize = true, ForeColor = Color.White };
    readonly Label _modeLabel = new() { Text = "Mode:", AutoSize = true, ForeColor = Color.White };
    readonly TomasAI.IFM.UI.Net.Views.App.DarkTabControl _workspaceTabs = new()
    {
        Name = "tradeOrderWorkspaceTabs",
        AccessibleName = "Trade Order workspace",
        Dock = DockStyle.Fill,
        Appearance = TabAppearance.Normal
    };
    readonly TabPage _tradeOrdersPage = new("Trade Orders")
    {
        Name = "tradeOrdersTab",
        BackColor = Color.FromArgb(64, 64, 64),
        ForeColor = Color.White
    };
    readonly TabPage _tradeBlotterPage = new("Trade Blotter")
    {
        Name = "tradeBlotterTab",
        BackColor = Color.FromArgb(64, 64, 64),
        ForeColor = Color.White
    };
    bool _adjustingTradeBlotterLayout;
    int _submissionInProgress;

    /// <summary>Creates the Portfolio-aware Trade Order editor.</summary>
    /// <param name="appRoot">The application service boundary.</param>
    /// <param name="referenceDataService">The reference-data service used to resolve contract definitions.</param>
    public TradeOrderEditorForm(
        IAppRoot appRoot,
        IReferenceDataService referenceDataService)
    {
        InitializeComponent();
        DoubleBuffered = true;
        ConfigurePortfolioScope();
        TradeOrderTypography.Apply(this);
        TradeOrderInputPalette.Apply(this);
        pnlContentFrame.BackColor = Color.Gray;
        ConfigureCompactLayout();
        btnSubmitOrder.Visible = false;
        btnEndOfDay.Visible = false;
        ddlTradeState.SelectedIndexChanged += ddlTradeState_SelectedIndexChanged;
        _appRoot = appRoot;
        _referenceDataService = referenceDataService;
    }

    public PortfolioFundEditorModel Fund => _viewModel?.SelectedFund!;

    public PortfolioFundOrderEditorModel FundOrder => _viewModel?.SelectedFundOrder!;

    public PortfolioFundOrderTradeEditorModel FundOrderTrade => _viewModel?.SelectedFundOrderTrade!;

    /// <summary>Gets the currently selected canonical Portfolio identifier.</summary>
    public int PortfolioId => _viewModel?.SelectedPortfolio?.PortfolioId ?? 0;
    /// <summary>Loads the editor view model and begins observing its presentation state.</summary>
    /// <param name="viewModel">The Trade Order editor state.</param>
    public void LoadViewModel(TradeOrderEditorViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        if (_viewModel is not null)
            _viewModel.PropertyChanged -= ViewModelPropertyChanged;
        _viewModel = viewModel;
        _viewModel.PropertyChanged += ViewModelPropertyChanged;
        RenderEditor();
    }

    void ConfigurePortfolioScope()
    {
        pnlFundSelector.Height = 100;
        pnlFundSelector.BorderStyle = BorderStyle.None;
        ddlFund.Location = new Point(ContentLeft, 58);
        lblFundSelector.Location = new Point(LeftLabelLeft, 64);
        btnCreateFund.Visible = false;
        btnCreateFund.Enabled = false;
        pnlFundSelector.Controls.Remove(btnCreateFund);
        _portfolioSelector.Left = ContentLeft;
        pnlFundSelector.Controls.Add(_portfolioLabel);
        pnlFundSelector.Controls.Add(_portfolioSelector);
        _sourceLabel.Text = "Source:";
        pnlFundSelector.Controls.Add(_sourceLabel);
        _sourceFilter.Items.AddRange(["All", "Manual", "Strategy Workflow"]);
        _sourceFilter.SelectedIndex = 0;
        pnlFundSelector.Controls.Add(_sourceFilter);
        _portfolioSelector.SelectedIndexChanged += async (_, _) =>
        {
            if (_rendering) return;
            await _viewModel.SelectPortfolioAsync(_portfolioSelector.SelectedIndex);
            RenderEditor();
        };
        _sourceFilter.SelectedIndexChanged += (_, _) => RenderFundOrders();
        btnOpenTrade.Visible = false;
        if (lstTradeOrders.Columns.Count == 4) lstTradeOrders.Columns.Add("Source", 150);
    }

    void ConfigureCompactLayout()
    {
        // The initial width is a layout baseline, not a measurement of a designer-scaled combo.
        ClientSize = new Size(1440, DefaultClientHeight);
        MinimumSize = SizeFromClientSize(new Size(1200, MinimumClientHeight));
        pnlTradePosition.Controls.Remove(lblDaysToExpiry);
        pnlTradePosition.Controls.Remove(txtDaysToExpiry);
        ConfigureWorkspaceTabs();
        AlignLeftColumnAndCalendarFilters();
        LayoutPortfolioFilters();
        cbLiveFeed.AutoSize = false;
        cbLiveFeed.Size = new Size(112, 28);
        cbLiveFeed.FlatStyle = FlatStyle.Flat;
        cbLiveFeed.Padding = new Padding(6, 0, 4, 0);
        cbLiveFeed.TextAlign = ContentAlignment.MiddleCenter;
        cbLiveFeed.UseVisualStyleBackColor = false;
        UpdateLiveFeedAppearance();
        AlignTradePositionHeader();
        lstTradeOrders.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        lstTrades.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        pnlTradeBlotter.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        foreach (var button in new[] { btnLoadOrder, btnCreateOrder, btnDeleteOrder, btnCompleteOrder,
            btnAddTrade, btnRemoveTrade, btnChangeTradeState, btnOpenTrade, btnSubmitOrder, btnEndOfDay })
        {
            button.AutoSize = false;
            button.Size = new Size(CommandButtonWidth, CommandButtonHeight);
            button.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        }

        PositionButtonColumn(pnlTradeOrders, lstTradeOrders.Top, ListCommandButtonGap,
            btnLoadOrder, btnCreateOrder, btnDeleteOrder, btnCompleteOrder);
        PositionButtonColumn(pnlTrades, lstTrades.Top, ListCommandButtonGap,
            btnAddTrade, btnRemoveTrade, btnChangeTradeState);
        btnOpenTrade.Location = btnAddTrade.Location;
        PositionButtonColumn(pnlTradePosition, pnlTradeBlotter.Top, CommandButtonGap,
            btnSubmitOrder, btnEndOfDay);

        pnlFundSelector.Resize += (_, _) =>
        {
            ddlFund.Width = CalculateMainContentWidth(pnlFundSelector);
            LayoutPortfolioFilters();
        };
        pnlTradeOrders.Resize += (_, _) =>
        {
            LayoutMainContent(pnlTradeOrders, lstTradeOrders);
            FitListToPanelBottom(pnlTradeOrders, lstTradeOrders);
            PositionButtonColumn(pnlTradeOrders, lstTradeOrders.Top, ListCommandButtonGap,
                btnLoadOrder, btnCreateOrder, btnDeleteOrder, btnCompleteOrder);
        };
        pnlTrades.Resize += (_, _) =>
        {
            LayoutMainContent(pnlTrades, lstTrades);
            FitListToPanelBottom(pnlTrades, lstTrades);
            PositionButtonColumn(pnlTrades, lstTrades.Top, ListCommandButtonGap,
                btnAddTrade, btnRemoveTrade, btnChangeTradeState);
            btnOpenTrade.Location = btnAddTrade.Location;
        };
        pnlTradePosition.Resize += (_, _) =>
        {
            LayoutTradeBlotterHeight();
            LayoutTradePositionBlotter();
            AlignTradePositionHeader();
            PositionButtonColumn(pnlTradePosition, pnlTradeBlotter.Top, CommandButtonGap,
                btnSubmitOrder, btnEndOfDay);
        };
        pnlTradeBlotter.ControlAdded += (_, _) => LayoutTradeBlotterHeight();
        pnlTradeBlotter.ControlRemoved += (_, _) => LayoutTradeBlotterHeight();

        ddlFund.Width = CalculateMainContentWidth(pnlFundSelector);
        LayoutMainContent(pnlTradeOrders, lstTradeOrders);
        FitListToPanelBottom(pnlTradeOrders, lstTradeOrders);
        LayoutMainContent(pnlTrades, lstTrades);
        FitListToPanelBottom(pnlTrades, lstTrades);
        LayoutTradePositionBlotter();
        LayoutTradeBlotterHeight();
        PositionButtonColumn(pnlTradeOrders, lstTradeOrders.Top, ListCommandButtonGap,
            btnLoadOrder, btnCreateOrder, btnDeleteOrder, btnCompleteOrder);
    }

    void ConfigureWorkspaceTabs()
    {
        pnlContentFrame.SuspendLayout();
        _tradeOrdersPage.SuspendLayout();
        _tradeBlotterPage.SuspendLayout();
        try
        {
            pnlContentFrame.Controls.Remove(pnlFundSelector);
            pnlContentFrame.Controls.Remove(panel1);
            pnlContentFrame.Controls.Remove(pnlTradeOrders);

            panel1.Controls.Remove(pnlTrades);
            panel1.Controls.Remove(pnlTradePosition);
            panel1.Controls.Remove(txtStatus);

            var orderLayout = new TableLayoutPanel
            {
                Name = "tradeOrdersLayout",
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Margin = new Padding(0),
                Padding = new Padding(0),
                BackColor = Color.FromArgb(64, 64, 64)
            };
            orderLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            orderLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 54));
            orderLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 46));

            pnlTradeOrders.Dock = DockStyle.Fill;
            pnlTradeOrders.Margin = new Padding(0);
            pnlTrades.Dock = DockStyle.Fill;
            pnlTrades.Margin = new Padding(0);
            orderLayout.Controls.Add(pnlTradeOrders, 0, 0);
            orderLayout.Controls.Add(pnlTrades, 0, 1);
            _tradeOrdersPage.Controls.Add(orderLayout);

            pnlTradePosition.Dock = DockStyle.Fill;
            _tradeBlotterPage.Controls.Add(pnlTradePosition);

            _workspaceTabs.TabPages.Clear();
            _workspaceTabs.TabPages.AddRange([_tradeOrdersPage, _tradeBlotterPage]);
            _workspaceTabs.SelectedTab = _tradeOrdersPage;

            var workspaceLayout = new TableLayoutPanel
            {
                Name = "tradeOrderWorkspaceLayout",
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Margin = new Padding(0),
                Padding = new Padding(0),
                BackColor = Color.Gray
            };
            workspaceLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            workspaceLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, pnlFundSelector.Height));
            workspaceLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 3));
            workspaceLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            pnlFundSelector.Dock = DockStyle.Fill;
            txtStatus.Visible = false;
            workspaceLayout.Controls.Add(pnlFundSelector, 0, 0);
            workspaceLayout.Controls.Add(new DarkHeaderSeparator
            {
                Name = "portfolioFundHeaderSeparator",
                Margin = new Padding(4, 0, 4, 0)
            }, 0, 1);
            workspaceLayout.Controls.Add(_workspaceTabs, 0, 2);
            pnlContentFrame.Controls.Add(workspaceLayout);
        }
        finally
        {
            _tradeBlotterPage.ResumeLayout(true);
            _tradeOrdersPage.ResumeLayout(true);
            pnlContentFrame.ResumeLayout(true);
        }
    }

    void LayoutPortfolioFilters()
    {
        const int groupGap = 12;
        const int labelGap = 6;
        var filterWidth = 2 * groupGap + 2 * labelGap + _sourceLabel.PreferredWidth
            + _modeLabel.PreferredWidth + _sourceFilter.Width + _historyModeSelector.Width;
        _portfolioSelector.Width = Math.Min(720, Math.Max(200,
            pnlFundSelector.ClientSize.Width - ContentLeft - filterWidth - CommandRightMargin));
        _sourceLabel.Location = new Point(_portfolioSelector.Right + groupGap,
            _portfolioSelector.Top + (_portfolioSelector.Height - _sourceLabel.PreferredHeight) / 2);
        _sourceFilter.Location = new Point(_sourceLabel.Right + labelGap, _portfolioSelector.Top);
        _modeLabel.Location = new Point(_sourceFilter.Right + groupGap,
            _portfolioSelector.Top + (_portfolioSelector.Height - _modeLabel.PreferredHeight) / 2);
        _historyModeSelector.Location = new Point(_modeLabel.Right + labelGap, _portfolioSelector.Top);
    }

    void AlignLeftColumnAndCalendarFilters()
    {
        foreach (var label in new[]
                 {
                     _portfolioLabel, lblFundSelector, lblFrom, lblTradeOrders,
                     label1, lblTrades, lblTradeType,
                 })
            label.Left = LeftLabelLeft;

        _portfolioSelector.Left = ContentLeft;
        ddlFund.Left = ContentLeft;
        dtpFrom.Left = ContentLeft;
        lblTo.Left = dtpFrom.Right + 24;
        dtpTo.Left = lblTo.Right + 8;
        txtTradeType.Left = ContentLeft;
        lblTradeType.Left = 8;
    }

    void AlignTradePositionHeader()
    {
        var longestTradeType = Enum.GetNames<TradeType>()
            .Select(name => TextRenderer.MeasureText(name, txtTradeType.Font).Width)
            .DefaultIfEmpty(txtTradeType.Width)
            .Max();
        txtTradeType.Width = longestTradeType + 30;
        const int labelToControlGap = 6;
        const int groupGap = 14;
        lblTradeType.Left = 8;
        txtTradeType.Left = lblTradeType.Right + labelToControlGap;
        lblTradeDate.Left = txtTradeType.Right + groupGap;
        dtpTradeDate.Left = lblTradeDate.Right + labelToControlGap;
        lblOrderAction.Left = dtpTradeDate.Right + groupGap;
        ddlOrderActionType.Left = lblOrderAction.Right + labelToControlGap;
        cbLiveFeed.Left = ddlOrderActionType.Right + groupGap;
        cbLiveFeed.Top = ddlOrderActionType.Top - 1;
    }

    void LayoutTradeBlotterHeight()
    {
        if (_adjustingTradeBlotterLayout)
            return;

        _adjustingTradeBlotterLayout = true;
        try
        {
            pnlTradeBlotter.Height = Math.Max(
                EmptyTradeBlotterHeight,
                pnlTradePosition.ClientSize.Height - pnlTradeBlotter.Top - TradeBlotterBottomPadding);
        }
        finally
        {
            _adjustingTradeBlotterLayout = false;
        }
    }

    static int MeasureHostedControlHeight(Control control)
    {
        control.PerformLayout();
        var contentBottom = control.Controls.Cast<Control>()
            .Select(child => child.Bottom)
            .DefaultIfEmpty(0)
            .Max();
        return Math.Max(
            control.MinimumSize.Height,
            contentBottom + HostedControlBottomPadding);
    }

    static void PositionButtonColumn(Control parent, int top, int gap, params Button[] buttons)
    {
        var left = Math.Max(ContentLeft, parent.ClientSize.Width - CommandRightMargin - CommandButtonWidth);
        for (var index = 0; index < buttons.Length; index++)
            buttons[index].Location = new Point(
                left,
                top + index * (CommandButtonHeight + gap));
    }

    static void LayoutMainContent(Control parent, Control content)
    {
        content.Left = ContentLeft;
        content.Width = CalculateMainContentWidth(parent);
    }

    static void FitListToPanelBottom(Control parent, Control list)
        => list.Height = Math.Max(80, parent.ClientSize.Height - list.Top - 8);

    void LayoutTradePositionBlotter()
    {
        const int horizontalPadding = 8;
        pnlTradeBlotter.Left = horizontalPadding;
        pnlTradeBlotter.Width = Math.Max(300,
            pnlTradePosition.ClientSize.Width - 2 * horizontalPadding);
    }

    static int CalculateMainContentWidth(Control parent)
        => Math.Max(
            300,
            parent.Width - CommandRightMargin - CommandButtonWidth
            - ContentToCommandGap - ContentLeft);

    void ViewModelPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        const int error = 1, change = 2, portfolios = 4, funds = 8, orders = 16,
            orderSelection = 32, trades = 64, buttons = 128;
        var flag = eventArgs.PropertyName switch
        {
            nameof(TradeOrderEditorViewModel.LastError) => error,
            nameof(TradeOrderEditorViewModel.LastChange) => change,
            nameof(TradeOrderEditorViewModel.Portfolios) => portfolios,
            nameof(TradeOrderEditorViewModel.Funds) => funds,
            nameof(TradeOrderEditorViewModel.FundOrders) or nameof(TradeOrderEditorViewModel.CanonicalOrders) => orders,
            nameof(TradeOrderEditorViewModel.FundOrderSelectedIndex) => orderSelection,
            nameof(TradeOrderEditorViewModel.FundOrderTrades) or nameof(TradeOrderEditorViewModel.FundOrderTradeSelectedIndex) => trades,
            _ => buttons
        };
        Interlocked.Or(ref _pendingRenderFlags, flag);
        if (Interlocked.Exchange(ref _renderPosted, 1) != 0) return;
        this.Post(() =>
        {
            Interlocked.Exchange(ref _renderPosted, 0);
            var pending = Interlocked.Exchange(ref _pendingRenderFlags, 0);
            if (IsDisposed || pending == 0) return;
            if ((pending & error) != 0) ShowLatestError();
            if ((pending & change) != 0) HandleLatestChange();
            if ((pending & portfolios) != 0) RenderPortfolios();
            if ((pending & funds) != 0) RenderFunds();
            if ((pending & orders) != 0) RenderFundOrders();
            if ((pending & orderSelection) != 0) SynchronizeFundOrderSelection();
            if ((pending & trades) != 0) RenderTrades();
            if ((pending & (portfolios | funds | orders | orderSelection | trades | buttons)) != 0)
                UpdateButtons();
        });
    }

    void ShowLatestError()
    {
        var error = _viewModel.LastError;
        if (error is null || error.Sequence <= _lastErrorSequence)
            return;
        _lastErrorSequence = error.Sequence;
        this.ShowErrorMessage(error.Message, error.Caption);
    }

    void HandleLatestChange()
    {
        var change = _viewModel.LastChange;
        if (change is null || change.Sequence <= _lastChangeSequence) return;
        _lastChangeSequence = change.Sequence;
        UpdateButtons();
    }

    /// <summary>Selects the order action displayed by the editor.</summary>
    /// <param name="orderActionType">The opening or closing action.</param>
    public void SetOrderAction(OrderActionType orderActionType)
    {
        ddlOrderActionType.SelectedItem = $"{orderActionType}";
    }

    /// <summary>Sets the trade date displayed by the editor.</summary>
    /// <param name="tradeDate">The trade date.</param>
    public void SetTradeDate(DateOnly tradeDate)
        => dtpTradeDate.Value = tradeDate.ToDateTime(TimeOnly.MinValue);

    /// <summary>Calculates and displays days to expiry from the current trade date.</summary>
    /// <param name="maturityDate">The option maturity date.</param>
    public void SetDaysToExpiry(DateOnly maturityDate)
        => txtDaysToExpiry.Text = $"{maturityDate.DayNumber - DateOnly.FromDateTime(dtpTradeDate.Value).DayNumber}";

    async void TradeOrderForm_Load(object sender, EventArgs e)
    {
        _lastTradeId = null;
        _lastTradeOrderId = null;
        
        var easternToday = EasternTime.GetNow(TimeProvider.System);
        dtpTradeDate.Value = _viewModel!.ValueDate.HasValue ? _viewModel.ValueDate.Value.ToDateTime(TimeOnly.MinValue) : easternToday.Date;
        btnLoadOrder.Enabled = false;
        btnCreateOrder.Enabled = false;
        btnDeleteOrder.Enabled = false;
        btnCompleteOrder.Enabled = false;
        var dtpList = new List<DateTimePicker> { dtpFrom, dtpTo };
        dtpList.ForEach(o => o.Enabled = false);
        dtpFrom.Value = new DateTime(easternToday.Year, easternToday.Month, 1);
        dtpTo.Value = new DateTime(dtpFrom.Value.Year, dtpFrom.Value.Month, DateTime.DaysInMonth(dtpFrom.Value.Year, dtpFrom.Value.Month), 23, 59, 59);
        dtpList.ForEach(o => o.Enabled = true);
        LoadOrderActionTypes();
        try
        {
            _viewModel.SetOrderDateRange(dtpFrom.Value.AddMonths(-1), dtpTo.Value);
            await _viewModel.StartFundOrderListener();
            await LoadFundsAsync();
        }
        catch (Exception exception)
        {
            this.ShowErrorMessage(exception.Message, "Trade Order Editor Error");
        }
    }

    void TradeOrderEditorForm_Shown(object sender, EventArgs e)
    {
    }

    async void TradeOrderEditorForm_FormClosing(object sender, FormClosingEventArgs e)
    {
        _orderSelectionCancellation?.Cancel();
        _orderSelectionCancellation?.Dispose();
        _orderSelectionCancellation = null;
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= ViewModelPropertyChanged;
            await _viewModel.StopFundOrderListener();
        }
        await ClearHostedBlotterAsync();
    }

    void LoadOrderActionTypes()
    {
        ddlOrderActionType.Enabled = false;
        ddlOrderActionType.Items.Clear();
        ddlOrderActionType.Items.Add($"{OrderActionType.Open}");
        ddlOrderActionType.Items.Add($"{OrderActionType.Close}");
        ddlOrderActionType.SelectedIndex = 0;
    }

    async Task LoadFundsAsync()
    {
        _lastTradeId = null;
        _lastTradeOrderId = null;
        _displayedTradeId = null;
        await ClearHostedBlotterAsync();
        await _viewModel.LoadFunds();
        if (_viewModel.SelectedFundOrder is { } selectedOrder)
        {
            _lastTradeOrderId = selectedOrder.OrderId;
            await SelectOrderAsync(selectedOrder.OrderId);
        }
    }

    void RenderEditor()
    {
        RenderPortfolios();
        RenderFunds();
        RenderFundOrders();
        RenderTrades();
        UpdateButtons();
    }

    void RenderPortfolios()
    {
        var prior = _rendering; _rendering = true;
        try
        {
            _portfolioSelector.Items.Clear();
            foreach (var portfolio in _viewModel.Portfolios) _portfolioSelector.Items.Add($"{portfolio.PortfolioId} — {portfolio.Name}");
            _portfolioSelector.SelectedIndex = _viewModel.PortfolioSelectedIndex;
        }
        finally { _rendering = prior; }
    }

    void RenderFunds()
    {
        var wasRendering = _rendering;
        _rendering = true;
        try
        {
            ddlFund.Items.Clear();
            foreach (var fund in _viewModel.Funds)
                ddlFund.Items.Add(fund.Name);
            ddlFund.AccessibleDescription = string.Join(", ", _viewModel.Funds.Select(fund => fund.Name));
            ddlFund.SelectedIndex = _viewModel.FundSelectedIndex;
            UpdateFundSelectorAccessibility();
        }
        finally
        {
            _rendering = wasRendering;
        }
    }

    void RenderFundOrders()
    {
        var wasRendering = _rendering;
        _rendering = true;
        lstTradeOrders.BeginUpdate();
        try
        {
            var filter = _sourceFilter.SelectedItem?.ToString() ?? "All";
            var canonicalById = _viewModel.CanonicalOrders.ToDictionary(order => order.OrderId);
            var visible = new List<(PortfolioFundOrderEditorModel Order, string Source)>();
            foreach (var fundOrder in _viewModel.FundOrders)
            {
                if (!canonicalById.TryGetValue(fundOrder.OrderId, out var canonical)) continue;
                var source = canonical.Origin == CompositionOrigin.ManualUi ? "Manual" : "Strategy Workflow";
                if (filter != "All" && filter != source) continue;
                visible.Add((fundOrder, source));
            }
            var sameRows = lstTradeOrders.Items.Count == visible.Count;
            for (var index = 0; sameRows && index < visible.Count; index++)
                sameRows = lstTradeOrders.Items[index].Tag is PortfolioFundOrderEditorModel old
                    && old.OrderId == visible[index].Order.OrderId;
            if (!sameRows) lstTradeOrders.Items.Clear();
            for (var index = 0; index < visible.Count; index++)
            {
                var (fundOrder, source) = visible[index];
                string[] values = [
                    $"{fundOrder.OrderId}",
                    $"{EasternTime.FromUtc(fundOrder.CreatedOnUtc):yyyy-MMM-dd}",
                    fundOrder.Status,
                    fundOrder.OperatorReference,
                    source
                ];
                if (sameRows) UpdateSubItems(lstTradeOrders.Items[index], values, fundOrder);
                else lstTradeOrders.Items.Add(new ListViewItem(values) { Tag = fundOrder });
            }
            if (!ReferenceEquals(_orderDescriptionSource, _viewModel.FundOrders))
            {
                var description = string.Join(" || ", _viewModel.FundOrders.Select(fundOrder =>
                    $"{fundOrder.OrderId} | {EasternTime.FromUtc(fundOrder.CreatedOnUtc):yyyy-MMM-dd} | "
                    + $"{fundOrder.Status} | {fundOrder.OperatorReference}"));
                lstTradeOrders.AccessibleDescription = description;
                lstTradeOrders.AccessibleName = $"Portfolio fund orders; rows: {description}";
                _orderDescriptionSource = _viewModel.FundOrders;
            }
            var selectedOrderId = _lastTradeOrderId ?? _viewModel.SelectedFundOrder?.OrderId;
            _lastTradeOrderId = null;
            ListViewItem? selectedItem = null;
            foreach (ListViewItem item in lstTradeOrders.Items)
                if (item.Tag is PortfolioFundOrderEditorModel order && order.OrderId == selectedOrderId)
                { selectedItem = item; break; }
            if (selectedItem is not null)
            {
                if (!selectedItem.Selected) selectedItem.Selected = true;
                selectedItem.Focused = true;
                if (!sameRows) selectedItem.EnsureVisible();
            }
        }
        finally
        {
            lstTradeOrders.EndUpdate();
            _rendering = wasRendering;
        }
    }

    void SynchronizeFundOrderSelection()
    {
        if (_rendering) return;
        var selectedOrderId = _viewModel.SelectedFundOrder?.OrderId;
        ListViewItem? selectedItem = null;
        foreach (ListViewItem item in lstTradeOrders.Items)
            if (item.Tag is PortfolioFundOrderEditorModel order && order.OrderId == selectedOrderId)
            { selectedItem = item; break; }
        var wasRendering = _rendering;
        _rendering = true;
        try
        {
            foreach (ListViewItem item in lstTradeOrders.Items)
                if (item.Selected != ReferenceEquals(item, selectedItem))
                    item.Selected = ReferenceEquals(item, selectedItem);
            if (selectedItem is not null)
            {
                selectedItem.Focused = true;
                selectedItem.EnsureVisible();
            }
        }
        finally { _rendering = wasRendering; }
    }
    static void UpdateSubItems(ListViewItem item, string[] values, object model)
    {
        item.Tag = model;
        for (var index = 0; index < values.Length; index++)
        {
            if (index >= item.SubItems.Count) item.SubItems.Add(values[index]);
            else if (!string.Equals(item.SubItems[index].Text, values[index], StringComparison.Ordinal))
                item.SubItems[index].Text = values[index];
        }
    }
    void RenderTrades()
    {
        var wasRendering = _rendering;
        _rendering = true;
        lstTrades.BeginUpdate();
        try
        {
            ddlTradeState.Items.Clear();
            var trades = _viewModel.FundOrderTrades;
            var sameRows = lstTrades.Items.Count == trades.Count;
            for (var index = 0; sameRows && index < trades.Count; index++)
                sameRows = lstTrades.Items[index].Tag is PortfolioFundOrderTradeEditorModel old
                    && old.TradeId == trades[index].TradeId;
            if (!sameRows) lstTrades.Items.Clear();
            for (var index = 0; index < trades.Count; index++)
            {
                var trade = trades[index];
                string[] values = [
                    $"{trade.TradeId}", $"{trade.TradeType}", $"{trade.RequestedTradeDate:yyyy-MMM-dd}",
                    $"{trade.RequestedMaturityDate:yyyy-MMM-dd}", $"{trade.TradeState}",
                    $"{trade.TradeAction} {trade.InstructionReference}"
                ];
                if (sameRows) UpdateSubItems(lstTrades.Items[index], values, trade);
                else lstTrades.Items.Add(new ListViewItem(values) { Tag = trade });
            }
            if (!ReferenceEquals(_tradeDescriptionSource, trades))
            {
                var description = string.Join(" || ", trades.Select(trade =>
                    $"{trade.TradeId} | {trade.TradeType} | {trade.RequestedTradeDate:yyyy-MMM-dd} | "
                    + $"{trade.RequestedMaturityDate:yyyy-MMM-dd} | {trade.TradeState} | {trade.TradeAction} {trade.InstructionReference}"));
                lstTrades.AccessibleDescription = description;
                lstTrades.AccessibleName = $"Portfolio fund order trades; rows: {description}";
                _tradeDescriptionSource = trades;
            }
            var selectedTradeId = _lastTradeId ?? _viewModel.SelectedFundOrderTrade?.TradeId;
            _lastTradeId = null;
            ListViewItem? selectedItem = null;
            foreach (ListViewItem item in lstTrades.Items)
                if (item.Tag is PortfolioFundOrderTradeEditorModel trade && trade.TradeId == selectedTradeId)
                { selectedItem = item; break; }
            if (selectedItem is not null && !selectedItem.Selected)
                selectedItem.Selected = true;
        }
        finally
        {
            lstTrades.EndUpdate();
            _rendering = wasRendering;
        }
        if (lstTrades.Items.Count == 0)
        {
            _displayedTradeId = null;
            _ = ObserveAsync(ClearHostedBlotterAsync);
        }
        if (!wasRendering && lstTrades.SelectedIndices.Count > 0)
            _ = ObserveAsync(ShowSelectedTradeAsync);
    }
    void UpdateButtons()
    {
        Cursor.Current = _viewModel.IsBusy ? Cursors.WaitCursor : Cursors.Default;
        btnDeleteOrder.AccessibleName = _viewModel.SelectedFundOrder is { } selectedOrder
            ? $"Remove Order {selectedOrder.OrderId}" : "Remove Order";
        btnRemoveTrade.AccessibleName = _viewModel.SelectedFundOrder is { } tradeOrder
                                         && _viewModel.SelectedFundOrderTrade is { } selectedTrade
            ? $"Remove Trade {selectedTrade.TradeId} From Order {tradeOrder.OrderId}" : "Remove Trade";
        btnCreateFund.Enabled = false;
        btnLoadOrder.Enabled = _viewModel.CanLoadOrder;
        btnCreateOrder.Enabled = _viewModel.CanCreateOrder;
        btnDeleteOrder.Enabled = _viewModel.CanDeleteOrder;
        btnCompleteOrder.Enabled = _viewModel.CanCompleteOrder;
        btnAddTrade.Enabled = _viewModel.CanAddTrade;
        btnRemoveTrade.Enabled = _viewModel.CanRemoveTrade;
        btnChangeTradeState.Enabled = _viewModel.CanChangeTradeState && ddlTradeState.Items.Count > 0;
        ddlTradeState.Enabled = _viewModel.CanChangeTradeState && ddlTradeState.Items.Count > 0;
        btnEndOfDay.Enabled = _viewModel.SelectedPortfolio is not null && _viewModel.SelectedFundOrderTrade is not null;
        btnSubmitOrder.Enabled = _viewModel.CanSubmitOrder;
        cbLiveFeed.Enabled = _viewModel.CanUseLiveFeed;
        UpdateLiveFeedAppearance();
        btnOpenTrade.Visible = false;
        btnAddTrade.Visible = true;
        btnRemoveTrade.Visible = true;
        btnChangeTradeState.Visible = true;
        ddlTradeState.Visible = true;
        lblTradeStateTarget.Visible = true;
    }
    async Task ClearTradeOrderControlAsync()
    {
        var fund = _viewModel.SelectedFund;
        var fundOrder = _viewModel.SelectedFundOrder;
        var fundOrderTrade = _viewModel.SelectedFundOrderTrade;
        if (fund is null || fundOrder is null || fundOrderTrade is null)
            return;
        var fundId = fund.FundId;
        await ClearHostedBlotterAsync();
        if (_viewModel.SelectedFundOrderTrade?.TradeId != fundOrderTrade.TradeId) return;
        pnlTradeBlotter.SuspendLayout();
        try
        {
            var workflowControl = default(Control);
            var tradeType = fundOrderTrade!.TradeType;
            switch (tradeType)
            {
               case TradeType.ShortIronCondor:
               case TradeType.LongIronCondor:
                    var orderActionType = GetOrderActionType(fundOrderTrade.TradeState);
                    var valueDate = fundOrderTrade.RequestedTradeDate;
                    var baseContract = FindBaseContract(_viewModel.BaseContracts,
                        fundOrderTrade.BaseContractId, fundOrderTrade.BaseContractSymbol);
                   baseContract = baseContract ?? _viewModel.BaseContracts.ElementAt(0);
                   var viewModel = new IronCondorTradeOrderViewModel(
                       _appRoot,
                       valueDate,
                       fundId,
                       baseContract,
                       fundOrder!,
                       fundOrderTrade,
                       orderActionType,
                       _referenceDataService,
                       portfolioId: _viewModel.SelectedPortfolio?.PortfolioId ?? 0);
                   workflowControl = new IronCondorTradeOrderView(this, viewModel);
                   break;
               case TradeType.FuturesOutright:
               case TradeType.PutCreditSpread:
               case TradeType.PutDebitSpread:
               case TradeType.CallCreditSpread:
               case TradeType.CallDebitSpread:
                    var brokerBaseContract = FindBaseContract(_viewModel.BaseContracts,
                        fundOrderTrade.BaseContractId, fundOrderTrade.BaseContractSymbol)
                       ?? _viewModel.BaseContracts.FirstOrDefault()
                       ?? throw new InvalidOperationException(
                           $"No Futures contract is available for {fundOrderTrade.BaseContractSymbol}.");
                   var brokerViewModel = new BrokerManualTradeOrderViewModel(
                       _appRoot,
                       _viewModel.SelectedPortfolio?.PortfolioId ?? 0,
                       fundOrder!,
                       fundOrderTrade,
                       brokerBaseContract);
                   workflowControl = new BrokerManualTradeOrderView(brokerViewModel);
                   break;
            }
            if (workflowControl != null)
            {
                var blotter = new EsTradeBlotterControl(
                    _appRoot,
                    _viewModel.SelectedFund!,
                    fundOrder!,
                    fundOrderTrade,
                    _viewModel.SelectedPortfolio?.PortfolioId ?? 0,
                    historicalReadOnly: false,
                    workflowControl: workflowControl)
                {
                    Name = workflowControl.Name
                };
                blotter.SubmitOpeningRequested += async (_, _) =>
                    await SubmitTradeOrderAsync(OrderActionType.Open);
                blotter.SubmitClosingRequested += async (_, _) =>
                    await SubmitTradeOrderAsync(OrderActionType.Close);
                blotter.EndOfDayRequested += (_, eventArgs) => btnEndOfDay_Click(blotter, eventArgs);
                btnSubmitOrder.Visible = false;
                btnEndOfDay.Visible = false;
                pnlTradePosition.Controls.Remove(btnSubmitOrder);
                pnlTradePosition.Controls.Remove(btnEndOfDay);
                blotter.Dock = DockStyle.Fill;
                pnlTradeBlotter.Controls.Add(blotter);
            }
            if (lstTradeOrders.SelectedIndices.Count > 0)
            {
                btnAddTrade.Enabled = true;
                btnRemoveTrade.Enabled = false;
            }
            UpdateButtons();
        
        }
        finally { pnlTradeBlotter.ResumeLayout(true); }
    }

    Task ClearHostedBlotterAsync()
    {
        var oldControls = pnlTradeBlotter.Controls.Cast<Control>().ToArray();
        pnlTradeBlotter.Controls.Clear();
        var previous = _hostedCleanupTask;
        return _hostedCleanupTask = CloseDetachedControlsAsync(previous, oldControls);
    }

    async Task CloseDetachedControlsAsync(Task previous, Control[] oldControls)
    {
        try { await previous; }
        catch (Exception error)
        {
            if (!IsDisposed) this.ShowErrorMessage(error.Message, "Trade Blotter Cleanup Error");
        }
        foreach (var control in oldControls)
        {
            try
            {
                if (control is IAsyncFormControl asyncControl) await asyncControl.CloseAsync();
                else if (control is IFormControl formControl) formControl.Close();
            }
            finally { control.Dispose(); }
        }
    }

    static FuturesContractV3ReadModel? FindBaseContract(
        IReadOnlyList<FuturesContractV3ReadModel> contracts, string? contractId, string? symbol)
    {
        FuturesContractV3ReadModel? symbolMatch = null;
        foreach (var contract in contracts)
        {
            if (string.Equals(contract.ContractId, contractId, StringComparison.OrdinalIgnoreCase)) return contract;
            if (symbolMatch is null && string.Equals(contract.Symbol, symbol, StringComparison.OrdinalIgnoreCase))
                symbolMatch = contract;
        }
        return symbolMatch;
    }


    void EnableTradeButtons()
        => UpdateButtons();

    async void btnLoadOrder_Click(object sender, EventArgs e)
        => await ObserveAsync(LoadTradeOrderAsync);

    async Task LoadTradeOrderAsync()
    {
        var trade = _viewModel.SelectedFundOrderTrade;
        if (trade is null) return;
        switch (trade.TradeState)
        {
            case TradeState.TradeToOpen:
            case TradeState.TradeToClose:
                DialogResult = DialogResult.OK;
                Close();
                break;
            case TradeState.OrderFilled:
                var order = _viewModel.CanonicalOrders.Single(value => value.OrderId == trade.OrderId);
                await _viewModel.ChangeManualTradeStateAsync(order, trade.TradeId, TradeState.TradeToOpen);
                break;
            default:
                this.ShowErrorMessage($"Unable to load Trade Order {trade.OrderId}:{trade.TradeId} with Trade State: {trade.TradeState}", "Load Trade Order Error");
                break;
        }
    }
    async void ddlFund_SelectedIndexChanged(object sender, EventArgs e)
    {
        UpdateFundSelectorAccessibility();
        if (_rendering) return;
        if (ddlFund.SelectedIndex < 0) return;
        if (_viewModel.SelectFund(ddlFund.SelectedIndex))
        {
            await _viewModel.LoadCanonicalOrdersAsync();
            if (_viewModel.SelectedFundOrder is { } selectedOrder)
            {
                _lastTradeOrderId = selectedOrder.OrderId;
                await SelectOrderAsync(selectedOrder.OrderId);
            }
        }
        UpdateButtons();
    }

    void UpdateFundSelectorAccessibility()
        => ddlFund.AccessibleName = $"Trade fund selector; selected={ddlFund.SelectedItem}; "
            + $"catalog: {ddlFund.AccessibleDescription}";

    void ShowFundOrders()
    {
        _viewModel.SetOrderDateRange(dtpFrom.Value.AddMonths(-1), dtpTo.Value);
        RenderFundOrders();
        RenderTrades();
        UpdateButtons();
    }

    async void lstTradeOrders_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (_rendering || lstTradeOrders.SelectedItems.Count == 0)
            return;

        var selected = lstTradeOrders.SelectedItems[0].Tag as PortfolioFundOrderEditorModel;
        if (selected is null)
            return;

        _lastTradeOrderId = selected.OrderId;

        _displayedTradeId = null;
        await ClearHostedBlotterAsync();
        ddlOrderActionType.Enabled = false;
        txtDaysToExpiry.Visible = false;
        lblDaysToExpiry.Visible = false;
        await SelectOrderAsync(selected.OrderId);
    }

    async Task SelectOrderAsync(int orderId)
    {
        _orderSelectionCancellation?.Cancel();
        _orderSelectionCancellation?.Dispose();
        _orderSelectionCancellation = new CancellationTokenSource();
        var cancellationToken = _orderSelectionCancellation.Token;
        await ObserveAsync(() => _viewModel.SelectCanonicalOrderAsync(orderId, cancellationToken));
        if (!cancellationToken.IsCancellationRequested)
            UpdateButtons();
    }

    async void lstTrades_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (_rendering) return;
        await ObserveAsync(ShowSelectedTradeAsync);
    }

    async Task ShowSelectedTradeAsync()
    {
        if (_viewModel.FundOrders.Count > 0 && _viewModel.FundOrderTrades.Count > 0)
        {
            var selectedTrade = lstTrades.SelectedItems.Count > 0
                ? lstTrades.SelectedItems[0].Tag as PortfolioFundOrderTradeEditorModel
                : _viewModel.SelectedFundOrderTrade;
            var index = -1;
            if (selectedTrade is not null)
                for (var candidate = 0; candidate < _viewModel.FundOrderTrades.Count; candidate++)
                    if (_viewModel.FundOrderTrades[candidate].TradeId == selectedTrade.TradeId)
                    { index = candidate; break; }
            if (index < 0) return;
            _lastTradeId = selectedTrade!.TradeId;
            _viewModel.SelectFundOrderTrade(index);
            await _viewModel.RefreshSelectedTradeFillEvidenceAsync();
            var trade = _viewModel.GetFundOrderTrade(index)!;
            LoadTradeStateTargets(trade.TradeState);
            foreach (var control in new Control[] { dtpTradeDate, ddlOrderActionType })
                control.Enabled = trade.TradeState == TradeState.NewTrade;
            txtTradeType.Text = trade.TradeType.ToString();
            dtpTradeDate.Value = trade.RequestedTradeDate.ToDateTime(TimeOnly.MinValue);
            txtDaysToExpiry.Visible = true;
            lblDaysToExpiry.Visible = true;
            if (_displayedTradeId != trade.Id)
            {
                _displayedTradeId = trade.Id;
                await ClearTradeOrderControlAsync();
            }
        }
        UpdateButtons();
    }
    async void btnCreateOrder_Click(object sender, EventArgs e)
    {
        var fundId = _viewModel.GetFundId(ddlFund.SelectedIndex);
        var vm = new FundOrderEditorViewModel(
            fundId,
            _referenceDataService,
            allocateOrderId: false);
        var dlg = new CreateFundOrderForm();
        dlg.SetViewModel(vm);
        if (dlg.ShowDialog() == DialogResult.OK)
            await ObserveAsync(async () =>
            {
                await _viewModel.CreateManualOrderAsync(dlg.FundOrder);
                RenderFundOrders();
                RenderTrades();
                UpdateButtons();
            });
    }

    void ddlLiveFeed_SelectedIndexChanged(object sender, EventArgs e)
    {

    }

    void lstTrades_Enter(object sender, EventArgs e)
        => EnableTradeButtons();

    void lstTrades_Leave(object sender, EventArgs e)
        => EnableTradeButtons();

    async void btnAddTrade_Click(object sender, EventArgs e)
    {
        if (lstTradeOrders.SelectedItems.Count == 0)
            return;

        if (lstTradeOrders.SelectedItems[0].Tag is not PortfolioFundOrderEditorModel fundOrder)
        {
            this.ShowErrorMessage(
                "The selected Portfolio order is unavailable. Reload the order list and try again.",
                "Add Trade Error");
            return;
        }

        var canonical = _viewModel.CanonicalOrders.FirstOrDefault(order => order.OrderId == fundOrder.OrderId);
        if (canonical is null)
        {
            this.ShowErrorMessage(
                "The selected Portfolio order changed. Reload the order list and try again.",
                "Add Trade Error");
            return;
        }

        var dlg = new CreateFundOrderTradeForm();
        dlg.SetViewModel(_viewModel);
        dlg.SetFundOrder(fundOrder);
        if (dlg.ShowDialog() == DialogResult.OK)
        {
            var fundOrderTrade = dlg.FundOrderTrade with
            {
                PortfolioId = canonical.PortfolioId,
                FundId = canonical.FundId,
                OrderId = canonical.OrderId,
                PrimaryTrade = fundOrder.Trades.Length == 0
            };
            _lastTradeOrderId = canonical.OrderId;
            _lastTradeId = fundOrderTrade.TradeId;
            await ObserveAsync(() => _viewModel.AddManualTradeAsync(canonical, fundOrderTrade));
        }
    }
    void ShowTradeEditorUnavailable(string message)
    {
        _ = ObserveAsync(ClearHostedBlotterAsync);
        pnlTradeBlotter.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(48, 48, 48),
            ForeColor = Color.White,
            Font = new Font("Microsoft Sans Serif", 12F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter,
            AccessibleName = message,
            Text = message,
        });
    }

    static OrderActionType GetOrderActionType(TradeState tradeState)
        => tradeState == TradeState.TradeToClose ? OrderActionType.Close : OrderActionType.Open;

    async void btnRemoveTrade_Click(object sender, EventArgs e)
    {
        if (_viewModel.SelectedFundOrder is not { } selectedOrder
            || _viewModel.SelectedFundOrderTrade is not { } selectedTrade) return;
        var canonical = _viewModel.CanonicalOrders.Single(order => order.OrderId == selectedOrder.OrderId);
        await ObserveAsync(() => _viewModel.RemoveManualTradeAsync(canonical, selectedTrade.TradeId));
    }
    void btnClearTrade_Click(object sender, EventArgs e)
        => _ = ObserveAsync(ClearTradeOrderControlAsync);

    async void btnSubmitOrder_Click(object sender, EventArgs e)
        => await SubmitTradeOrderAsync((OrderActionType)Enum.Parse(
            typeof(OrderActionType), ddlOrderActionType.SelectedItem!.ToString()!));

    async Task SubmitTradeOrderAsync(OrderActionType orderActionType)
    {
        if (Interlocked.Exchange(ref _submissionInProgress, 1) != 0)
            return;
        try
        {
            if (!_viewModel.ValidateOrderSubmission(orderActionType))
                return;
            var tradeOrderControl = pnlTradeBlotter.Controls.OfType<ITradeOrderControl>().SingleOrDefault();
            if (tradeOrderControl is null)
                return;
            var orderConfirmation = new WinFormsTradeOrderConfirmationService(this);
            await ObserveAsync(async () =>
            {
                var commandId = await tradeOrderControl.SubmitOrderAsync(
                    DateOnly.FromDateTime(dtpTradeDate.Value),
                    orderActionType,
                    orderConfirmation);
            });
        }
        finally
        {
            Volatile.Write(ref _submissionInProgress, 0);
        }
    }

    void dtpTradeDate_ValueChanged(object sender, EventArgs e)
    {
        if (pnlTradeBlotter.Controls.Count > 0)
        {
            var tradeOrderControl = pnlTradeBlotter.Controls[0] as ITradeOrderControl;
            txtDaysToExpiry.Text =$"{ tradeOrderControl!.MaturityDate.DayNumber - DateOnly.FromDateTime(dtpTradeDate.Value).DayNumber }";
        }
    }

    async void btnCancelOrder_Click(object sender, EventArgs e)
    {
        if (_viewModel.SelectedFundOrder is not { } selectedOrder)
            return;
        var confirmation = MessageBox.Show(
            this,
            $"Delete draft Portfolio order {selectedOrder.OrderId} and its economically inactive trade?",
            "Remove Portfolio Order",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);
        if (confirmation != DialogResult.Yes)
            return;
        var canonical = _viewModel.CanonicalOrders.Single(order => order.OrderId == selectedOrder.OrderId);
        await ObserveAsync(() => _viewModel.DeleteManualOrderAsync(canonical, "Deleted by operator."));
    }
    void btnNearestStrikes_Click(object sender, EventArgs e)
    {
        var tradeOrderControl = pnlTradeBlotter.Controls[0] as ITradeOrderControl;
        tradeOrderControl?.SetNearestStrikePrices();
    }

    void btnEndOfDay_Click(object sender, EventArgs e)
    {
        var portfolio = _viewModel.SelectedPortfolio;
        var trade = _viewModel.SelectedFundOrderTrade;
        if (portfolio is null || trade is null)
            return;
        var baseContract = _viewModel.BaseContracts.FirstOrDefault(value =>
            string.Equals(value.Symbol, trade.BaseContractSymbol, StringComparison.OrdinalIgnoreCase));
        if (baseContract is null)
        {
            this.ShowErrorMessage(
                $"The base contract for {trade.BaseContractSymbol} is unavailable.",
                "End Of Day Process");
            return;
        }
        var strategyKind = trade.TradeType switch
        {
            TradeType.ShortIronCondor or TradeType.LongIronCondor => TradeStrategyKind.IronCondor,
            TradeType.PutCreditSpread or TradeType.PutDebitSpread or
                TradeType.CallCreditSpread or TradeType.CallDebitSpread => TradeStrategyKind.VerticalSpread,
            _ => TradeStrategyKind.Unknown
        };
        if (strategyKind == TradeStrategyKind.Unknown)
        {
            this.ShowErrorMessage(
                $"End-of-day processing is not available for {trade.TradeType}.",
                "End Of Day Process");
            return;
        }
        using var dialog = new TradeEndOfDayForm(_appRoot, new TradeEndOfDayParameter
        {
            PortfolioId = portfolio.PortfolioId,
            FundId = trade.FundId,
            OrderId = trade.OrderId,
            TradeId = trade.TradeId,
            TradeType = trade.TradeType,
            StrategyKind = strategyKind,
            BaseContractId = baseContract.ContractId,
            ValueDate = DateOnly.FromDateTime(dtpTradeDate.Value)
        });
        dialog.ShowDialog(this);
    }

    async void btnChangeTradeState_Click(object sender, EventArgs e)
    {
        var trade = _viewModel.SelectedFundOrderTrade;
        if (trade is null || ddlTradeState.SelectedItem is null)
            return;
        if (!Enum.TryParse<TradeState>(ddlTradeState.SelectedItem.ToString(), out var targetState))
            return;
        var order = _viewModel.CanonicalOrders.Single(value => value.OrderId == trade.OrderId);
        await ObserveAsync(() => _viewModel.ChangeManualTradeStateAsync(order, trade.TradeId, targetState));
    }

    void LoadTradeStateTargets(TradeState currentState)
    {
        ddlTradeState.Items.Clear();
        foreach (var state in Enum.GetValues<TradeState>().Where(state => state != currentState))
            ddlTradeState.Items.Add(state.ToStringFast());
        var preferred = currentState == TradeState.NewTrade ? TradeState.OrderSubmitted.ToStringFast() : null;
        ddlTradeState.SelectedIndex = preferred is null
            ? (ddlTradeState.Items.Count > 0 ? 0 : -1)
            : ddlTradeState.Items.IndexOf(preferred);
        UpdateTradeStateSelectorAccessibility();
    }

    void ddlTradeState_SelectedIndexChanged(object? sender, EventArgs e)
        => UpdateTradeStateSelectorAccessibility();

    void UpdateTradeStateSelectorAccessibility()
    {
        ddlTradeState.AccessibleDescription = string.Join(", ", ddlTradeState.Items.Cast<object>());
        ddlTradeState.AccessibleName = $"Trade state selector; selected={ddlTradeState.SelectedItem}; "
            + $"catalog: {ddlTradeState.AccessibleDescription}";
    }

    void btnCreateFund_Click(object sender, EventArgs e)
        => this.ShowErrorMessage("Create Portfolio Funds from Portfolio Administration.", "Portfolio Fund");

    async void ddlOrderActionType_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (pnlTradeBlotter.Controls.Count == 0) return;
        var orderActionType = Enum.Parse<OrderActionType>(ddlOrderActionType.SelectedItem!.ToString()!);
        _viewModel.OrderActionType = orderActionType;   
        var tradeOrderControl = pnlTradeBlotter.Controls[0] as ITradeOrderControl;
        if (tradeOrderControl is not null)
            await ObserveAsync(() => tradeOrderControl.OrderActionTypeChangedAsync(orderActionType));
    }

    void dtpFrom_ValueChanged(object sender, EventArgs e)
    {
        if (!dtpFrom.Enabled) return;
        dtpTo.Value = new DateTime(dtpFrom.Value.Year, dtpFrom.Value.Month, DateTime.DaysInMonth(dtpFrom.Value.Year, dtpFrom.Value.Month), 23,59,59);
    }

    void dtpTo_ValueChanged(object sender, EventArgs e)
    {
        if (!dtpTo.Enabled) return;
        ShowFundOrders();
    }

    async void btnCloseOrder_Click(object sender, EventArgs e)
    {
        if (_viewModel.SelectedFundOrder is not { } selectedOrder) return;
        var canonical = _viewModel.CanonicalOrders.Single(order => order.OrderId == selectedOrder.OrderId);
        await ObserveAsync(() => _viewModel.CloseManualOrderAsync(canonical, "Closed by operator."));
    }
    async void lstTradeOrders_DoubleClick(object sender, EventArgs e)
        => await ObserveAsync(LoadTradeOrderAsync);

    void pnlTradePosition_Paint(object sender, PaintEventArgs e)
    {

    }

    void TradeOrderEditorForm_FormClosed(object sender, FormClosedEventArgs e)
    {

    }

    /// <summary>Implements the legacy form-control open contract.</summary>
    /// <exception cref="NotImplementedException">This form is opened through its normal WinForms lifecycle.</exception>
    public void Open()
    {
        throw new NotImplementedException();
    }

    void IFormControl.Resize(Control parentControl)
    {
        throw new NotImplementedException();
    }

    async void cbLiveFeed_CheckedChanged(object sender, EventArgs e)
    {
        UpdateLiveFeedAppearance();

        var tradeOrderControl = pnlTradeBlotter.Controls[0] as ITradeOrderControl;
        await ObserveAsync(() => tradeOrderControl!.SetLiveFeedAsync(cbLiveFeed.Checked));
    }

    void UpdateLiveFeedAppearance()
    {
        cbLiveFeed.BackColor = cbLiveFeed.Checked switch
        {
            _ when !cbLiveFeed.Enabled => Color.FromArgb(74, 74, 74),
            true => Color.FromArgb(38, 142, 65),
            false => Color.FromArgb(150, 42, 42)
        };
        cbLiveFeed.ForeColor = Color.Black;
    }

    async Task ObserveAsync(Func<Task> operation)
    {
        try
        {
            await operation();
        }
        catch (OperationCanceledException)
        {
            // A newer order selection superseded this one.
        }
        catch (UiServiceOperationException)
        {
            // The ViewModel publishes coded failures through LastError.
        }
        catch (Exception exception)
        {
            this.ShowErrorMessage(exception.Message, "Trade Order Editor Error");
        }
    }
}
