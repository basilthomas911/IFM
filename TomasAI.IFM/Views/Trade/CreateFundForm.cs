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
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;
using TomasAI.IFM.ViewModels.Fund;

namespace TomasAI.IFM.Views.Trade
{
    public partial class CreateFundForm : Form, IFormControl
    {
        private readonly CreateFundReadModel _viewModel;
        private FundReadModel _fund;

        public FundReadModel Fund => _fund;

        public CreateFundForm(CreateFundReadModel viewModel)
        {
            _viewModel = viewModel;
            InitializeComponent();
        }

        private void CreateFundForm_Load(object sender, EventArgs e)
        {
            txtFundName.Text = string.Empty;
            txtDescription.Text = string.Empty;
            txtInitialBalance.Text = $"{0m}";
            _viewModel.LoadNewFundId(
                newFundIdAction: newFundId => this.Invoke(() => txtFundId.Text = $"{newFundId}"),
                onError: errorMsg => this.ShowErrorMessage(errorMsg, "New Fund Id Error"));
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            var newFund = new FundReadModel
            (
                FundId: Convert.ToInt32(txtFundId.Text),
                Name: txtFundName.Text,
                Description: txtDescription.Text,
                Balance: Convert.ToDecimal(txtInitialBalance.Text),
                IsProduction: false,
                CreatedBy: $"{Environment.UserDomainName}\\{Environment.UserName}",
                CreatedOn: DateTime.Now
            );
            _viewModel.CreateNewFund(newFund,
                onCompleted: () => 
                    this.Post(() =>  {
                        _fund = newFund;
                        DialogResult = DialogResult.OK;
                        this.Close();
                    }),
                onError: errorMsg => this.ShowErrorMessage(errorMsg, "Create Fund Error"));
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            this.Close();
        }

        void IFormControl.Resize(Control parentControl)
        {
        }

        public void Open()
        {
            throw new NotImplementedException();
        }
    }
}
