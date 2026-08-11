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

namespace TomasAI.IFM.UI.Net.Views.Trade;

public partial class TradeOrderEditorForm 
    : Form, IForm<TradeOrderEditorForm>, IFormControl
{
    readonly IAppRoot _appRoot;
    TradeOrderEditorViewModel _viewModel = null!;
    int _lastTradeIndex;
    int _lastTradeOrderIndex;
    long _lastErrorSequence;
    long _lastChangeSequence;
    bool _rendering;

    /// <summary>
    /// create trade order form
    /// </summary>
    /// <param name="appRoot"></param>
    public TradeOrderEditorForm(IAppRoot appRoot)
    {
        InitializeComponent();
        _appRoot = appRoot;
    }

    public FundReadModel Fund => _viewModel?.SelectedFund!;

    public FundOrderReadModel FundOrder => _viewModel?.SelectedFundOrder!;

    public FundOrderTradeReadModel FundOrderTrade => _viewModel?.SelectedFundOrderTrade!;

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

    void ViewModelPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
        => this.Post(() =>
        {
            if (eventArgs.PropertyName == nameof(TradeOrderEditorViewModel.LastError))
                ShowLatestError();
            if (eventArgs.PropertyName == nameof(TradeOrderEditorViewModel.LastChange))
                HandleLatestChange();
            if (eventArgs.PropertyName is nameof(TradeOrderEditorViewModel.Funds)
                or nameof(TradeOrderEditorViewModel.FundOrders)
                or nameof(TradeOrderEditorViewModel.FundOrderTrades))
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
                    await _viewModel.AddTradeLiveFeed(added.FundOrderTrade.OrderId, added.FundOrderTrade.TradeId);
                    break;
                case TradeRemovedFromFundOrderCompleteEvent removed:
                    await _viewModel.RemoveTradeLiveFeed(removed.FundOrderTradeId.OrderId, removed.FundOrderTradeId.TradeId);
                    break;
                case FundOrderTradeStateChangedCompleteEvent:
                    DialogResult = DialogResult.OK;
                    Close();
                    break;
            }
        }
        catch (ModelOperationException)
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

    async void TradeOrderForm_Load(object sender, EventArgs e)
    {
        _lastTradeIndex = -1;
        _lastTradeOrderIndex = -1;
        
        dtpTradeDate.Value = _viewModel!.ValueDate.HasValue ? _viewModel.ValueDate.Value.ToDateTime(TimeOnly.MinValue) : new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day);
        btnLoadOrder.Enabled = false;
        btnCreateOrder.Enabled = false;
        btnDeleteOrder.Enabled = false;
        btnCompleteOrder.Enabled = false;
        var dtpList = new List<DateTimePicker> { dtpFrom, dtpTo };
        dtpList.ForEach(o => o.Enabled = false);
        dtpFrom.Value = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
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
        _lastTradeIndex = -1;
        _lastTradeOrderIndex = -1;
        pnlTradeControl.Controls.Clear();
        await _viewModel.LoadFunds();
    }

    void RenderEditor()
    {
        RenderFunds();
        RenderFundOrders();
        RenderTrades();
        UpdateButtons();
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
            ddlFund.SelectedIndex = _viewModel.FundSelectedIndex;
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
            foreach (var fundOrder in _viewModel.FundOrders)
            {
                lstTradeOrders.Items.Add(new ListViewItem([
                    $"{fundOrder.OrderId}",
                    $"{fundOrder.OrderDate:yyyy-MMM-dd}",
                    $"{fundOrder.OrderStatus}",
                    fundOrder.Reference ?? string.Empty
                ]));
            }
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
            var index = _lastTradeIndex >= 0 ? _lastTradeIndex : _viewModel.FundOrderTradeSelectedIndex;
            _lastTradeIndex = -1;
            if (index >= 0 && index < lstTrades.Items.Count)
                lstTrades.Items[index].Selected = true;
        }
        finally
        {
            _rendering = wasRendering;
        }
        if (!wasRendering && lstTrades.SelectedIndices.Count > 0)
            ShowSelectedTrade();
    }

    void UpdateButtons()
    {
        Cursor.Current = _viewModel.IsBusy ? Cursors.WaitCursor : Cursors.Default;
        btnCreateFund.Enabled = !_viewModel.IsBusy;
        btnLoadOrder.Enabled = _viewModel.CanLoadOrder;
        btnCreateOrder.Enabled = _viewModel.CanCreateOrder;
        btnDeleteOrder.Enabled = _viewModel.CanDeleteOrder;
        btnCompleteOrder.Enabled = _viewModel.CanCompleteOrder;
        btnAddTrade.Enabled = _viewModel.CanAddTrade;
        btnRemoveTrade.Enabled = _viewModel.CanRemoveTrade;
        btnEndOfDay.Enabled = _viewModel.CanEndOfDay;
        btnSubmitOrder.Enabled = _viewModel.CanSubmitOrder;
        cbLiveFeed.Enabled = _viewModel.CanUseLiveFeed;
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
                    // var valueDate = _viewModel.ValueDate.HasValue ? _viewModel.ValueDate.Value : DateOnly.FromDateTime(DateTime.Now.Date);
                    var orderActionType = GetOrderActionType(fundOrderTrade.TradeState);
                    var valueDate = orderActionType == OrderActionType.Open
                        ? fundOrder!.TradeDate
                        : fundOrderTrade!.TradeDate;
                    var baseContract = _viewModel.BaseContracts.Where(e => e.Symbol == fundOrderTrade.BaseContractSymbol).FirstOrDefault();
                   baseContract = baseContract ?? _viewModel.BaseContracts.ElementAt(0);
                   var viewModel = new IronCondorTradeOrderReadModel(_appRoot, valueDate, fundId, baseContract, fundOrder!, fundOrderTrade, orderActionType,
                       maturityDate => this.Post(() => txtDaysToExpiry.Text = $"{(maturityDate.DayNumber - DateOnly.FromDateTime(dtpTradeDate.Value).DayNumber)}"),
                       tradeDate => this.Post(() => dtpTradeDate.Value = tradeDate.ToDateTime(TimeOnly.MinValue)));
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

        static OrderActionType GetOrderActionType(TradeState tradeState) => tradeState == TradeState.TradeToClose ? OrderActionType.Close : OrderActionType.Open;
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

    void ddlFund_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (_rendering) return;
        if (ddlFund.SelectedIndex < 0) return;
        if (_viewModel.SelectFund(ddlFund.SelectedIndex))
        {
            RenderFundOrders();
            RenderTrades();
        }
        UpdateButtons();
    }

    void ShowFundOrders()
    {
        _viewModel.SetOrderDateRange(dtpFrom.Value.AddMonths(-1), dtpTo.Value);
        RenderFundOrders();
        RenderTrades();
        UpdateButtons();
    }

    void lstTradeOrders_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (_rendering) return;
        pnlTradeControl.Controls.Clear();
        ddlOrderActionType.Enabled = false;
        txtDaysToExpiry.Visible = false;
        lblDaysToExpiry.Visible = false;
        lstTrades.Items.Clear();
        if (lstTradeOrders.SelectedItems.Count > 0)
        {
            _viewModel.SelectFundOrder(lstTradeOrders.SelectedIndices[0]);
            RenderTrades();
            UpdateButtons();
        }
        else
            _viewModel.SelectFundOrder(-1);
    }


    void lstTrades_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (_rendering) return;
        ShowSelectedTrade();
    }

    void ShowSelectedTrade()
    {
        if (_viewModel!.FundOrders.Count > 0 && _viewModel!.FundOrderTrades.Count > 0)
        {
            var index = lstTrades.SelectedIndices.Count > 0 ? lstTrades.SelectedIndices[0] : 0;
            if (_lastTradeIndex == index)
                return;
            _lastTradeIndex = index;
            _viewModel.SelectFundOrderTrade(index);
            var fundOrderTrade = _viewModel.GetFundOrderTrade(index);
            var controls = new Control[] { dtpTradeDate, ddlOrderActionType };
            foreach (var o in controls)
                o.Enabled = fundOrderTrade!.TradeState == TradeState.NewTrade;
            txtTradeType.Text = fundOrderTrade!.TradeType.ToString();
            dtpTradeDate.Value = fundOrderTrade.TradeDate.ToDateTime(TimeOnly.MinValue);
            txtDaysToExpiry.Visible = true;
            lblDaysToExpiry.Visible = true;
            ClearTradeOrderControl();
        }
        return;
        
    }

    async void btnCreateOrder_Click(object sender, EventArgs e)
    {
        var fundId = _viewModel.GetFundId(ddlFund.SelectedIndex);
        var valueDate = _viewModel.ValueDate.HasValue ? _viewModel.ValueDate.Value : DateOnly.FromDateTime(DateTime.Now.Date);
        var vm = new FundOrderEditorViewModel(_appRoot, valueDate, _viewModel.BaseContracts, fundId);
        var dlg = new CreateFundOrderForm();
        dlg.SetViewModel(vm);
        if (dlg.ShowDialog() == DialogResult.OK)
            await ObserveAsync(() => _viewModel.AddOrderToFund(dlg.FundOrder));
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
            var dlg = new CreateFundOrderTradeForm(_appRoot);
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
                await ObserveAsync(async () =>
                {
                    var newTradeId = await _viewModel.GetNewTradeIdAsync();
                    await _viewModel.AddTradeToFundOrder(fundOrderTrade with { TradeId = newTradeId });
                });
            }
        }
    }

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

    void btnSubmitOrder_Click(object sender, EventArgs e)
    {
        var orderActionType = (OrderActionType)Enum.Parse(typeof(OrderActionType), ddlOrderActionType.SelectedItem!.ToString()!);
        var fundOrderTrade = _viewModel.GetFundOrderTrade(lstTrades.SelectedIndices[0]);
        var tradeOrderControl = pnlTradeControl.Controls[0] as ITradeOrderControl;
        var orderConfirmation = new TradeOrderConfirmationViewModel(viewModel => {
            var confirmTradeOrder = true;
            var dlg = new TradeOrderConfirmationForm(viewModel);
            if (dlg.ShowDialog() == DialogResult.Cancel)
                confirmTradeOrder = false;
            return confirmTradeOrder;
        });
        tradeOrderControl!.SubmitOrder(DateOnly.FromDateTime(dtpTradeDate.Value), orderActionType, orderConfirmation, _viewModel.SetCommandId );
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

    async void btnCreateFund_Click(object sender, EventArgs e)
    {
        var vm = new CreateFundReadModel(_appRoot);
        var dlg = new CreateFundForm(vm);
        switch (dlg.ShowDialog())
        {
            case DialogResult.OK:
                _viewModel.SetSelectedFundIndex(dlg.Fund.FundId);
                await ObserveAsync(LoadFundsAsync);
                break;
        }
    }

    void ddlOrderActionType_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (pnlTradeControl.Controls.Count == 0) return;
        var orderActionType = Enum.Parse<OrderActionType>(ddlOrderActionType.SelectedItem!.ToString()!);
        _viewModel.OrderActionType = orderActionType;   
        var tradeOrderControl = pnlTradeControl.Controls[0] as ITradeOrderControl;
        tradeOrderControl?.OrderActionTypeChanged(orderActionType);
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

    void cbLiveFeed_CheckedChanged(object sender, EventArgs e)
    {
        cbLiveFeed.BackColor = cbLiveFeed.Checked switch
        {
            _ when !cbLiveFeed.Enabled => Color.DarkGray,
            _ when cbLiveFeed.Checked => Color.LimeGreen,
            _ => Color.Red, 
        };

        var tradeOrderControl = pnlTradeControl.Controls[0] as ITradeOrderControl;
        tradeOrderControl!.LiveFeed(cbLiveFeed.Checked);
    }

    async Task ObserveAsync(Func<Task> operation)
    {
        try
        {
            await operation();
        }
        catch (ModelOperationException)
        {
            // The ViewModel publishes coded failures through LastError.
        }
        catch (Exception exception)
        {
            this.ShowErrorMessage(exception.Message, "Trade Order Editor Error");
        }
    }
}
