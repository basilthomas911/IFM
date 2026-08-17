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

    private async void CreateFundForm_Load(object sender, EventArgs e)
    {
        txtFundName.Text = string.Empty;
        txtDescription.Text = string.Empty;
        txtInitialBalance.Text = $"{0m}";
        try
        {
            await _viewModel.LoadNewFundIdOperation.ExecuteAsync();
            txtFundId.Text = $"{_viewModel.NewFundId}";
        }
        catch (Exception exception)
        {
            this.ShowErrorMessage(exception.Message, "New Fund Id Error");
        }
    }

    private async void btnSave_Click(object sender, EventArgs e)
    {
        var newFund = new FundReadModel
        (
            fundId: Convert.ToInt32(txtFundId.Text),
            name: txtFundName.Text,
            description: txtDescription.Text,
            balance: Convert.ToDecimal(txtInitialBalance.Text),
            isProduction: false,
            createdBy: $"{Environment.UserDomainName}\\{Environment.UserName}",
            createdOn: DateTime.UtcNow
        );
        if (!newFund.IsValid)
        {
            this.ShowErrorMessage("A valid fund identifier and name are required.", "Create Fund Error");
            return;
        }
        try
        {
            _viewModel.SetPendingFund(newFund);
            btnSave.Enabled = false;
            await _viewModel.CreateFundOperation.ExecuteAsync();
            if (_viewModel.CreatedFund is not null)
            {
                _fund = _viewModel.CreatedFund;
                DialogResult = DialogResult.OK;
                Close();
            }
        }
        catch (Exception exception)
        {
            this.ShowErrorMessage(exception.Message, "Create Fund Error");
        }
        finally
        {
            if (!IsDisposed)
                btnSave.Enabled = true;
        }
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
