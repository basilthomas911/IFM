using System.ComponentModel;
using TomasAI.IFM.UI.Net.Models.Portfolio;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.ViewModels.Trade;

namespace TomasAI.IFM.UI.Net.Views.Trade;

/// <summary>WinForms adapter for observable new-fund-order state.</summary>
public partial class CreateFundOrderForm : DarkTradingForm, IForm<CreateFundOrderForm>, IFormControl
{
    FundOrderEditorViewModel _viewModel = null!;
    long _lastErrorSequence;

    public CreateFundOrderForm() => InitializeComponent();

    public ManualFundOrderDraftEditorModel FundOrder => _viewModel.FundOrder;

    public void SetViewModel(FundOrderEditorViewModel viewModel)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _viewModel.PropertyChanged += ViewModelPropertyChanged;
    }

    async void CreateFundOrderForm_Load(object sender, EventArgs e)
    {
        RenderState();
        try
        {
            await _viewModel.LoadOperation.ExecuteAsync();
            RenderState();
        }
        catch (Exception exception)
        {
            ShowOperationFailure(exception, "New Fund Order Error");
        }
    }

    async void CreateFundOrderForm_FormClosed(object sender, FormClosedEventArgs e)
    {
        _viewModel.PropertyChanged -= ViewModelPropertyChanged;
        try
        {
            await _viewModel.DisposeAsync();
        }
        catch (Exception exception)
        {
            this.ShowErrorMessage(exception.Message, "New Fund Order Close Error");
        }
    }

    void ViewModelPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
        => this.Post(() =>
        {
            if (eventArgs.PropertyName == nameof(FundOrderEditorViewModel.LastError))
                RenderLatestError();
            else
                RenderState();
        });

    void RenderState()
    {
        txtOrderId.Text = _viewModel.OrderId > 0 ? $"{_viewModel.OrderId}" : "Allocated on Save";
        txtOrderDate.Text = $"{_viewModel.OrderDate:yyyy-MMM-dd hh:mm tt}";
        txtOrderStatus.Text = $"{_viewModel.OrderStatus}";
        if (txtReference.Text != _viewModel.Reference)
            txtReference.Text = _viewModel.Reference;
        txtReference.Enabled = !_viewModel.IsBusy;
        btnSave.Enabled = true;
        UseWaitCursor = _viewModel.IsBusy;
    }

    void RenderLatestError()
    {
        if (_viewModel.LastError is not { } error || error.Sequence <= _lastErrorSequence) return;
        _lastErrorSequence = error.Sequence;
        this.ShowErrorMessage(error.Message, error.Caption);
    }

    void ShowOperationFailure(Exception exception, string caption)
    {
        if (_viewModel.LastError?.Message == exception.Message)
        {
            RenderLatestError();
            return;
        }
        this.ShowErrorMessage(exception.Message, caption);
    }

    void btnSave_Click(object sender, EventArgs e)
    {
        DialogResult = DialogResult.OK;
        Close();
    }

    void btnCancel_Click(object sender, EventArgs e)
    {
        DialogResult = DialogResult.Cancel;
        Close();
    }

    void txtReference_TextChanged(object sender, EventArgs e)
        => _viewModel.SetReference(txtReference.Text);

    public void Open() => throw new NotImplementedException();
    void IFormControl.Resize(Control parentControl) => throw new NotImplementedException();
}