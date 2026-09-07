using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.ViewModels.Fund;

namespace TomasAI.IFM.UI.Net.Views.Fund;

public partial class AdjustFundTransactionEditor : DarkTradingForm, IForm<AdjustFundTransactionEditor>, IFormControl
{
    AdjustFundTransactionReadModel? _viewModel;
    bool _closeComplete;

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
        if (_viewModel is not null)
            _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        _viewModel = viewModel;
        _viewModel.PropertyChanged += ViewModel_PropertyChanged;
    }

    public async Task UnloadModelAsync()
    {
        if (_viewModel is not null)
            await _viewModel.StopListener();
    }

    private async void AdjustFundTransactionForm_Load(object sender, EventArgs e)
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
            if (_viewModel is not null)
                await _viewModel.StartListener();
        }
        catch (Exception exception)
        {
            this.ShowErrorMessage(exception.Message, "Fund Transaction Adjustment Error");
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }
    }

    private async void AdjustFundTransactionForm_FormClosing(object sender, FormClosingEventArgs e)
    {
        if (_closeComplete)
            return;
        e.Cancel = true;
        await UnloadModelAsync();
        if (_viewModel is not null)
            _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        _closeComplete = true;
        Close();
    }

    private async void btnSave_Click(object sender, EventArgs e)
    {
        var adjustmentTransaction = _viewModel?.GetAdjustmentTransaction(
            amount: Convert.ToDecimal(txtAmount.Text),
            comment: txtComment.Text);
        if (_viewModel is null || adjustmentTransaction is null)
            return;
        try
        {
            _viewModel.SetPendingAdjustment(adjustmentTransaction);
            btnSave.Enabled = false;
            await _viewModel.SubmitAdjustmentOperation.ExecuteAsync();
        }
        catch (Exception exception)
        {
            this.ShowErrorMessage(exception.Message, "Fund Transaction Adjustment Error");
            UpdateSaveEnabled();
        }
    }

    private void btnCancel_Click(object sender, EventArgs e)
    {
        DialogResult = DialogResult.Cancel;
        Close();
    }

    private void txtAmount_TextChanged(object sender, EventArgs e)
    {
       UpdateSaveEnabled();
    }

    private void txtComment_TextChanged(object sender, EventArgs e)
    {
         UpdateSaveEnabled();
    }

    void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (_viewModel is null)
            return;
        this.Post(() =>
        {
            if (_viewModel.IsAdjustmentCompleted)
            {
                DialogResult = DialogResult.OK;
                Close();
                return;
            }
            if (_viewModel.AdjustmentFailure is { } failure)
                this.ShowErrorMessage(failure.Message, "Fund Transaction Adjustment Error");
            UpdateSaveEnabled();
        });
    }

    void UpdateSaveEnabled()
        => btnSave.Enabled = _viewModel is not null
            && _viewModel.CommandId == Guid.Empty
            && !_viewModel.SubmitAdjustmentOperation.IsRunning
            && decimal.TryParse(txtAmount.Text, out _)
            && !string.IsNullOrWhiteSpace(txtComment.Text);

    public void Open()
    {
        throw new NotImplementedException();
    }

    void IFormControl.Resize(Control parentControl)
    {
        throw new NotImplementedException();
    }
}
