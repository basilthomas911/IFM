using TomasAI.IFM.UI.Net.ViewModels.Trade;

namespace TomasAI.IFM.UI.Net.Views.Trade;

/// <summary>WinForms adapter for framework-neutral trade-order confirmation state.</summary>
public partial class TradeOrderConfirmationForm : Form
{
    readonly TradeOrderConfirmationViewModel _viewModel;

    public TradeOrderConfirmationForm(TradeOrderConfirmationViewModel viewModel)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        InitializeComponent();
    }

    void TradeOrderConfirmationForm_Load(object sender, EventArgs e)
    {
        txtName.Text = $"{_viewModel.TradeOrder.TradeType}";
        txtDescription.Text = _viewModel.TradeOrder.OrderDescription;
        txtAction.Text = $"{_viewModel.TradeOrder.OrderAction} {_viewModel.TradeOrder.OrderQuantity}";
        txtOrderPrice.Text = $"{_viewModel.TradeOrder.OrderPrice:F2}";
        txtOrderType.Text = $"{_viewModel.TradeOrder.OrderType}";
        txtOrderAmount.Text = $"{_viewModel.TradeOrder.OrderAmount:C}";
        txtCommission.Text = $"{_viewModel.TradeOrder.Commission:C}";
        txtTotalAmount.Text = $"{_viewModel.TradeOrder.TotalAmount:C}";
        ddlTradeFillType.Items.Clear();
        foreach (var tradeFillType in _viewModel.TradeFillTypes)
            ddlTradeFillType.Items.Add($"{tradeFillType}");
        ddlTradeFillType.SelectedIndex = _viewModel.TradeFillTypes
            .ToList()
            .IndexOf(_viewModel.SelectedTradeFillType);
        btnContinue.Enabled = _viewModel.CanConfirm;
        btnCancel.Select();
    }

    void btnContinue_Click(object sender, EventArgs e) => Close();
    void btnCancel_Click(object sender, EventArgs e) => Close();

    void ddlTradeFillType_SelectedIndexChanged(object sender, EventArgs e)
    {
        _viewModel.SelectTradeFillType(ddlTradeFillType.SelectedIndex);
        btnContinue.Enabled = _viewModel.CanConfirm;
    }
}
