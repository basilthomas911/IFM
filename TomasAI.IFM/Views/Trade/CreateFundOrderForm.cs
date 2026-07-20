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
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;
using TomasAI.IFM.Shared.Fund;
using TomasAI.IFM.ViewModels.Trade;

namespace TomasAI.IFM.Views.Trade
{
    public partial class CreateFundOrderForm : Form, IForm<CreateFundOrderForm>, IFormControl
    {
        FundOrderEditorViewModel _viewModel;

        public CreateFundOrderForm()
        {
            InitializeComponent();
        }

        public FundOrderReadModel FundOrder => _viewModel.FundOrder;
      
        public void SetViewModel(FundOrderEditorViewModel viewModel)
        {
            _viewModel = viewModel;
        }

        private void CreateFundOrderForm_Load(object sender, EventArgs e)
        {
            txtOrderDate.Text = $"{_viewModel.OrderDate:yyyy-MMM-dd hh:mm tt}";
            txtOrderStatus.Text = $"{_viewModel.OrderStatus}";
            dtpTradeDate.Value = _viewModel.TradeDate;
            dtpMaturityDate.Value = _viewModel.MaturityDate;
            _viewModel.OnNewOrderId = () => this.Post(() => txtOrderId.Text = $"{_viewModel.OrderId}");
            _viewModel.OnReferenceChanged = () => this.Post(() => txtReference.Text = $"{_viewModel.Reference}");
            _viewModel.LoadNewOrderId();
            ddlBaseContracts.Items.Clear();
            foreach (var baseContractId in _viewModel.BaseContractIds)
                ddlBaseContracts.Items.Add(baseContractId);
            if (ddlBaseContracts.Items.Count > 0)
                ddlBaseContracts.SelectedIndex = 0;
        }

        private void CreateFundOrderForm_FormClosed(object sender, FormClosedEventArgs e)
        {
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }

        private void ddlBaseContracts_SelectedIndexChanged(object sender, EventArgs e)
        {
            _viewModel.SetBaseContractId(ddlBaseContracts.SelectedIndex);
        }

        private void dtpTradeDate_ValueChanged(object sender, EventArgs e)
        {
            _viewModel.SetTradeDate(dtpTradeDate.Value);
        }

        private void dtpMaturityDate_ValueChanged(object sender, EventArgs e)
        {
            _viewModel.SetMaturityDate(dtpMaturityDate.Value);
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
