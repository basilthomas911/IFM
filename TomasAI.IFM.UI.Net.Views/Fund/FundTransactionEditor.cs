using TomasAI.IFM.Domain.Trade.Shared;
using System.Data;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.ViewModels.Fund;
using TomasAI.IFM.UI.Net.ViewModels.Fund;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;

namespace TomasAI.IFM.UI.Net.Views.Fund;

public partial class FundTransactionEditor
    : Form, IForm<FundTransactionEditor>, IFormControl
{
    FundTransactionEditorViewModel? _viewModel;
    readonly IViewNavigator _navigator;

    /// <summary>
    /// Initializes a new instance of the <see cref="FundTransactionEditor"/> class.
    /// </summary>
    /// <remarks>This constructor initializes the date range controls to represent the current month. The
    /// "From" date is set to the first day of the current month, and the "To" date is set to the last day of the
    /// current month. The controls are temporarily disabled during initialization to prevent user
    /// interaction.</remarks>
    public FundTransactionEditor(IViewNavigator navigator)
    {
        _navigator = navigator;
        InitializeComponent();
        var dtpControls = new DateTimePicker[] { dtpFrom, dtpTo };
        _ = dtpControls.Select(e => e.Enabled = false).ToArray();
        dtpFrom.Value = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
        dtpTo.Value = new DateTime(dtpFrom.Value.Year, dtpFrom.Value.Month, DateTime.DaysInMonth(dtpFrom.Value.Year, dtpFrom.Value.Month));
        _ = dtpControls.Select(e => e.Enabled = true).ToArray();
    }

    /// <summary>
    /// Initializes the specified <see cref="FundTransactionEditorViewModel"/> and sets up event handlers for its
    /// operations.
    /// </summary>
    /// <remarks>This method assigns the provided view model to the internal state and configures event
    /// handlers to update the UI based on the view model's operations. If <paramref name="viewModel"/> is <see
    /// langword="null"/>, the current view model is cleared, and no event handlers are set.</remarks>
    /// <param name="viewModel">The <see cref="FundTransactionEditorViewModel"/> instance to be loaded. Can be <see langword="null"/> to clear
    /// the current view model.</param>
    public void LoadViewModel(FundTransactionEditorViewModel? viewModel)
    {
        UnsubscribeFromOperations();
        _viewModel = viewModel;
        if (_viewModel is null)
            return;

        _viewModel.LoadFundsOperation.PropertyChanged += Operation_PropertyChanged;
        _viewModel.LoadFundDetailsOperation.PropertyChanged += Operation_PropertyChanged;
    }

    async void FundTransactionEditor_Load(object sender, EventArgs e)
        => await LoadFundsAsync();

    async void ddlFund_SelectedIndexChanged(object sender, EventArgs e)
        => await LoadSelectedFundDetailsAsync();

    async void dtpFrom_ValueChanged(object sender, EventArgs e)
    {
        if (dtpFrom.Enabled)
            await LoadSelectedFundDetailsAsync();
    }

    async void dtpTo_ValueChanged(object sender, EventArgs e)
    {
        if (dtpTo.Enabled)
            await LoadSelectedFundDetailsAsync();
    }

    async void btnAdjust_Click(object sender, EventArgs e)
        => await OnAdjustClickedAsync();

    async Task LoadFundsAsync()
    {
        if (_viewModel is null)
            return;
        try
        {
            await _viewModel.LoadFundsOperation.ExecuteAsync();
            BindFunds();
        }
        catch (Exception exception)
        {
            this.ShowErrorMessage(exception.Message, "Loading Funds Error");
        }
    }

    async Task LoadSelectedFundDetailsAsync()
    {
        if (_viewModel is null)
            return;
        var fundId = _viewModel.GetFundId(ddlFund.SelectedIndex);
        if (fundId < 0)
            return;
        if (dtpFrom.Value > dtpTo.Value)
        {
            this.ShowErrorMessage("The From date must not be after the To date.", "Loading Fund Details Error");
            return;
        }

        try
        {
            _viewModel.SetFundDetailsFilter(fundId, dtpFrom.Value, dtpTo.Value);
            await _viewModel.LoadFundDetailsOperation.ExecuteAsync();
            BindFundDetails();
        }
        catch (Exception exception)
        {
            this.ShowErrorMessage(exception.Message, "Loading Fund Details Error");
        }
    }

    void OnTransactionSelectionChanged()
    {
        if (gridTransactions.SelectedRows.Count > 0)
        {
            var index = gridTransactions.SelectedRows[0].Index;
            _viewModel!.SelectTransaction(index);
            txtComment.Text = _viewModel.TransactionComment;
        }
    }

    async Task OnAdjustClickedAsync()
    {
        if (gridTransactions.RowCount > 0)
        {
            var index = gridTransactions.SelectedRows[0].Index;
            var fundTransaction = _viewModel!.GetFundTransaction(index);
            var result = _navigator.ShowModal<AdjustFundTransactionEditor>(view =>
                view.LoadModel(new AdjustFundTransactionReadModel(
                    _viewModel.AppRoot,
                    fundTransaction!,
                    _viewModel.FundBalance)));
            if (result == NavigationResult.Accepted)
                await LoadFundsAsync();
        }
    }

    void FundTransactionEditor_FormClosed(object sender, FormClosedEventArgs e)
    {
        UnsubscribeFromOperations();
    }

    void SetupFundTransactionGridColumns()
    {
        gridTransactions.Columns.Clear();
        gridTransactions.Columns.AddRange(
            new DataGridViewTextBoxColumn { Name = "TransactionId", HeaderText = "Transaction Id", DataPropertyName = "TransactionId" },
            new DataGridViewTextBoxColumn { Name = "TransactionDate", HeaderText = "Transaction Date", DataPropertyName = "TransactionDate" },
            new DataGridViewTextBoxColumn { Name = "TransactionType", HeaderText = "Transaction Type", DataPropertyName = "TransactionType" },
            new DataGridViewTextBoxColumn { Name = "FundId", HeaderText = "Fund Id", DataPropertyName = "FundId" },
            new DataGridViewTextBoxColumn { Name = "OrderId", HeaderText = "Order Id", DataPropertyName = "OrderId" },
            new DataGridViewTextBoxColumn { Name = "TradeId", HeaderText = "Trade Id", DataPropertyName = "TradeId" },
            new DataGridViewTextBoxColumn { Name = "TradeType", HeaderText = "Trade Type", DataPropertyName = "TradeType" },
            new DataGridViewTextBoxColumn { Name = "ValueDate", HeaderText = "Value Date", DataPropertyName = "ValueDate" },
            new DataGridViewTextBoxColumn { Name = "TradeStatus", HeaderText = "Trade Status", DataPropertyName = "TradeStatus" },
            new DataGridViewTextBoxColumn { Name = "Description", HeaderText = "Description", DataPropertyName = "Description" },
            new DataGridViewTextBoxColumn { Name = "Amount", HeaderText = "Amount", DataPropertyName = "Amount", DefaultCellStyle = new DataGridViewCellStyle { Format = "C" } },
            new DataGridViewTextBoxColumn { Name = "Balance", HeaderText = "Balance", DataPropertyName = "Balance", DefaultCellStyle = new DataGridViewCellStyle { Format = "C" } }
        );
    }

    public void Open()
        => throw new NotImplementedException();

    void gridTransactions_CellContentClick(object sender, DataGridViewCellEventArgs e)
    {

    }

    void gridTransactions_DoubleClick(object sender, EventArgs e)
        => _ = OnAdjustClickedAsync();

    void gridTransactions_SelectionChanged(object sender, EventArgs e)
        => OnTransactionSelectionChanged();

    private void bindingSource1_CurrentChanged(object sender, EventArgs e)
    {

    }

    private async void btnAdjust_Click_1(object sender, EventArgs e)
        => await OnAdjustClickedAsync();

    void BindFunds()
    {
        ddlFund.Items.Clear();
        if (_viewModel is null)
            return;
        foreach (var fund in _viewModel.Funds)
            ddlFund.Items.Add(fund.Name);
        if (ddlFund.Items.Count > 0)
            ddlFund.SelectedIndex = 0;
    }

    void BindFundDetails()
    {
        if (_viewModel is null)
            return;
        txtComment.Text = _viewModel.TransactionComment;
        gridTransactions.DataSource = null;
        if (_viewModel.FundTransactions.Count > 0)
        {
            gridTransactions.AutoGenerateColumns = false;
            SetupFundTransactionGridColumns();
            gridTransactions.DataSource = _viewModel.FundTransactions.ToList();
        }
        txtFundBalance.Text = $"{_viewModel.FundBalance:C}";
        if (_viewModel.FundPnlReport is not { } report)
            return;
        txtWinRate.Text = $"{report.WinRate:P2}";
        txtAverageProfit.Text = report.AverageProfit.ToString("C");
        txtLossRate.Text = $"{report.LossRate:P2}";
        txtAverageLoss.Text = report.AverageLoss.ToString("C");
        txtWinLossRatio.Text = $"{report.WinLossRatio:F2}";
        txtSharpeRatio.Text = $"{report.ActualSharpeRatio:F2}";
        txtProfitLoss.Text = $"{report.PnlAmount:C}";
        txtProfitLossPercent.Text = $"{report.PnlPercent:P2}";
        txtCommission.Text = $"{report.TradeCommission:C}";
    }

    void Operation_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(IAsyncOperation.IsRunning) || _viewModel is null)
            return;
        this.Post(() =>
        {
            var isBusy = _viewModel.LoadFundsOperation.IsRunning || _viewModel.LoadFundDetailsOperation.IsRunning;
            ddlFund.Enabled = !isBusy;
            dtpFrom.Enabled = !isBusy;
            dtpTo.Enabled = !isBusy;
            btnAdjust.Enabled = !isBusy && gridTransactions.RowCount > 0;
        });
    }

    void UnsubscribeFromOperations()
    {
        if (_viewModel is null)
            return;
        _viewModel.LoadFundsOperation.PropertyChanged -= Operation_PropertyChanged;
        _viewModel.LoadFundDetailsOperation.PropertyChanged -= Operation_PropertyChanged;
    }

    void IFormControl.Resize(Control parentControl)
        => throw new NotImplementedException();
}
