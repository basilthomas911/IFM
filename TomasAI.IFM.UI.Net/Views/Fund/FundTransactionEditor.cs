using System.Data;
using TomasAI.IFM.Contracts;
using TomasAI.IFM.ViewModels.Fund;
using TomasAI.IFM.UI.Net.ViewModels.Fund;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;

namespace TomasAI.IFM.Views.Fund;

public partial class FundTransactionEditor
    : Form, IForm<FundTransactionEditor>, IFormControl
{
    FundTransactionEditorViewModel? _viewModel;

    /// <summary>
    /// Initializes a new instance of the <see cref="FundTransactionEditor"/> class.
    /// </summary>
    /// <remarks>This constructor initializes the date range controls to represent the current month. The
    /// "From" date is set to the first day of the current month, and the "To" date is set to the last day of the
    /// current month. The controls are temporarily disabled during initialization to prevent user
    /// interaction.</remarks>
    public FundTransactionEditor()
    {
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
        _viewModel = viewModel;
        _viewModel?.OnErrorMessage = (errorMsg, caption) => this.ShowErrorMessage(errorMsg, caption);
        _viewModel?.OnFundsLoaded = (funds) =>
            this.Post(() =>
            {
                btnAdjust.Enabled = false;
                ddlFund.Items.Clear();
                if (funds.Count == 0)
                    return;
                foreach (var fund in funds)
                    ddlFund.Items.Add(fund.Name);
                if (ddlFund.Items.Count > 0)
                    ddlFund.SelectedIndex = 0;
                btnAdjust.Enabled = ddlFund.Items.Count > 0;
            });
        _viewModel?.OnFundTransactionsLoaded = (fundTransactions) =>
            this.Post(() =>
            {
                txtComment.Text = string.Empty;
                gridTransactions.DataSource = null;
                if (fundTransactions is not null && fundTransactions.Count > 0)
                {
                    gridTransactions.AutoGenerateColumns = false;
                    SetupFundTransactionGridColumns();
                    gridTransactions.DataSource = fundTransactions.ToList();
                }
            });
        _viewModel?.OnTransactionCommentLoaded = (comment) => this.Post(() => txtComment.Text = comment ?? string.Empty);
        _viewModel?.OnFundBalanceLoaded = (fundBalance) => this.Post(() => txtFundBalance.Text = $"{fundBalance:C}");
        _viewModel?.OnFundPnlReportLoaded = e => this.Post(() =>
        {
            txtWinRate.Text = $"{e.WinRate:P2}";
            txtAverageProfit.Text = e.AverageProfit.ToString("C");
            txtLossRate.Text = $"{e.LossRate:P2}";
            txtAverageLoss.Text = e.AverageLoss.ToString("C");
            txtWinLossRatio.Text = $"{e.WinLossRatio:F2}";
            txtSharpeRatio.Text = $"{e.ActualSharpeRatio:F2}";
            txtProfitLoss.Text = $"{e.PnlAmount:C}";
            txtProfitLossPercent.Text = $"{e.PnlPercent:P2}";
            txtCommission.Text = $"{e.TradeCommission:C}";
        });
    }

    void FundTransactionEditor_Load(object sender, EventArgs e)
        => OnFundTransactionEditorLoad();

    void ddlFund_SelectedIndexChanged(object sender, EventArgs e)
        => OnFundSelectedIndexChanged();

    void dtpFrom_ValueChanged(object sender, EventArgs e)
        => OnFromValueChanged();

    void dtpTo_ValueChanged(object sender, EventArgs e)
        => OnToValueChanged();

    void btnAdjust_Click(object sender, EventArgs e)
        => OnAdjustClicked();

    void OnFundTransactionEditorLoad() => _viewModel!.LoadFunds();

    void OnFundSelectedIndexChanged()
    {
        var fundId = _viewModel!.GetFundId(ddlFund.SelectedIndex);
        _viewModel.LoadFundTransactions(fundId, dtpFrom.Value, dtpTo.Value);
        _viewModel.LoadFundPnlReport(fundId, dtpFrom.Value, dtpTo.Value);
    }

    void OnFromValueChanged()
    {
        if (dtpFrom.Enabled)
        {
            var fundId = _viewModel!.GetFundId(ddlFund.SelectedIndex);
            _viewModel.LoadFundTransactions(fundId, dtpFrom.Value, dtpTo.Value);
            _viewModel.LoadFundPnlReport(fundId, dtpFrom.Value, dtpTo.Value);
        }
    }

    void OnToValueChanged()
    {
        if (dtpTo.Enabled)
        {
            var fundId = _viewModel!.GetFundId(ddlFund.SelectedIndex);
            _viewModel.LoadFundTransactions(fundId, dtpFrom.Value, dtpTo.Value);
            _viewModel.LoadFundPnlReport(fundId, dtpFrom.Value, dtpTo.Value);
        }
    }

    void OnTransactionSelectionChanged()
    {
        if (gridTransactions.SelectedRows.Count > 0)
        {
            var index = gridTransactions.SelectedRows[0].Index;
            _viewModel!.LoadTransactionComment(
                transactionId: _viewModel.GetFundTransactionId(index));
        }
    }

    void OnAdjustClicked()
    {
        if (gridTransactions.RowCount > 0)
        {
            var index = gridTransactions.SelectedRows[0].Index;
            var fundTransaction = _viewModel!.GetFundTransaction(index);
            var dlg = _viewModel.AppRoot.GetForm<AdjustFundTransactionEditor>();
            dlg.LoadModel(new AdjustFundTransactionReadModel(_viewModel.AppRoot, fundTransaction!, _viewModel.FundBalance));
            if (dlg.ShowDialog() == DialogResult.OK)
                _viewModel.LoadFunds();
            dlg.UnloadModel();
        }
    }

    void FundTransactionEditor_FormClosed(object sender, FormClosedEventArgs e)
    {
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
        => OnAdjustClicked();

    void gridTransactions_SelectionChanged(object sender, EventArgs e)
        => OnTransactionSelectionChanged();

    private void bindingSource1_CurrentChanged(object sender, EventArgs e)
    {

    }

    private void btnAdjust_Click_1(object sender, EventArgs e)
        => OnAdjustClicked();

    void IFormControl.Resize(Control parentControl)
        => throw new NotImplementedException();
}
