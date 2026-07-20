using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.ViewModels.Trade;

namespace TomasAI.IFM.Views.Trade
{
    public partial class TradeOrderConfirmationForm : Form
    {
        readonly TradeOrderConfirmationViewModel _viewModel;

        public TradeOrderConfirmationForm(TradeOrderConfirmationViewModel viewModel)
        {
            _viewModel = viewModel;
            InitializeComponent();
        }

        private void TradeOrderConfirmationForm_Load(object sender, EventArgs e)
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
            ddlTradeFillType.Items.Add($"{TradeFillType.Manual}");
            ddlTradeFillType.Items.Add($"{TradeFillType.Broker}");
            ddlTradeFillType.SelectedIndex = 0;
            btnCancel.Select();
        }

        private void btnContinue_Click(object sender, EventArgs e) => this.Close();

        private void btnCancel_Click(object sender, EventArgs e) => this.Close();

        private void ddlTradeFillType_SelectedIndexChanged(object sender, EventArgs e)
        {
            var tradeFillType = (TradeFillType )Enum.Parse(typeof(TradeFillType), $"{ddlTradeFillType.SelectedItem}");
            switch (tradeFillType)
            {
                case TradeFillType.Manual:
                    btnContinue.Enabled = true;
                    break;
                case TradeFillType.Broker:
                    /// broker trade fill currently not implemented...
                    btnContinue.Enabled = false;
                    break;
            }
        }
    }
}
