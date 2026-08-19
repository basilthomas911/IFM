using System.ComponentModel;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.ViewModels.Trade;

namespace TomasAI.IFM.UI.Net.Views.Trade;

/// <summary>
/// Transitional WinForms adapter for observable new-fund-order state.
/// </summary>
public partial class CreateFundOrderForm : Form, IForm<CreateFundOrderForm>, IFormControl
{
    FundOrderEditorViewModel _viewModel = null!;
    bool _rendering;
    long _lastErrorSequence;

    public CreateFundOrderForm()
    {
        InitializeComponent();
    }

    public FundOrderReadModel FundOrder => _viewModel.FundOrder;

    public void SetViewModel(FundOrderEditorViewModel viewModel)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _viewModel.PropertyChanged += ViewModelPropertyChanged;
    }

    async void CreateFundOrderForm_Load(object sender, EventArgs e)
    {
        RenderStaticState();
        RenderObservableState();
        try
        {
            await _viewModel.LoadOperation.ExecuteAsync();
            RenderObservableState();
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
            switch (eventArgs.PropertyName)
            {
                case nameof(FundOrderEditorViewModel.OrderId):
                    txtOrderId.Text = $"{_viewModel.OrderId}";
                    break;
                case nameof(FundOrderEditorViewModel.Reference):
                    txtReference.Text = _viewModel.Reference;
                    break;
                case nameof(FundOrderEditorViewModel.SelectedBaseContractId):
                    RenderSelectedBaseContract();
                    break;
                case nameof(FundOrderEditorViewModel.IsBusy):
                case nameof(FundOrderEditorViewModel.CanSave):
                    RenderOperationState();
                    break;
                case nameof(FundOrderEditorViewModel.LastError):
                    RenderLatestError();
                    break;
            }
        });

    void RenderStaticState()
    {
        _rendering = true;
        try
        {
            txtOrderDate.Text = $"{_viewModel.OrderDate:yyyy-MMM-dd hh:mm tt}";
            txtOrderStatus.Text = $"{_viewModel.OrderStatus}";
            dtpTradeDate.Value = _viewModel.TradeDate.ToDateTime(TimeOnly.MinValue);
            dtpMaturityDate.Value = _viewModel.MaturityDate.ToDateTime(TimeOnly.MinValue);
            ddlBaseContracts.Items.Clear();
            ddlBaseContracts.Items.AddRange(_viewModel.BaseContractIds.Cast<object>().ToArray());
            ddlBaseContracts.AccessibleDescription = string.Join(", ", _viewModel.BaseContractIds);
            RenderSelectedBaseContract();
        }
        finally
        {
            _rendering = false;
        }
    }

    void RenderObservableState()
    {
        txtOrderId.Text = _viewModel.OrderId > 0 ? $"{_viewModel.OrderId}" : string.Empty;
        txtReference.Text = _viewModel.Reference;
        RenderSelectedBaseContract();
        RenderOperationState();
        RenderLatestError();
    }

    void RenderSelectedBaseContract()
    {
        var selectedIndex = _viewModel.BaseContractIds
            .Select((contractId, index) => (contractId, index))
            .Where(value => value.contractId == _viewModel.SelectedBaseContractId)
            .Select(value => value.index)
            .DefaultIfEmpty(-1)
            .First();
        if (ddlBaseContracts.SelectedIndex != selectedIndex)
            ddlBaseContracts.SelectedIndex = selectedIndex;
        UpdateBaseContractSelectorAccessibility();
    }

    void UpdateBaseContractSelectorAccessibility()
        => ddlBaseContracts.AccessibleName = $"Base contract selector; selected={ddlBaseContracts.SelectedItem}; "
            + $"catalog: {ddlBaseContracts.AccessibleDescription}";

    void RenderOperationState()
    {
        ddlBaseContracts.Enabled = !_viewModel.IsBusy;
        dtpTradeDate.Enabled = !_viewModel.IsBusy;
        dtpMaturityDate.Enabled = !_viewModel.IsBusy;
        btnSave.Enabled = _viewModel.CanSave;
        UseWaitCursor = _viewModel.IsBusy;
    }

    void RenderLatestError()
    {
        if (_viewModel.LastError is not { } error || error.Sequence <= _lastErrorSequence)
            return;

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
        if (!_viewModel.CanSave)
        {
            this.ShowErrorMessage(
                "A generated order ID, base contract, and valid date range are required.",
                "New Fund Order Error");
            return;
        }

        DialogResult = DialogResult.OK;
        Close();
    }

    void btnCancel_Click(object sender, EventArgs e)
    {
        DialogResult = DialogResult.Cancel;
        Close();
    }

    async void ddlBaseContracts_SelectedIndexChanged(object sender, EventArgs e)
    {
        UpdateBaseContractSelectorAccessibility();
        if (_rendering || !_viewModel.SelectBaseContract(ddlBaseContracts.SelectedIndex))
            return;

        try
        {
            await _viewModel.RefreshReferenceOperation.ExecuteAsync();
        }
        catch (Exception exception)
        {
            ShowOperationFailure(exception, "Futures EOD Data Error");
        }
    }

    void dtpTradeDate_ValueChanged(object sender, EventArgs e)
    {
        if (!_rendering)
            _viewModel.SetTradeDate(DateOnly.FromDateTime(dtpTradeDate.Value));
    }

    void dtpMaturityDate_ValueChanged(object sender, EventArgs e)
    {
        if (!_rendering)
            _viewModel.SetMaturityDate(DateOnly.FromDateTime(dtpMaturityDate.Value));
    }

    void txtReference_TextChanged(object sender, EventArgs e)
    {
        if (!_rendering)
            _viewModel.SetReference(txtReference.Text);
    }

    public void Open() => throw new NotImplementedException();

    void IFormControl.Resize(Control parentControl) => throw new NotImplementedException();
}
