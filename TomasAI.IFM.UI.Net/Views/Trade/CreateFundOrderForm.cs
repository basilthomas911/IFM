using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.ViewModels.Trade;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;

namespace TomasAI.IFM.UI.Net.Views.Trade;

public partial class CreateFundOrderForm : Form, IForm<CreateFundOrderForm>, IFormControl
{
    FundOrderEditorViewModel _viewModel = null!;

    public CreateFundOrderForm()
    {
        InitializeComponent();
    }

    public FundOrderReadModel FundOrder => _viewModel.FundOrder;
  
    public void SetViewModel(FundOrderEditorViewModel viewModel)
    {
        _viewModel = viewModel;
    }

    void CreateFundOrderForm_Load(object sender, EventArgs e)
    {
        txtOrderDate.Text = $"{_viewModel.OrderDate:yyyy-MMM-dd hh:mm tt}";
        txtOrderStatus.Text = $"{_viewModel.OrderStatus}";
        dtpTradeDate.Value = _viewModel.TradeDate.ToDateTime(TimeOnly.MinValue);
        dtpMaturityDate.Value = _viewModel.MaturityDate.ToDateTime(TimeOnly.MinValue);
        _viewModel.OnNewOrderId = () => this.Post(() => txtOrderId.Text = $"{_viewModel.OrderId}");
        _viewModel.OnReferenceChanged = () => this.Post(() => txtReference.Text = $"{_viewModel.Reference}");
        _viewModel.LoadNewOrderId();
        ddlBaseContracts.Items.Clear();
        foreach (var baseContractId in _viewModel.BaseContractIds)
            ddlBaseContracts.Items.Add(baseContractId);
        if (ddlBaseContracts.Items.Count > 0)
            ddlBaseContracts.SelectedIndex = 0;
    }

    void CreateFundOrderForm_FormClosed(object sender, FormClosedEventArgs e)
    {
    }

    void btnSave_Click(object sender, EventArgs e)
    {
        this.DialogResult = DialogResult.OK;
        this.Close();
    }

    void btnCancel_Click(object sender, EventArgs e)
    {
        this.DialogResult = DialogResult.Cancel;
        this.Close();
    }

    void ddlBaseContracts_SelectedIndexChanged(object sender, EventArgs e)
    {
        _viewModel.SetBaseContractId(ddlBaseContracts.SelectedIndex);
    }

    void dtpTradeDate_ValueChanged(object sender, EventArgs e)
    {
        _viewModel.SetTradeDate(DateOnly.FromDateTime(dtpTradeDate.Value));
    }

    private void dtpMaturityDate_ValueChanged(object sender, EventArgs e)
    {
        _viewModel.SetMaturityDate(DateOnly.FromDateTime(dtpMaturityDate.Value));
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
