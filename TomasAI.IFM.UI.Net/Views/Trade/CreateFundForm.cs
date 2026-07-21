using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.ViewModels.Fund;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;

namespace TomasAI.IFM.UI.Net.Views.Trade;

public partial class CreateFundForm : Form, IFormControl
{
    readonly CreateFundReadModel _viewModel;
    FundReadModel _fund = null!;

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
            fundId: Convert.ToInt32(txtFundId.Text),
            name: txtFundName.Text,
            description: txtDescription.Text,
            balance: Convert.ToDecimal(txtInitialBalance.Text),
            isProduction: false,
            createdBy: $"{Environment.UserDomainName}\\{Environment.UserName}",
            createdOn: DateTime.Now
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
