using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Models;
using TomasAI.IFM.UI.Net.ViewModels.Trade;
using TomasAI.IFM.UI.Net.Models.Portfolio;

namespace TomasAI.IFM.UI.Net.Views.Trade;

public partial class CreateFundOrderTradeForm : DarkTradingForm, IForm<CreateFundOrderTradeForm>, IFormControl
{
    TradeOrderEditorViewModel? _viewModel;
    PortfolioFundOrderTradeEditorModel? _fundOrderTrade;
    PortfolioFundOrderTradeEditorModel? _openingTrade;
    readonly Dictionary<string, FuturesContractV3ReadModel> _baseContractMap = [];
    readonly ComboBox _tradeStrategySelector = new()
    {
        Name = "ddlTradeStrategy", DropDownStyle = ComboBoxStyle.DropDownList,
        Font = new Font("Microsoft Sans Serif", 10.2F), Dock = DockStyle.Left, Width = 311
    };
    bool _loadingTradeSelectors;

    public PortfolioFundOrderTradeEditorModel FundOrderTrade => _fundOrderTrade!;

    public CreateFundOrderTradeForm()
    {
        InitializeComponent();
        ConfigureTradeStrategySelector();
        txtReference.ReadOnly = true;
        ddlBaseSymbol.SelectedIndexChanged += ddlBaseSymbol_SelectedIndexChanged;
        dtpTradeDate.ValueChanged += TradeReferenceInputChanged;
        dtpMaturityDate.ValueChanged += TradeReferenceInputChanged;
    }

    /// <summary>Assigns the canonical trade-order editor view model.</summary>
    /// <param name="viewModel">The editor view model.</param>
    public void SetViewModel(TradeOrderEditorViewModel viewModel) => _viewModel = viewModel;

    /// <summary>Assigns the canonical Portfolio Fund order being edited.</summary>
    /// <param name="fundOrder">The selected order.</param>
    public void SetFundOrder(PortfolioFundOrderEditorModel fundOrder)
    {
        _openingTrade = fundOrder.Trades.FirstOrDefault(trade => trade.PrimaryTrade);
        var tradeDate = _openingTrade?.RequestedTradeDate
            ?? _viewModel?.ValueDate
            ?? DateOnly.FromDateTime(EasternTime.GetNow(TimeProvider.System));
        var maturityDate = _openingTrade?.RequestedMaturityDate
            ?? _viewModel?.BaseContracts.FirstOrDefault()?.LastTradeDate
            ?? tradeDate;
        dtpTradeDate.Value = tradeDate.ToDateTime(TimeOnly.MinValue);
        dtpTradeDate.Enabled = _openingTrade is null;
        dtpMaturityDate.Value = maturityDate.ToDateTime(TimeOnly.MinValue);
        dtpMaturityDate.Enabled = _openingTrade is null;
        UpdateTradeReference();
    }

    private void LoadTradeTypes()
    {
        _loadingTradeSelectors = true;
        ddlTradeType.Enabled = false;
        ddlTradeType.Items.Clear();
        var types = $"{_tradeStrategySelector.SelectedItem}" switch
        {
            "Iron Condor" => new[] { TradeType.ShortIronCondor, TradeType.LongIronCondor },
            "Vertical Spread" => new[] { TradeType.PutCreditSpread, TradeType.PutDebitSpread,
                TradeType.CallCreditSpread, TradeType.CallDebitSpread },
            "Futures Outright" => new[] { TradeType.FuturesOutright },
            _ => []
        };
        foreach (var tradeType in types)
            ddlTradeType.Items.Add($"{tradeType}");
        ddlTradeType.SelectedIndex = 0;
        UpdateSelectorAccessibility(ddlTradeType, "Trade type selector");
        ddlTradeType.Enabled = true;
        _loadingTradeSelectors = false;
    }

    private async void CreateFundOrderTradeForm_Load(object sender, EventArgs e)
    {
        try
        {
            var tradeId = await _viewModel!.GetNewTradeIdAsync();
            this.Post(() =>
            {
                txtTradeId.Text = $"{tradeId}";
                txtTradeState.Text = $"{TradeState.NewTrade}";
                _tradeStrategySelector.SelectedIndex = 0;
                LoadTradeTypes();
                LoadBaseContracts(_viewModel.BaseContracts);
                ConfigureClosingTrade();
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Create Fund Order Trade Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    void ConfigureTradeStrategySelector()
    {
        foreach (Control control in tableLayoutPanel1.Controls.Cast<Control>().ToArray())
        {
            var row = tableLayoutPanel1.GetRow(control);
            if (row >= 1)
                tableLayoutPanel1.SetRow(control, row + 1);
        }
        var label = new Label
        {
            Name = "lblTradeStrategy", Text = "Trade Strategy:", Dock = DockStyle.Fill,
            ForeColor = SystemColors.ControlLightLight, Font = new Font("Microsoft Sans Serif", 10.2F),
            TextAlign = ContentAlignment.MiddleRight
        };
        tableLayoutPanel1.Controls.Add(label, 0, 1);
        tableLayoutPanel1.Controls.Add(_tradeStrategySelector, 1, 1);
        tableLayoutPanel1.SetRow(pnlBaseContractSymbol, 7);
        tableLayoutPanel1.SetRow(ddlBaseSymbol, 7);
        tableLayoutPanel1.SetRow(panel2, 8);
        tableLayoutPanel1.SetRow(txtReference, 8);
        _tradeStrategySelector.Items.AddRange(["Iron Condor", "Vertical Spread", "Futures Outright"]);
        _tradeStrategySelector.SelectedIndexChanged += (_, _) =>
        {
            if (!_loadingTradeSelectors)
                LoadTradeTypes();
        };
    }

    void SetClosingTradeType(TradeType openingTradeType)
    {
        var closingTradeType = PortfolioFundOrderEditorPolicy.ClosingType(openingTradeType);
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

    void LoadBaseContracts(IReadOnlyList<FuturesContractV3ReadModel> contracts)
    {
        ddlBaseSymbol.Enabled = false;
        ddlBaseSymbol.Items.Clear();
        _baseContractMap.Clear();
        foreach (var contract in contracts
                     .Where(contract => !string.IsNullOrWhiteSpace(contract.ContractId))
                     .OrderBy(contract => contract.Symbol)
                     .ThenBy(contract => contract.LastTradeDate))
        {
            if (_baseContractMap.TryAdd(contract.ContractId, contract))
                ddlBaseSymbol.Items.Add(contract.ContractId);
        }
        if (ddlBaseSymbol.Items.Count > 0)
        {
            ddlBaseSymbol.SelectedIndex = 0;
            UpdateSelectorAccessibility(ddlBaseSymbol, "Base contract selector");
            ddlBaseSymbol.Enabled = true;
        }
    }

    void ConfigureClosingTrade()
    {
        if (_openingTrade is null)
            return;

        _loadingTradeSelectors = true;
        _tradeStrategySelector.SelectedItem = StrategyFor(_openingTrade.TradeType);
        _loadingTradeSelectors = false;
        LoadTradeTypes();
        SetClosingTradeType(_openingTrade.TradeType);
        _tradeStrategySelector.Enabled = false;
        ddlTradeType.Enabled = false;

        var matchingContract = _baseContractMap.Values.FirstOrDefault(contract =>
            string.Equals(contract.ContractId, _openingTrade.BaseContractId, StringComparison.OrdinalIgnoreCase))
            ?? _baseContractMap.Values.FirstOrDefault(contract =>
                string.Equals(contract.Symbol, _openingTrade.BaseContractSymbol, StringComparison.OrdinalIgnoreCase));
        if (matchingContract is not null)
            ddlBaseSymbol.SelectedItem = matchingContract.ContractId;
        else
        {
            ddlBaseSymbol.Items.Add(_openingTrade.BaseContractId);
            ddlBaseSymbol.SelectedItem = _openingTrade.BaseContractId;
        }
        ddlBaseSymbol.Enabled = false;
        UpdateSelectorAccessibility(ddlBaseSymbol, "Base contract selector");
        UpdateTradeReference();
    }

    static string StrategyFor(TradeType tradeType) => tradeType switch
    {
        TradeType.ShortIronCondor or TradeType.LongIronCondor => "Iron Condor",
        TradeType.FuturesOutright => "Futures Outright",
        _ => "Vertical Spread"
    };

    PortfolioFundOrderTradeEditorModel? ValidateNewFundOrderTrade()
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
        var selectedContract = _baseContractMap.GetValueOrDefault($"{ddlBaseSymbol.SelectedItem}");
        var baseContractId = string.IsNullOrWhiteSpace(_openingTrade?.BaseContractId)
            ? selectedContract?.ContractId
            : _openingTrade.BaseContractId;
        var baseContractSymbol = string.IsNullOrWhiteSpace(_openingTrade?.BaseContractSymbol)
            ? selectedContract?.Symbol
            : _openingTrade.BaseContractSymbol;
        if (string.IsNullOrWhiteSpace(baseContractId) || string.IsNullOrWhiteSpace(baseContractSymbol))
        {
            MessageBox.Show("A valid base contract is required", "Fund Order Trade Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return null;
        }
        var tradeDate = DateOnly.FromDateTime(dtpTradeDate.Value);
        var maturityDate = DateOnly.FromDateTime(dtpMaturityDate.Value);
        var reference = FundOrderTradeReference.Create(baseContractId, tradeDate, maturityDate);
        txtReference.Text = reference;

        return new PortfolioFundOrderTradeEditorModel
        {
            TradeId = tradeId,
            TradeFamily = tradeType.ToString(),
            TradeType = tradeType,
            RequestedTradeDate = tradeDate,
            RequestedMaturityDate = maturityDate,
            TradeState = tradeState,
            TradeAction = tradeAction,
            InstructionReference = reference,
            PrimaryTrade = true,
            UnderlyingRoot = baseContractSymbol,
            BaseContractSymbol = baseContractSymbol,
            BaseContractId = baseContractId,
            CreatedBy = $"{Environment.UserDomainName}\\{Environment.UserName}",
            CreatedOnUtc = DateTime.UtcNow,
        };
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
    {
        UpdateSelectorAccessibility(ddlBaseSymbol, "Base contract selector");
        if (_openingTrade is null
            && _baseContractMap.GetValueOrDefault($"{ddlBaseSymbol.SelectedItem}") is { } contract
            && contract.LastTradeDate >= DateOnly.FromDateTime(dtpTradeDate.Value))
            dtpMaturityDate.Value = contract.LastTradeDate.ToDateTime(TimeOnly.MinValue);
        UpdateTradeReference();
    }

    void TradeReferenceInputChanged(object? sender, EventArgs e) => UpdateTradeReference();

    void UpdateTradeReference()
    {
        var baseContractId = string.IsNullOrWhiteSpace(_openingTrade?.BaseContractId)
            ? $"{ddlBaseSymbol.SelectedItem}".Trim()
            : _openingTrade.BaseContractId.Trim();
        txtReference.Text = string.IsNullOrWhiteSpace(baseContractId)
            ? string.Empty
            : FundOrderTradeReference.Create(
                baseContractId,
                DateOnly.FromDateTime(dtpTradeDate.Value),
                DateOnly.FromDateTime(dtpMaturityDate.Value));
    }

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
