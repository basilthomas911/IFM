using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Models.Reference;
using TomasAI.IFM.UI.Net.ViewModels.Trade;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;
using TomasAI.IFM.Domain.Fund.Shared;

namespace TomasAI.IFM.UI.Net.Views.Trade;

public partial class CreateFundOrderTradeForm : DarkTradingForm, IForm<CreateFundOrderTradeForm>, IFormControl
{
    TradeOrderEditorViewModel? _viewModel;
    FundOrderTradeReadModel? _fundOrderTrade;
    FundOrderTradeReadModel? _openingTrade;
    Dictionary<string, LookupTypeUiModel> _baseSymbolMap;

    public FundOrderTradeReadModel FundOrderTrade => _fundOrderTrade!;

    public CreateFundOrderTradeForm()
    {
        _baseSymbolMap = [];
        InitializeComponent();
        ddlBaseSymbol.SelectedIndexChanged += ddlBaseSymbol_SelectedIndexChanged;
    }

    public void SetViewModel(TradeOrderEditorViewModel viewModel) => _viewModel = viewModel;

    public void SetFundOrder(FundOrderReadModel fundOrder)
    {
        _openingTrade = fundOrder.Trades.FirstOrDefault(trade => trade.PrimaryTrade);
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
        ddlTradeType.Items.Add($"{TradeType.PutCreditSpread}");
        ddlTradeType.Items.Add($"{TradeType.PutDebitSpread}");
        ddlTradeType.Items.Add($"{TradeType.CallCreditSpread}");
        ddlTradeType.Items.Add($"{TradeType.CallDebitSpread}");
        ddlTradeType.Items.Add($"{TradeType.FuturesOutright}");
        ddlTradeType.SelectedIndex = 0;
        UpdateSelectorAccessibility(ddlTradeType, "Trade type selector");
        ddlTradeType.Enabled = true;
    }

    private async void CreateFundOrderTradeForm_Load(object sender, EventArgs e)
    {
        try
        {
            var tradeId = await _viewModel!.GetNewTradeIdAsync();
            var symbols = await _viewModel.GetSymbolsAsync();
            this.Post(() =>
            {
                txtTradeId.Text = $"{tradeId}";
                txtTradeState.Text = $"{TradeState.NewTrade}";
                LoadTradeTypes();
                LoadSymbols([.. symbols]);
                ConfigureClosingTrade();
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Create Fund Order Trade Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    void SetClosingTradeType(TradeType openingTradeType)
    {
        var closingTradeType = FundOrderTradingPolicy.ClosingType(openingTradeType);
        for (var index = 0; index < ddlTradeType.Items.Count; index++)
            if ($"{ddlTradeType.Items[index]}" == $"{closingTradeType}")
            {
                ddlTradeType.SelectedIndex = index;
                break;
            }
    }

    void CreateFundOrderTradeForm_FormClosed(object sender, FormClosedEventArgs e)
    {
    }

    void LoadSymbols(LookupTypeUiModel[] lookupTypes)
    {
        ddlBaseSymbol.Enabled = false;
        ddlBaseSymbol.Items.Clear();
        _baseSymbolMap.Clear();
        if (lookupTypes?.Length > 0)
        {
            foreach (var e in lookupTypes)
            {
                _baseSymbolMap.Add(e.Description, e);
                ddlBaseSymbol.Items.Add(e.Description);
            }
            ddlBaseSymbol.SelectedIndex = 0;
            UpdateSelectorAccessibility(ddlBaseSymbol, "Base symbol selector");
            ddlBaseSymbol.Enabled = true;
        }
    }

    void ConfigureClosingTrade()
    {
        if (_openingTrade is null)
            return;

        SetClosingTradeType(_openingTrade.TradeType);
        ddlTradeType.Enabled = false;
        txtReference.Text = _openingTrade.Reference;
        txtReference.ReadOnly = true;

        var matchingSymbol = _baseSymbolMap
            .FirstOrDefault(entry => string.Equals(
                entry.Value.ShortCode,
                _openingTrade.BaseContractSymbol,
                StringComparison.OrdinalIgnoreCase));
        if (matchingSymbol.Value is not null)
            ddlBaseSymbol.SelectedItem = matchingSymbol.Key;
        else
        {
            ddlBaseSymbol.Items.Add(_openingTrade.BaseContractSymbol);
            ddlBaseSymbol.SelectedItem = _openingTrade.BaseContractSymbol;
        }
        ddlBaseSymbol.Enabled = false;
        UpdateSelectorAccessibility(ddlBaseSymbol, "Base symbol selector");
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
        var baseContractSymbol = _openingTrade?.BaseContractSymbol;
        if (string.IsNullOrWhiteSpace(baseContractSymbol))
            baseContractSymbol = _baseSymbolMap
                .SingleOrDefault(e => e.Key == $"{ddlBaseSymbol.SelectedItem}")
                .Value?.ShortCode;
        if (string.IsNullOrWhiteSpace(baseContractSymbol))
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
            baseContractSymbol: baseContractSymbol,
            createdBy: $"{Environment.UserDomainName}\\{Environment.UserName}",
            createdOn: DateTime.UtcNow,
            updatedBy: $"{Environment.UserDomainName}\\{Environment.UserName}",
            updatedOn: DateTime.UtcNow
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
        UpdateSelectorAccessibility(ddlTradeType, "Trade type selector");
        if (ddlTradeType.SelectedItem is null)
            return;
        var tradeType = Enum.Parse<TradeType>($"{ddlTradeType.SelectedItem}");
        txtTradeAction.Text = tradeType switch {
            TradeType.ShortIronCondor => $"{TradeAction.Sell}",
            TradeType.LongIronCondor => $"{TradeAction.Buy}",
            TradeType.PutCreditSpread or TradeType.CallCreditSpread => $"{TradeAction.Sell}",
            TradeType.PutDebitSpread or TradeType.CallDebitSpread or TradeType.FuturesOutright => $"{TradeAction.Buy}",
            _ => throw new NotImplementedException()
        };
    }

    void ddlBaseSymbol_SelectedIndexChanged(object? sender, EventArgs e)
        => UpdateSelectorAccessibility(ddlBaseSymbol, "Base symbol selector");

    static void UpdateSelectorAccessibility(ComboBox selector, string label)
    {
        selector.AccessibleDescription = string.Join(", ", selector.Items.Cast<object>());
        selector.AccessibleName = $"{label}; selected={selector.SelectedItem}; "
            + $"catalog: {selector.AccessibleDescription}";
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
