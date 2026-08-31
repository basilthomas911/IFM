using TomasAI.IFM.Domain.Trade.Shared;
using System.Data;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.UI.Net.Views.Trade.IronCondor;
using TomasAI.IFM.UI.Net.ViewModels.Fund;
using TomasAI.IFM.UI.Net.ViewModels.Trade;
using TomasAI.IFM.UI.Net.ViewModels.Trade.IronCondor;
using TomasAI.IFM.Domain.Fund.Shared;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;
using TomasAI.IFM.Domain.Fund.Shared.Events;
using System.ComponentModel;
using TomasAI.IFM.UI.Net.ViewModels.Presentation;
using TomasAI.IFM.UI.Net.ViewModels.Operations;
using TomasAI.IFM.UI.Net.Models;
using TomasAI.IFM.UI.Net.Services.Reference;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;

namespace TomasAI.IFM.UI.Net.Views.Trade;

public partial class TradeOrderEditorForm 
    : Form, IForm<TradeOrderEditorForm>, IFormControl
{
    readonly IAppRoot _appRoot;
    readonly IReferenceDataService _referenceDataService;
    TradeOrderEditorViewModel _viewModel = null!;
    int _lastTradeIndex;
    int _lastTradeOrderIndex;
    FundOrderTradeId? _displayedTradeId;
    long _lastErrorSequence;
    long _lastChangeSequence;
    bool _rendering;
    readonly ComboBox _portfolioSelector = new() { Name = "ddlPortfolio", AccessibleName = "Portfolio selector", DropDownStyle = ComboBoxStyle.DropDownList, BackColor = Color.FromArgb(64, 64, 64), ForeColor = Color.White, Font = new Font("Microsoft Sans Serif", 12F), Location = new Point(93, 8), Size = new Size(720, 28) };
    readonly ComboBox _sourceFilter = new() { Name = "ddlCompositionSource", AccessibleName = "Composition source filter", DropDownStyle = ComboBoxStyle.DropDownList, BackColor = Color.FromArgb(64, 64, 64), ForeColor = Color.White, Font = new Font("Microsoft Sans Serif", 12F), Location = new Point(1010, 8), Size = new Size(220, 28) };
    readonly ComboBox _historyModeSelector = new() { Name = "ddlTradeHistoryMode", AccessibleName = "Trade history mode", DropDownStyle = ComboBoxStyle.DropDownList, BackColor = Color.FromArgb(64, 64, 64), ForeColor = Color.White, Font = new Font("Microsoft Sans Serif", 10F), Location = new Point(1300, 8), Size = new Size(140, 28) };
    bool _canonicalOrderSelected;
    bool _legacyOrderSelected;
    LegacyFundOrderHistoryReadModel? _selectedLegacyOrder;
    Control? _embeddedLegacyTradeEditor;
    long _legacyViewerGeneration;

    /// <summary>
    /// create trade order form
    /// </summary>
    /// <param name="appRoot"></param>
    public TradeOrderEditorForm(
        IAppRoot appRoot,
        IReferenceDataService referenceDataService)
    {
        InitializeComponent();
        ConfigurePortfolioScope();
        ConfigureCompactLayout();
        ddlTradeState.SelectedIndexChanged += ddlTradeState_SelectedIndexChanged;
        _appRoot = appRoot;
        _referenceDataService = referenceDataService;
    }

    public FundReadModel Fund => _viewModel?.SelectedFund!;

    public FundOrderReadModel FundOrder => _viewModel?.SelectedFundOrder!;

    public FundOrderTradeReadModel FundOrderTrade => _viewModel?.SelectedFundOrderTrade!;

    /// <summary>Gets the read-only historical trade accepted for the main-screen legacy tab.</summary>
    public LegacyFundTradeHistoryReadModel? LegacyTradeHistory { get; private set; }

    /// <summary>Gets the source legacy Fund selected with <see cref="LegacyTradeHistory"/>.</summary>
    public FundReadModel? LegacyFund { get; private set; }

    /// <summary>Gets the source legacy FundOrder selected with <see cref="LegacyTradeHistory"/>.</summary>
    public FundOrderReadModel? LegacyFundOrder { get; private set; }

    /// <summary>
    /// load view model
    /// </summary>
    /// <param name="viewModel"></param>
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
        ddlFund.Location = new Point(93, 58); lblFundSelector.Location = new Point(29, 64);
        btnCreateFund.Visible = false; btnCreateFund.Enabled = false; pnlFundSelector.Controls.Remove(btnCreateFund);
        pnlFundSelector.Controls.Add(new Label { Text = "Portfolio:", AutoSize = true, ForeColor = Color.White, Font = new Font("Microsoft Sans Serif", 12F), Location = new Point(8, 14) });
        pnlFundSelector.Controls.Add(_portfolioSelector);
        pnlFundSelector.Controls.Add(new Label { Text = "Source:", AutoSize = true, ForeColor = Color.White, Font = new Font("Microsoft Sans Serif", 12F), Location = new Point(935, 14) });
        _sourceFilter.Items.AddRange(["All", "Manual", "Strategy Workflow"]); _sourceFilter.SelectedIndex = 0; pnlFundSelector.Controls.Add(_sourceFilter);
        pnlFundSelector.Controls.Add(new Label { Text = "Mode:", AutoSize = true, ForeColor = Color.White, Font = new Font("Microsoft Sans Serif", 10F), Location = new Point(1240, 14) });
        _historyModeSelector.Items.AddRange(["Current", "Legacy History"]); _historyModeSelector.SelectedIndex = 0; pnlFundSelector.Controls.Add(_historyModeSelector);
        _portfolioSelector.SelectedIndexChanged += async (_, _) => { if (_rendering) return; await _viewModel.SelectPortfolioAsync(_portfolioSelector.SelectedIndex); RenderEditor(); };
        _sourceFilter.SelectedIndexChanged += (_, _) => RenderFundOrders();
        _historyModeSelector.SelectedIndexChanged += async (_, _) =>
        {
            if (_rendering || _viewModel is null) return;
            Interlocked.Increment(ref _legacyViewerGeneration);
            await CloseEmbeddedLegacyTradeEditorAsync();
            var legacy = _historyModeSelector.SelectedIndex == 1;
            if (legacy)
            {
                dtpFrom.Value = new DateTime(2000, 1, 1);
                dtpTo.Value = EasternTime.GetNow(TimeProvider.System).Date.AddDays(1);
            }
            _viewModel.SetOrderDateRange(dtpFrom.Value, dtpTo.Value);
            await _viewModel.SetLegacyHistoryModeAsync(legacy);
            _sourceFilter.Enabled = !legacy;
            RenderEditor();
        };
        btnOpenTrade.Text = "View Legacy Trade...";
        btnOpenTrade.AccessibleName = "View selected legacy trade in the main read-only trade tab";
        panel1.Controls.Remove(btnOpenTrade);
        pnlTrades.Controls.Add(btnOpenTrade);
        btnOpenTrade.Location = new Point(1260, 12);
        btnOpenTrade.Visible = false;
        btnOpenTrade.Click += (_, _) => AcceptLegacyTradeSelection();
        lstTrades.DoubleClick += (_, _) =>
        {
            if (_viewModel.IsLegacyHistoryMode) AcceptLegacyTradeSelection();
        };
        if (lstTradeOrders.Columns.Count == 4) lstTradeOrders.Columns.Add("Source", 150);
    }

    void ConfigureCompactLayout()
    {
        MinimumSize = new Size(1200, 780);
        pnlTradeOrders.Height = 270;
        lstTradeOrders.Height = pnlTradeOrders.ClientSize.Height - lstTradeOrders.Top - 6;
        lstTradeOrders.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        lstTrades.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        pnlTradeControl.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        foreach (var button in new[] { btnLoadOrder, btnCreateOrder, btnDeleteOrder, btnCompleteOrder,
            btnAddTrade, btnRemoveTrade, btnChangeTradeState, btnOpenTrade, btnSubmitOrder, btnEndOfDay })
            button.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        pnlTradePosition.Resize += (_, _) =>
        {
            pnlTradeControl.Height = Math.Max(80, pnlTradePosition.ClientSize.Height - pnlTradeControl.Top - 10);
            pnlTradeControl.Width = Math.Max(300, pnlTradePosition.ClientSize.Width - pnlTradeControl.Left - 211);
        };
    }

    void ViewModelPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
        => this.Post(() =>
        {
            if (eventArgs.PropertyName == nameof(TradeOrderEditorViewModel.LastError))
                ShowLatestError();
            if (eventArgs.PropertyName == nameof(TradeOrderEditorViewModel.LastChange))
                HandleLatestChange();
            if (eventArgs.PropertyName is nameof(TradeOrderEditorViewModel.Funds)
                or nameof(TradeOrderEditorViewModel.FundOrders)
                or nameof(TradeOrderEditorViewModel.FundOrderTrades)
                or nameof(TradeOrderEditorViewModel.Portfolios)
                or nameof(TradeOrderEditorViewModel.CanonicalOrders)
                or nameof(TradeOrderEditorViewModel.LegacyOrders)
                or nameof(TradeOrderEditorViewModel.LegacyTrades)
                or nameof(TradeOrderEditorViewModel.IsLegacyHistoryMode))
                RenderEditor();
            else
                UpdateButtons();
        });

    void ShowLatestError()
    {
        var error = _viewModel.LastError;
        if (error is null || error.Sequence <= _lastErrorSequence)
            return;
        _lastErrorSequence = error.Sequence;
        this.ShowErrorMessage(error.Message, error.Caption);
    }

    async void HandleLatestChange()
    {
        var change = _viewModel.LastChange;
        if (change is null || change.Sequence <= _lastChangeSequence)
            return;
        _lastChangeSequence = change.Sequence;
        try
        {
            switch (change.Event)
            {
                case TradeAddedToFundOrderCompleteEvent added:
                    if (cbLiveFeed.Checked)
                        await _viewModel.AddTradeLiveFeed(added.FundOrderTrade.OrderId, added.FundOrderTrade.TradeId);
                    break;
                case TradeRemovedFromFundOrderCompleteEvent removed:
                    if (cbLiveFeed.Checked)
                        await _viewModel.RemoveTradeLiveFeed(removed.FundOrderTradeId.OrderId, removed.FundOrderTradeId.TradeId);
                    break;
            }
        }
        catch (UiServiceOperationException)
        {
            // The ViewModel publishes coded live-feed failures through LastError.
        }
        catch (Exception exception)
        {
            this.ShowErrorMessage(exception.Message, "Trade Order Editor Error");
        }
    }

    public void SetOrderAction(OrderActionType orderActionType)
    {
        ddlOrderActionType.SelectedItem = $"{orderActionType}";
    }

    public void SetTradeDate(DateOnly tradeDate)
        => dtpTradeDate.Value = tradeDate.ToDateTime(TimeOnly.MinValue);

    public void SetDaysToExpiry(DateOnly maturityDate)
        => txtDaysToExpiry.Text = $"{maturityDate.DayNumber - DateOnly.FromDateTime(dtpTradeDate.Value).DayNumber}";

    async void TradeOrderForm_Load(object sender, EventArgs e)
    {
        _lastTradeIndex = -1;
        _lastTradeOrderIndex = -1;
        
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
        Interlocked.Increment(ref _legacyViewerGeneration);
        await CloseEmbeddedLegacyTradeEditorAsync();
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= ViewModelPropertyChanged;
            await _viewModel.StopFundOrderListener();
        }
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
        Interlocked.Increment(ref _legacyViewerGeneration);
        await CloseEmbeddedLegacyTradeEditorAsync();
        _lastTradeIndex = -1;
        _lastTradeOrderIndex = -1;
        _displayedTradeId = null;
        pnlTradeControl.Controls.Clear();
        await _viewModel.LoadFunds();
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
        try
        {
            lstTradeOrders.Items.Clear();
            if (_viewModel.IsLegacyHistoryMode)
            {
                foreach (var history in _viewModel.LegacyOrders)
                {
                    var order = history.Order;
                    lstTradeOrders.Items.Add(new ListViewItem([
                        $"{order.OrderId}", LegacyDate(order.TradeDate), $"{order.OrderStatus}",
                        order.Reference ?? string.Empty, "Legacy"
                    ]) { Tag = history });
                }
                lstTradeOrders.AccessibleDescription = string.Join(" || ", _viewModel.LegacyOrders.Select(x =>
                    $"{x.Order.OrderId} | {LegacyDate(x.Order.TradeDate)} | {x.Order.OrderStatus} | Legacy"));
                lstTradeOrders.AccessibleName = $"Legacy fund orders; rows: {lstTradeOrders.AccessibleDescription}";
                return;
            }
            foreach (var fundOrder in _viewModel.FundOrders)
            {
                if (_sourceFilter.SelectedItem?.ToString() == "Strategy Workflow") continue;
                var item = new ListViewItem([
                    $"{fundOrder.OrderId}",
                    $"{EasternTime.FromUtc(fundOrder.OrderDate):yyyy-MMM-dd}",
                    $"{fundOrder.OrderStatus}",
                    fundOrder.Reference ?? string.Empty,
                    "Manual"
                ]) { Tag = fundOrder };
                lstTradeOrders.Items.Add(item);
            }
            foreach (var order in _viewModel.CanonicalOrders)
            {
                var source = order.Origin == CompositionOrigin.ManualUi ? "Manual" : "Strategy Workflow";
                var filter = _sourceFilter.SelectedItem?.ToString() ?? "All";
                if (filter != "All" && filter != source) continue;
                var reference = source == "Manual" ? order.OperatorReference : order.WorkflowId.ToString("N");
                lstTradeOrders.Items.Add(new ListViewItem([$"{order.OrderId}", $"{EasternTime.FromUtc(order.CreatedOnUtc):yyyy-MMM-dd}", order.Status, reference, source]) { Tag = order });
            }
            lstTradeOrders.AccessibleDescription = string.Join(" || ", _viewModel.FundOrders.Select(fundOrder =>
                $"{fundOrder.OrderId} | {EasternTime.FromUtc(fundOrder.OrderDate):yyyy-MMM-dd} | "
                + $"{fundOrder.OrderStatus} | {fundOrder.Reference ?? string.Empty}"));
            lstTradeOrders.AccessibleName = $"Fund orders; rows: {lstTradeOrders.AccessibleDescription}";
            var index = _lastTradeOrderIndex >= 0 ? _lastTradeOrderIndex : _viewModel.FundOrderSelectedIndex;
            _lastTradeOrderIndex = -1;
            if (index >= 0 && index < lstTradeOrders.Items.Count)
                lstTradeOrders.Items[index].Selected = true;
        }
        finally
        {
            _rendering = wasRendering;
        }
    }

    void RenderTrades()
    {
        var wasRendering = _rendering;
        _rendering = true;
        try
        {
            lstTrades.Items.Clear();
            ddlTradeState.Items.Clear();
            if (_viewModel.IsLegacyHistoryMode)
            {
                foreach (var history in _viewModel.LegacyTrades)
                {
                    var trade = history.Composition;
                    lstTrades.Items.Add(new ListViewItem([
                        $"{trade.TradeId}", $"{trade.TradeType}", LegacyDate(trade.TradeDate),
                        LegacyDate(trade.MaturityDate), $"{trade.TradeState}",
                        $"{history.MatchStatus}; fills={history.FillCount}; positions={history.PositionCount}"
                    ]) { Tag = history });
                }
                lstTrades.AccessibleDescription = string.Join(" || ", _viewModel.LegacyTrades.Select(x =>
                    $"{x.Composition.TradeId} | {x.Composition.TradeType} | {x.MatchStatus} | fills {x.FillCount} | positions {x.PositionCount}"));
                lstTrades.AccessibleName = $"Legacy fund order trades; rows: {lstTrades.AccessibleDescription}";
                return;
            }
            foreach (var trade in _viewModel.FundOrderTrades)
            {
                lstTrades.Items.Add(new ListViewItem([
                    $"{trade.TradeId}",
                    $"{trade.TradeType}",
                    $"{trade.TradeDate:yyyy-MMM-dd}",
                    $"{trade.MaturityDate:yyyy-MMM-dd}",
                    $"{trade.TradeState}",
                    $"{trade.TradeAction} {trade.Reference}"
                ]));
            }
            lstTrades.AccessibleDescription = string.Join(" || ", _viewModel.FundOrderTrades.Select(trade =>
                $"{trade.TradeId} | {trade.TradeType} | {trade.TradeDate:yyyy-MMM-dd} | "
                + $"{trade.MaturityDate:yyyy-MMM-dd} | {trade.TradeState} | {trade.TradeAction} {trade.Reference}"));
            lstTrades.AccessibleName = $"Fund order trades; rows: {lstTrades.AccessibleDescription}";
            var index = _lastTradeIndex >= 0 ? _lastTradeIndex : _viewModel.FundOrderTradeSelectedIndex;
            _lastTradeIndex = -1;
            if (index >= 0 && index < lstTrades.Items.Count)
                lstTrades.Items[index].Selected = true;
        }
        finally
        {
            _rendering = wasRendering;
        }
        if (lstTrades.Items.Count == 0)
        {
            _displayedTradeId = null;
            pnlTradeControl.Controls.Clear();
        }
        if (!wasRendering && lstTrades.SelectedIndices.Count > 0)
            _ = ObserveAsync(ShowSelectedTradeAsync);
    }

    void UpdateButtons()
    {
        Cursor.Current = _viewModel.IsBusy ? Cursors.WaitCursor : Cursors.Default;
        var readOnlyHistory = _canonicalOrderSelected || _legacyOrderSelected || _viewModel.IsLegacyHistoryMode;
        btnDeleteOrder.AccessibleName = _viewModel.SelectedFundOrder is { } selectedOrder
            ? $"Delete Order {selectedOrder.OrderId}"
            : "Delete Order";
        btnRemoveTrade.AccessibleName = _viewModel.SelectedFundOrder is { } tradeOrder
                                        && _viewModel.SelectedFundOrderTrade is { } selectedTrade
            ? $"Remove Trade {selectedTrade.TradeId} From Order {tradeOrder.OrderId}"
            : "Remove Trade";
        btnCreateFund.Enabled = false;
        btnLoadOrder.Enabled = !readOnlyHistory && _viewModel.CanLoadOrder;
        btnCreateOrder.Enabled = !readOnlyHistory && _viewModel.CanCreateOrder;
        btnDeleteOrder.Enabled = !readOnlyHistory && _viewModel.CanDeleteOrder;
        btnCompleteOrder.Enabled = !readOnlyHistory && _viewModel.CanCompleteOrder;
        btnAddTrade.Enabled = !readOnlyHistory && _viewModel.CanAddTrade;
        btnRemoveTrade.Enabled = !readOnlyHistory && _viewModel.CanRemoveTrade;
        btnChangeTradeState.Enabled = !readOnlyHistory && _viewModel.CanChangeTradeState && ddlTradeState.Items.Count > 0;
        ddlTradeState.Enabled = !readOnlyHistory && _viewModel.CanChangeTradeState && ddlTradeState.Items.Count > 0;
        btnEndOfDay.Enabled = !readOnlyHistory && _viewModel.CanEndOfDay;
        btnSubmitOrder.Enabled = !readOnlyHistory && _viewModel.CanSubmitOrder;
        cbLiveFeed.Enabled = !readOnlyHistory && _viewModel.CanUseLiveFeed;
        btnOpenTrade.Enabled = _legacyOrderSelected && lstTrades.SelectedItems.Count > 0;
        btnOpenTrade.Visible = _viewModel.IsLegacyHistoryMode;
        btnAddTrade.Visible = !_viewModel.IsLegacyHistoryMode;
        btnRemoveTrade.Visible = !_viewModel.IsLegacyHistoryMode;
        btnChangeTradeState.Visible = !_viewModel.IsLegacyHistoryMode;
        ddlTradeState.Visible = !_viewModel.IsLegacyHistoryMode;
        lblTradeStateTarget.Visible = !_viewModel.IsLegacyHistoryMode;
    }

    void ClearTradeOrderControl()
    {
        var fundId = _viewModel.Funds.ElementAt(ddlFund.SelectedIndex).FundId;
        var fundOrder = _viewModel.GetFundOrder(lstTradeOrders.SelectedItems[0].Index);
        var fundOrderTrade = _viewModel.GetFundOrderTrade(lstTrades.SelectedIndices.Count > 0 ? lstTrades.SelectedIndices[0] : 0);
        pnlTradeControl.Draw(() =>
        {
            var tradeControl = default(Control);
            pnlTradeControl.Controls.Clear();
            var tradeType = fundOrderTrade!.TradeType;
            switch (tradeType)
            {
               case TradeType.ShortIronCondor:
               case TradeType.LongIronCondor:
                    var orderActionType = GetOrderActionType(fundOrderTrade.TradeState);
                    var valueDate = orderActionType == OrderActionType.Open
                        ? fundOrder!.TradeDate
                        : fundOrderTrade!.TradeDate;
                    var baseContract = _viewModel.BaseContracts.Where(e => e.Symbol == fundOrderTrade.BaseContractSymbol).FirstOrDefault();
                   baseContract = baseContract ?? _viewModel.BaseContracts.ElementAt(0);
                   var viewModel = new IronCondorTradeOrderViewModel(
                       _appRoot,
                       valueDate,
                       fundId,
                       baseContract,
                       fundOrder!,
                       fundOrderTrade,
                       orderActionType,
                       _referenceDataService);
                   tradeControl = new IronCondorTradeOrderView(this, viewModel);
                   break;
            }
            if (tradeControl != null)
            {
                tradeControl.Dock = DockStyle.Fill;
                pnlTradeControl.Controls.Add(tradeControl);
            }
            if (lstTradeOrders.SelectedIndices.Count > 0)
            {
                btnAddTrade.Enabled = true;
                btnRemoveTrade.Enabled = false;
            }
            UpdateButtons();
        
        });
        return;

    }


    void EnableTradeButtons()
    {
        var enabled = lstTrades.SelectedIndices.Count > 0;
        btnRemoveTrade.Enabled = enabled;
    }

    async void btnLoadOrder_Click(object sender, EventArgs e)
        => await ObserveAsync(LoadTradeOrderAsync);

    async Task LoadTradeOrderAsync()
    {
        if (_viewModel.IsLegacyHistoryMode)
        {
            AcceptLegacyTradeSelection();
            return;
        }
        if (lstTradeOrders.SelectedIndices.Count > 0 && lstTrades.SelectedIndices.Count > 0)
        {
            var fundOrderTrade = _viewModel.GetFundOrderTrade(lstTrades.SelectedIndices[0]);
            switch (fundOrderTrade!.TradeState)
            {
                case TradeState.TradeToOpen:
                case TradeState.TradeToClose:
                    DialogResult = DialogResult.OK;
                    Close();
                    break;
                case TradeState.OrderFilled:
                    var fundId = _viewModel.Funds.ElementAt(ddlFund.SelectedIndex).FundId;
                    var fundOrderTradeId = new FundOrderTradeId(fundId, fundOrderTrade.OrderId, fundOrderTrade.TradeId);
                    await _viewModel.ChangeFundOrderTradeState(fundOrderTradeId, TradeState.TradeToOpen);
                    break;
                default:
                    this.ShowErrorMessage($"Unable to load Trade Order {fundOrderTrade.OrderId}:{fundOrderTrade.TradeId} with Trade State: {fundOrderTrade.TradeState} ", "Load Trade Order Error");
                    DialogResult = DialogResult.Cancel;
                    Close();
                    break;
            }
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
            RenderFundOrders();
            RenderTrades();
        }
        UpdateButtons();
    }

    void AcceptLegacyTradeSelection()
    {
        if (!_viewModel.IsLegacyHistoryMode || lstTrades.SelectedItems.Count == 0
            || lstTrades.SelectedItems[0].Tag is not LegacyFundTradeHistoryReadModel history)
            return;
        LegacyTradeHistory = history;
        LegacyFund = _viewModel.SelectedFund;
        LegacyFundOrder = _selectedLegacyOrder?.Order;
        DialogResult = DialogResult.OK;
        Close();
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
        if (_rendering) return;
        Interlocked.Increment(ref _legacyViewerGeneration);
        await CloseEmbeddedLegacyTradeEditorAsync();
        _displayedTradeId = null;
        pnlTradeControl.Controls.Clear();
        ddlOrderActionType.Enabled = false;
        txtDaysToExpiry.Visible = false;
        lblDaysToExpiry.Visible = false;
        lstTrades.Items.Clear();
        if (lstTradeOrders.SelectedItems.Count > 0)
        {
            if (lstTradeOrders.SelectedItems[0].Tag is LegacyFundOrderHistoryReadModel legacy)
            {
                _selectedLegacyOrder = legacy;
                _legacyOrderSelected = true;
                _canonicalOrderSelected = false;
                _viewModel.SelectFundOrder(-1);
                await _viewModel.GetLegacyTradesAsync(legacy.Order.OrderId);
                RenderTrades();
                UpdateButtons();
                return;
            }
            if (lstTradeOrders.SelectedItems[0].Tag is FundOrderProjectionReadModel canonical)
            {
                _selectedLegacyOrder = null;
                _legacyOrderSelected = false;
                _canonicalOrderSelected = true;
                _viewModel.SelectFundOrder(-1);
                var trades = await _viewModel.GetCanonicalTradesAsync(canonical.OrderId);
                foreach (var trade in trades)
                    lstTrades.Items.Add(new ListViewItem([$"{trade.TradeId}", trade.TradeFamily, trade.DirectionOrBias, trade.UnderlyingRoot]));
                UpdateButtons();
                return;
            }
            _canonicalOrderSelected = false;
            _legacyOrderSelected = false;
            _selectedLegacyOrder = null;
            _viewModel.SelectFundOrder(lstTradeOrders.SelectedIndices[0]);
            RenderTrades();
            UpdateButtons();
        }
        else
        {
            _canonicalOrderSelected = false;
            _legacyOrderSelected = false;
            _selectedLegacyOrder = null;
            _viewModel.SelectFundOrder(-1);
        }
    }


    async void lstTrades_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (_rendering) return;
        await ObserveAsync(ShowSelectedTradeAsync);
    }

    async Task ShowSelectedTradeAsync()
    {
        if (_viewModel.IsLegacyHistoryMode && lstTrades.SelectedItems.Count > 0
            && lstTrades.SelectedItems[0].Tag is LegacyFundTradeHistoryReadModel history)
        {
            await ShowLegacyTradeEditorAsync(history);
            return;
        }
        Interlocked.Increment(ref _legacyViewerGeneration);
        await CloseEmbeddedLegacyTradeEditorAsync();
        if (_viewModel!.FundOrders.Count > 0 && _viewModel!.FundOrderTrades.Count > 0)
        {
            var index = lstTrades.SelectedIndices.Count > 0 ? lstTrades.SelectedIndices[0] : 0;
            _lastTradeIndex = index;
            _viewModel.SelectFundOrderTrade(index);
            var fundOrderTrade = _viewModel.GetFundOrderTrade(index);
            LoadTradeStateTargets(fundOrderTrade!.TradeState);
            var controls = new Control[] { dtpTradeDate, ddlOrderActionType };
            foreach (var o in controls)
                o.Enabled = fundOrderTrade!.TradeState == TradeState.NewTrade;
            txtTradeType.Text = fundOrderTrade!.TradeType.ToString();
            dtpTradeDate.Value = fundOrderTrade.TradeDate.ToDateTime(TimeOnly.MinValue);
            txtDaysToExpiry.Visible = true;
            lblDaysToExpiry.Visible = true;
            if (_displayedTradeId != fundOrderTrade.Id)
            {
                _displayedTradeId = fundOrderTrade.Id;
                ClearTradeOrderControl();
            }
        }
        return;
        
    }

    async void btnCreateOrder_Click(object sender, EventArgs e)
    {
        var fundId = _viewModel.GetFundId(ddlFund.SelectedIndex);
        var valueDate = _viewModel.ValueDate.HasValue
            ? _viewModel.ValueDate.Value
            : DateOnly.FromDateTime(EasternTime.GetNow(TimeProvider.System));
        var vm = new FundOrderEditorViewModel(
            _appRoot,
            valueDate,
            _viewModel.BaseContracts,
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
        if (lstTradeOrders.SelectedIndices.Count > 0)
        {
            var fundOrder = _viewModel.GetFundOrder(lstTradeOrders.SelectedIndices[0]);
            var dlg = new CreateFundOrderTradeForm();
            dlg.SetViewModel(_viewModel);
            dlg.SetFundOrder(fundOrder!);
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                var fundOrderTrade = dlg.FundOrderTrade with
                {
                    FundId = fundOrder!.FundId,
                    OrderId = fundOrder.OrderId,
                    TradeDate = fundOrder.TradeDate,
                    MaturityDate = fundOrder.MaturityDate,
                    PrimaryTrade = fundOrder.Trades.Length == 0
                };
                _lastTradeOrderIndex = lstTradeOrders.SelectedIndices[0];
                _lastTradeIndex = lstTrades.Items.Count;
                await ObserveAsync(() => _viewModel.AddTradeToFundOrder(fundOrderTrade));
            }
        }
    }

    async Task ShowLegacyTradeEditorAsync(LegacyFundTradeHistoryReadModel history)
    {
        var generation = Interlocked.Increment(ref _legacyViewerGeneration);
        var composition = history.Composition;
        var trade = history.TradeDbTrade;
        txtTradeType.Text = composition.TradeType.ToString();
        if (composition.TradeDate != DateOnly.MinValue)
            dtpTradeDate.Value = composition.TradeDate.ToDateTime(TimeOnly.MinValue);
        txtDaysToExpiry.Visible = true; lblDaysToExpiry.Visible = true;
        txtDaysToExpiry.Text = composition.TradeDate == DateOnly.MinValue || composition.MaturityDate == DateOnly.MinValue
            ? "Unknown"
            : $"{composition.MaturityDate.DayNumber - composition.TradeDate.DayNumber}";
        await CloseEmbeddedLegacyTradeEditorAsync();
        if (generation != Volatile.Read(ref _legacyViewerGeneration))
            return;
        pnlTradeControl.Controls.Clear();

        var fund = _viewModel.SelectedFund;
        var order = _selectedLegacyOrder?.Order;
        if (trade is null || fund is null || order is null)
        {
            ShowTradeEditorUnavailable(
                trade is null
                    ? $"No corresponding TradeDb trade exists for {composition.OrderId}:{composition.TradeId}."
                    : $"The source Fund or FundOrder is unavailable for {composition.OrderId}:{composition.TradeId}.");
            UpdateButtons();
            return;
        }

        if (composition.TradeType is not (TradeType.ShortIronCondor or TradeType.LongIronCondor))
        {
            ShowTradeEditorUnavailable(
                $"No Iron Condor trade-order editor is available for trade type {composition.TradeType}.");
            UpdateButtons();
            return;
        }

        var baseContract = _viewModel.BaseContracts.FirstOrDefault(contract =>
            string.Equals(contract.ContractId?.Trim(), trade.UnderlyingContractId?.Trim(), StringComparison.OrdinalIgnoreCase));
        baseContract ??= _viewModel.BaseContracts.FirstOrDefault(contract =>
            string.Equals(contract.Symbol?.Trim(), composition.BaseContractSymbol?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (baseContract is null)
        {
            ShowTradeEditorUnavailable(
                $"The exact base contract '{composition.BaseContractSymbol}' for {composition.OrderId}:{composition.TradeId} is unavailable in reference data.");
            UpdateButtons();
            return;
        }

        var orderActionType = GetOrderActionType(composition.TradeState);
        var valueDate = orderActionType == OrderActionType.Open
            ? order.TradeDate
            : composition.TradeDate;
        if (valueDate == DateOnly.MinValue)
            valueDate = trade.TradeDate;
        var viewModel = new IronCondorTradeOrderViewModel(
            _appRoot,
            valueDate,
            fund.FundId,
            baseContract,
            order,
            composition,
            orderActionType,
            _referenceDataService,
            historicalReadOnly: true,
            historicalTrade: trade,
            historicalFundBalance: fund.Balance);
        var editor = new IronCondorTradeOrderView(this, viewModel);
        if (generation != Volatile.Read(ref _legacyViewerGeneration))
        {
            await CloseControlAsync(editor);
            editor.Dispose();
            return;
        }

        _embeddedLegacyTradeEditor = editor;
        editor.Dock = DockStyle.Fill;
        pnlTradeControl.Controls.Add(editor);
        if (editor is IFormControl formControl)
            formControl.Open();
        UpdateButtons();
    }

    void ShowTradeEditorUnavailable(string message)
    {
        pnlTradeControl.Controls.Clear();
        pnlTradeControl.Controls.Add(new Label
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

    async Task CloseEmbeddedLegacyTradeEditorAsync()
    {
        var editor = _embeddedLegacyTradeEditor;
        _embeddedLegacyTradeEditor = null;
        if (editor is null)
            return;
        pnlTradeControl.Controls.Remove(editor);
        await CloseControlAsync(editor);
        editor.Dispose();
    }

    static async ValueTask CloseControlAsync(Control viewer)
    {
        if (viewer is IAsyncFormControl asyncControl)
            await asyncControl.CloseAsync();
        else if (viewer is IFormControl formControl)
            formControl.Close();
    }

    static string LegacyDate(DateOnly value) => value == DateOnly.MinValue ? "Unknown" : $"{value:yyyy-MMM-dd}";

    static OrderActionType GetOrderActionType(TradeState tradeState)
        => tradeState == TradeState.TradeToClose ? OrderActionType.Close : OrderActionType.Open;

    async void btnRemoveTrade_Click(object sender, EventArgs e)
    {
        _ = _viewModel.GetFundOrder(lstTradeOrders.SelectedIndices[0]);
        if (lstTrades.SelectedIndices.Count > 0)
        {
            var fundOrderTrade = _viewModel.GetFundOrderTrade(lstTrades.SelectedIndices[0]);
            await ObserveAsync(() => _viewModel.RemoveTradeFromFundOrder(fundOrderTrade!.Id));
        }
    }

    void btnClearTrade_Click(object sender, EventArgs e) => ClearTradeOrderControl();

    async void btnSubmitOrder_Click(object sender, EventArgs e)
    {
        var orderActionType = (OrderActionType)Enum.Parse(typeof(OrderActionType), ddlOrderActionType.SelectedItem!.ToString()!);
        if (!_viewModel.ValidateOrderSubmission(orderActionType))
            return;
        var tradeOrderControl = pnlTradeControl.Controls[0] as ITradeOrderControl;
        var orderConfirmation = new WinFormsTradeOrderConfirmationService(this);
        await ObserveAsync(async () =>
        {
            var commandId = await tradeOrderControl!.SubmitOrderAsync(
                DateOnly.FromDateTime(dtpTradeDate.Value),
                orderActionType,
                orderConfirmation);
            if (commandId != Guid.Empty)
                _viewModel.SetCommandId(commandId);
        });
    }

    void dtpTradeDate_ValueChanged(object sender, EventArgs e)
    {
        if (pnlTradeControl.Controls.Count > 0)
        {
            var tradeOrderControl = pnlTradeControl.Controls[0] as ITradeOrderControl;
            txtDaysToExpiry.Text =$"{ tradeOrderControl!.MaturityDate.DayNumber - DateOnly.FromDateTime(dtpTradeDate.Value).DayNumber }";
        }
    }

    async void btnCancelOrder_Click(object sender, EventArgs e)
    {
        if (lstTradeOrders.SelectedIndices.Count > 0)
        {
            var fundOrder = _viewModel.GetFundOrder(lstTradeOrders.SelectedIndices[0]);
            var dlg = new DeleteFundOrderForm($"Are you sure you want to delete order:{Environment.NewLine} {fundOrder!.OrderId} {fundOrder.Reference ?? string.Empty} ?");
            if (dlg.ShowDialog() == DialogResult.Yes)
                await ObserveAsync(() => _viewModel.RemoveOrderFromFund(fundOrder.Id));
        }
    }

    void btnNearestStrikes_Click(object sender, EventArgs e)
    {
        var tradeOrderControl = pnlTradeControl.Controls[0] as ITradeOrderControl;
        tradeOrderControl?.SetNearestStrikePrices();
    }

    async void btnEndOfDay_Click(object sender, EventArgs e)
    {
        var index = lstTradeOrders.SelectedIndices.Count > 0 ? lstTradeOrders.SelectedIndices[0] : 0;
        var fundOrder = _viewModel.GetFundOrder(index);
        index = lstTrades.SelectedIndices.Count > 0 ? lstTrades.SelectedIndices[0] : 0;
        var fundOrderTrade = _viewModel.GetFundOrderTrade(lstTrades.SelectedIndices[0]);
        var baseContract = _viewModel.BaseContracts.Where(o => o.Symbol == fundOrderTrade!.BaseContractSymbol.Trim()).FirstOrDefault();
        var eodParam = new TradeEndOfDayParameter
        {
            FundId = fundOrder!.FundId,
            OrderId = fundOrder.OrderId,
            TradeId = fundOrderTrade!.TradeId,
            TradeType = fundOrderTrade.TradeType,
            BaseContractId =baseContract!.ContractId,
            ValueDate = DateOnly.FromDateTime(dtpTradeDate.Value)
        };
        var dlg = new TradeEndOfDayForm(_appRoot, eodParam);
        var dlgResult = dlg.ShowDialog();
        if (dlgResult == DialogResult.OK)
            await ObserveAsync(LoadFundsAsync);
    }

    async void btnChangeTradeState_Click(object sender, EventArgs e)
    {
        var trade = _viewModel.SelectedFundOrderTrade;
        if (trade is null || ddlTradeState.SelectedItem is null)
            return;
        if (!Enum.TryParse<TradeState>(ddlTradeState.SelectedItem.ToString(), out var targetState))
            return;
        await ObserveAsync(() => _viewModel.ChangeFundOrderTradeState(trade.Id, targetState));
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

    async void btnCreateFund_Click(object sender, EventArgs e)
    {
        var vm = new CreateFundReadModel(_appRoot, _referenceDataService);
        var dlg = new CreateFundForm(vm);
        switch (dlg.ShowDialog())
        {
            case DialogResult.OK:
                _viewModel.SetSelectedFundIndex(dlg.Fund.FundId);
                await ObserveAsync(LoadFundsAsync);
                break;
        }
    }

    async void ddlOrderActionType_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (pnlTradeControl.Controls.Count == 0) return;
        var orderActionType = Enum.Parse<OrderActionType>(ddlOrderActionType.SelectedItem!.ToString()!);
        _viewModel.OrderActionType = orderActionType;   
        var tradeOrderControl = pnlTradeControl.Controls[0] as ITradeOrderControl;
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
        if (lstTradeOrders.SelectedItems.Count > 0)
        {
            var fundOrder = _viewModel.GetFundOrder(lstTradeOrders.SelectedItems[0].Index);
            await ObserveAsync(() => _viewModel.CloseFundOrder(fundOrder!.Id));
        }
    }

    async void lstTradeOrders_DoubleClick(object sender, EventArgs e)
        => await ObserveAsync(LoadTradeOrderAsync);

    void pnlTradePosition_Paint(object sender, PaintEventArgs e)
    {

    }

    void TradeOrderEditorForm_FormClosed(object sender, FormClosedEventArgs e)
    {

    }

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
        cbLiveFeed.BackColor = cbLiveFeed.Checked switch
        {
            _ when !cbLiveFeed.Enabled => Color.DarkGray,
            _ when cbLiveFeed.Checked => Color.LimeGreen,
            _ => Color.Red, 
        };

        var tradeOrderControl = pnlTradeControl.Controls[0] as ITradeOrderControl;
        await ObserveAsync(() => tradeOrderControl!.SetLiveFeedAsync(cbLiveFeed.Checked));
    }

    async Task ObserveAsync(Func<Task> operation)
    {
        try
        {
            await operation();
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
