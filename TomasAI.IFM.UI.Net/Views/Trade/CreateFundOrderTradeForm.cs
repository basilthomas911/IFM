using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Models;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Reference.ViewModels;
using TomasAI.IFM.UI.Net.ViewModels.Trade;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;

namespace TomasAI.IFM.UI.Net.Views.Trade;

public partial class CreateFundOrderTradeForm : Form, IForm<CreateFundOrderTradeForm>, IFormControl
{
    readonly IAppRoot _appRoot;
    TradeOrderEditorViewModel? _viewModel;
    FundOrderTradeReadModel? _fundOrderTrade;
    Dictionary<string, LookupTypeReadModel> _baseSymbolMap;

    public FundOrderTradeReadModel FundOrderTrade => _fundOrderTrade!;

    public CreateFundOrderTradeForm(IAppRoot appRoot)
    {
        _appRoot = appRoot;
        _baseSymbolMap = [];
        InitializeComponent();
      }

    public void SetViewModel(TradeOrderEditorViewModel viewModel) => _viewModel = viewModel;

    public void SetFundOrder(FundOrderReadModel fundOrder)
    {
        dtpTradeDate.Value = fundOrder.TradeDate.ToDateTime(TimeOnly.MinValue);
        dtpTradeDate.Enabled = false;
        dtpMaturityDate.Value = fundOrder.MaturityDate.ToDateTime(TimeOnly.MinValue);
        dtpMaturityDate.Enabled = false;    
    }

    private void LoadTradeTypes()
    {
        ddlTradeType.Enabled = false;
        ddlTradeType.Items.Clear();
        ddlTradeType.Items.Add($"{TradeType.ShortIronCondor}");
        ddlTradeType.Items.Add($"{TradeType.LongIronCondor}");
        ddlTradeType.SelectedIndex = 0;
        ddlTradeType.Enabled = true;
    }

    private void CreateFundOrderTradeForm_Load(object sender, EventArgs e)
    {
        _appRoot.GetModel<ReferenceQueryModel>().Execute(async model =>
        {
            await model.GetCurrentTradeIdAsync(currentTradeId =>
                this.Post(() =>
                {
                    txtTradeId.Text = $"{currentTradeId + 1}";
                    txtTradeState.Text = $"{TradeState.NewTrade}";
                    LoadTradeTypes();
                    var openingFundOrderTrade = _viewModel!.GetOpeningFundOrderTrade();
                    if (openingFundOrderTrade != null)
                    {
                        SetClosingTradeType(openingFundOrderTrade!.TradeType);
                        txtReference.Text = openingFundOrderTrade.Reference;
                    }
                }));

            await model.LoadSymbolsAsync(symbols =>
                this.Post(() => LoadSymbols([.. symbols])));
        });
        return;

        void SetClosingTradeType(TradeType openingTradeType)
        {
            var closingTradeType = openingTradeType switch
            {
                TradeType.ShortIronCondor => TradeType.LongIronCondor,
                TradeType.LongIronCondor => TradeType.ShortIronCondor,
                _ => throw new NotImplementedException()
            };
            for (var index = 0; index < ddlTradeType.Items.Count; index++)
                if ($"{ddlTradeType.Items[index]}" == $"{closingTradeType}")
                {
                    ddlTradeType.SelectedIndex = index;
                    break;
                }
        }
    }

    void CreateFundOrderTradeForm_FormClosed(object sender, FormClosedEventArgs e)
    {
    }

    void LoadSymbols(LookupTypeReadModel[] lookupTypes)
    {
        ddlBaseSymbol.Enabled = false;
        ddlBaseSymbol.Items.Clear();
        if (lookupTypes?.Length > 0)
        {
            foreach (var e in lookupTypes)
            {
                _baseSymbolMap.Add(e.Description, e);
                ddlBaseSymbol.Items.Add(e.Description);
            }
            ddlBaseSymbol.SelectedIndex = 0;
            ddlBaseSymbol.Enabled = true;
        }
    }

    FundOrderTradeReadModel? ValidateNewFundOrderTrade()
    {
        if (!int.TryParse(txtTradeId.Text, out int tradeId))
        {
            MessageBox.Show("Invalid Trade Id", "Fund Order Trade Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return null;
        }
        if (!Enum.TryParse($"{ddlTradeType.SelectedItem ?? string.Empty}", out TradeType tradeType))
        {
            MessageBox.Show("Invalid Trade Type", "Fund Order Trade Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return null;
        }
        if (!Enum.TryParse(txtTradeState.Text, out TradeState tradeState))
        {
            MessageBox.Show("Invalid Trade State", "Fund Order Trade Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return null;
        }
        if (!Enum.TryParse(txtTradeAction.Text, out TradeAction tradeAction))
        {
            MessageBox.Show("Invalid Trade Action", "Fund Order Trade Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return null;
        }
        var lookupType = _baseSymbolMap.SingleOrDefault(e => e.Key == $"{ddlBaseSymbol.SelectedItem}").Value;
        if (lookupType is null)
        {
            MessageBox.Show("Invalid Base Symbol", "Fund Order Trade Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return null;
        }

        return new FundOrderTradeReadModel(
            fundId: 0,
            orderId: 0,
            tradeId: tradeId,
            tradeType: tradeType,
            tradeDate: DateOnly.FromDateTime(dtpTradeDate.Value),
            maturityDate: DateOnly.FromDateTime(dtpMaturityDate.Value),
            tradeState: tradeState,
            tradeAction: tradeAction,
            reference: txtReference.Text,
            primaryTrade: true,
            baseContractSymbol: lookupType.ShortCode,
            createdBy: $"{Environment.UserDomainName}\\{Environment.UserName}",
            createdOn: DateTime.Now,
            updatedBy: $"{Environment.UserDomainName}\\{Environment.UserName}",
            updatedOn: DateTime.Now
        );
    }

 
    

    void btnSave_Click(object sender, EventArgs e)
    {
        _fundOrderTrade = ValidateNewFundOrderTrade();
        if (_fundOrderTrade is not null)
        {
            DialogResult = DialogResult.OK;
            Close();
        }
    }

    void btnCancel_Click(object sender, EventArgs e)
    {
        _fundOrderTrade = null;
        DialogResult = DialogResult.Cancel;
        Close();
    }

    void ddlTradeType_SelectedIndexChanged(object sender, EventArgs e)
    {
        var tradeType = Enum.Parse<TradeType>($"{ddlTradeType.SelectedItem}");
        txtTradeAction.Text = tradeType switch { 
            TradeType.ShortIronCondor => $"{TradeAction.Sell}",
            TradeType.LongIronCondor => $"{TradeAction.Buy}",
            _ => throw new NotImplementedException()
        };
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
