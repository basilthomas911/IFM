using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using TomasAI.IFM.Contracts;
using TomasAI.IFM.Models;
using TomasAI.IFM.Shared.Fund.ViewModels;
using TomasAI.IFM.ViewModels.Fund;

namespace TomasAI.IFM.Views.Fund
{
    public partial class FundTransactionEditor : Form, IForm<FundTransactionEditor>, IFormControl
    {
        private FundTransactionEditorViewModel _viewModel;

        public FundTransactionEditor()
        {
            InitializeComponent();
            var dtpControls = new DateTimePicker[] { dtpFrom, dtpTo };
            dtpControls.Select(e => e.Enabled = false).ToArray();
            dtpFrom.Value = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            dtpTo.Value = new DateTime(dtpFrom.Value.Year, dtpFrom.Value.Month, DateTime.DaysInMonth(dtpFrom.Value.Year, dtpFrom.Value.Month));
            dtpControls.Select(e => e.Enabled = true).ToArray();
        }

        public void LoadViewModel(FundTransactionEditorViewModel viewModel)
        {
            _viewModel = viewModel;
            _viewModel.OnErrorMessage = (errorMsg, caption) => this.ShowErrorMessage(errorMsg, caption);
            _viewModel.OnFundsLoaded = (funds) => 
                this.Post(() => {
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
            _viewModel.OnFundTransactionsLoaded = (fundTransactions) => 
                this.Post(() => {
                    txtComment.Text = string.Empty;
                    gridTransactions.Rows.Clear();
                    if (fundTransactions != null && fundTransactions.Count > 0)
                    {
                         fundTransactionBindingSource.DataSource = fundTransactions;
                        fundTransactionBindingSource.ResetBindings(true);
                    }
                });
            _viewModel.OnTransactionCommentLoaded = (comment) => this.Post(() => txtComment.Text = comment ?? string.Empty);
            _viewModel.OnFundBalanceLoaded = (fundBalance) => this.Post(() => txtFundBalance.Text = $"{fundBalance:C}");
            _viewModel.OnFundPnlReportLoaded = e => this.Post(() =>
            {
                txtWinRate.Text = $"{e.WinRate:P2}";
                txtAverageProfit.Text = e?.AverageProfit.ToString("C");
                txtLossRate.Text = $"{e.LossRate:P2}";
                txtAverageLoss.Text = e?.AverageLoss.ToString("C");
                txtWinLossRatio.Text = $"{e.WinLossRatio:F2}";
                txtSharpeRatio.Text = $"{e.ActualSharpeRatio:F2}";
                txtProfitLoss.Text = $"{e.PnlAmount:C}";
                txtProfitLossPercent.Text = $"{e.PnlPercent:P2}";
                txtCommission.Text = $"{e.TradeCommission:C}";
            });
        }

        private void FundTransactionEditor_Load(object sender, EventArgs e)
            => OnFundTransactionEditorLoad();

        private void ddlFund_SelectedIndexChanged(object sender, EventArgs e)
            => OnFundSelectedIndexChanged();

        private void dtpFrom_ValueChanged(object sender, EventArgs e)
            => OnFromValueChanged();

        private void dtpTo_ValueChanged(object sender, EventArgs e)
            => OnToValueChanged();

        private void gridTransactions_SelectionChanged(object sender, EventArgs e)
            => OnTransactionSelectionChanged();

        private void btnAdjust_Click(object sender, EventArgs e)
            => OnAdjustClicked();

        private void OnFundTransactionEditorLoad() => _viewModel.LoadFunds();

        private void OnFundSelectedIndexChanged()
        {
            var fundId = _viewModel.GetFundId(ddlFund.SelectedIndex);
            _viewModel.LoadFundTransactions(fundId, dtpFrom.Value, dtpTo.Value);
            _viewModel.LoadFundPnlReport(fundId, dtpFrom.Value, dtpTo.Value);
        }

        private void OnFromValueChanged()
        {
            if (dtpFrom.Enabled)
            {
                var fundId = _viewModel.GetFundId(ddlFund.SelectedIndex);
                _viewModel.LoadFundTransactions(fundId, dtpFrom.Value, dtpTo.Value);
                _viewModel.LoadFundPnlReport(fundId, dtpFrom.Value, dtpTo.Value);
            }
        }

        private void OnToValueChanged()
        {
            if (dtpTo.Enabled)
            {
                var fundId = _viewModel.GetFundId(ddlFund.SelectedIndex);
                _viewModel.LoadFundTransactions(fundId, dtpFrom.Value, dtpTo.Value);
                _viewModel.LoadFundPnlReport(fundId, dtpFrom.Value, dtpTo.Value);
            }
        }

        private void OnTransactionSelectionChanged()
        {
            if (gridTransactions.SelectedRows.Count > 0)
            {
                var index = gridTransactions.SelectedRows[0].Index;
                _viewModel.LoadTransactionComment(
                    transactionId: _viewModel.GetFundTransactionId(index));
            }
        }

        private void OnAdjustClicked()
        {
            var index = gridTransactions.SelectedRows[0].Index;
            var fundTransaction = _viewModel.GetFundTransaction(index);
            var dlg = _viewModel.AppRoot.GetForm<AdjustFundTransactionForm>();
            dlg.LoadModel(new AdjustFundTransactionReadModel(_viewModel.AppRoot, fundTransaction, _viewModel.FundBalance));
            if (dlg.ShowDialog() == DialogResult.OK)
                _viewModel.LoadFunds();
            dlg.UnloadModel();
        }

        private void FundTransactionEditor_FormClosed(object sender, FormClosedEventArgs e)
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
    }
}
