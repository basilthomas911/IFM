using TomasAI.IFM.Contracts;
using TomasAI.IFM.ViewModels.Fund;

namespace TomasAI.IFM.Views.Fund;

public partial class AdjustFundTransactionEditor : Form, IForm<AdjustFundTransactionEditor>, IFormControl
{
    AdjustFundTransactionReadModel? _viewModel;

    public AdjustFundTransactionEditor()
    {
        InitializeComponent();
    }

    /// <summary>
    /// load view model
    /// </summary>
    /// <param name="viewModel"></param>
    public void LoadModel(AdjustFundTransactionReadModel viewModel)
    {
        _viewModel = viewModel;
        _viewModel.StartListener();
        _viewModel.OnFundTransactionAdjustment = () =>
            this.Post(() =>
            {
                MessageBox.Show($"Unable to Adjust: {_viewModel.FundTransaction.TransactionType} on Fund Transaction: {_viewModel.FundTransaction.Id} ", "Fund Transaction Adjustment Error", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.DialogResult = DialogResult.OK;
                this.Close();
            });
        _viewModel.OnErrorMessage = (errorMsg, caption) => this.ShowErrorMessage(errorMsg, caption);
    }

    public void UnloadModel()
    {
        _viewModel?.StopListener();
    }

    private void AdjustFundTransactionForm_Load(object sender, EventArgs e)
    {
        try
        {
            txtTransactionType.Text = $"{_viewModel?.GetAdjustmentTransactionType()}";
            txtFundId.Text = $"{_viewModel?.FundTransaction.FundId}";
            txtOrderId.Text = $"{_viewModel?.FundTransaction.OrderId}";
            txtTradeId.Text = $"{_viewModel?.FundTransaction.TradeId}";
            txtTradeType.Text = $"{_viewModel?.FundTransaction.TradeType}";
            txtValueDate.Text = $"{_viewModel?.FundTransaction.ValueDate:yyyy-MMM-dd}";
            txtTradeStatus.Text = $"{_viewModel?.FundTransaction.TradeStatus}";
            txtAmount.Text = string.Empty;
            txtComment.Text = string.Empty;
            txtBalance.Text = $"{_viewModel?.FundBalance:C}";
            btnSave.Enabled = false;
        }
        catch
        {
            MessageBox.Show($"Unable to Adjust Transaction Type: {_viewModel?.FundTransaction.TransactionType} for Adjustment", "Fund Transaction Adjustment Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }
    }

    private void AdjustFundTransactionForm_FormClosed(object sender, FormClosedEventArgs e)
    {
    }

    private void btnSave_Click(object sender, EventArgs e)
    {
        var adjustmentTransaction = _viewModel?.GetAdjustmentTransaction(
            amount: Convert.ToDecimal(txtAmount.Text),
            comment: txtComment.Text);
        _viewModel?.AdjustFundTransaction(adjustmentTransaction!);
    }

    private void btnCancel_Click(object sender, EventArgs e)
    {
        DialogResult = DialogResult.Cancel;
        Close();
    }

    private void txtAmount_TextChanged(object sender, EventArgs e)
    {
       btnSave.Enabled = decimal.TryParse(txtAmount.Text,  out _) 
            && !string.IsNullOrWhiteSpace(txtComment.Text);
    }

    private void txtComment_TextChanged(object sender, EventArgs e)
    {
         btnSave.Enabled = decimal.TryParse(txtAmount.Text, out _)
            && !string.IsNullOrWhiteSpace(txtComment.Text);
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
