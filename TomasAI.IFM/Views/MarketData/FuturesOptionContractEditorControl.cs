using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using TomasAI.IFM.Contracts;
using TomasAI.IFM.Models;
using TomasAI.IFM.Shared.Reference.ViewModels;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.ViewModels.MarketData;

namespace TomasAI.IFM.Views.MarketData
{
    public partial class FuturesOptionContractEditorControl : UserControl, IControlCommand, IFormControl
    {
        FuturesOptionContractEditorViewModel _viewModel;
        EditMode _editMode;
        int _lastContractIndex;
        string _originalContractId;
        ListBinding<ICollection<LookupTypeReadModel>> _optionTypeBinding;

        public bool CanChangeRemove => lstFuturesOptionContractIds.Items.Count > 0;

        public bool CanImport => false;

        public FuturesOptionContractEditorControl(FuturesOptionContractEditorViewModel viewModel)
        {
            _viewModel = viewModel;
            _editMode = EditMode.View;
            InitializeComponent();
        }

        void IControlCommand.Load(IAppRoot appRoot, Action<bool> dataLoaded)
        {
            _editMode = EditMode.View;
            _viewModel.StartListener();
            _viewModel.OnDataLoaded = dataLoaded;
            _viewModel.OnError = (_, errorMsg) => this.Post(() =>
                MessageBox.Show(
                    text: errorMsg,
                    caption: "Futures Option Contract Editor Error",
                    buttons: MessageBoxButtons.OK,
                    icon: MessageBoxIcon.Error));

            
            _viewModel.StartWaitIndicator = () => this.Post(() => Cursor.Current = Cursors.WaitCursor);
            _viewModel.StopWaitIndicator = () => this.Post(() => Cursor.Current = Cursors.Default);
            _viewModel.OnFuturesOptionContractAdded = () => this.Post(() =>
            {
                _editMode = EditMode.View;
                _viewModel.OnAddAction(true);
                LoadFuturesOptionContractIds(txtContractId.Text);
            });
            _viewModel.OnFuturesOptionContractChanged = () => this.Post(() =>
            {
                _editMode = EditMode.View;
                _viewModel.OnChangeAction(true);
                LoadFuturesOptionContractIds(txtContractId.Text);
            });
            _viewModel.OnFuturesOptionContractRemoved = () => this.Post(() =>
            {
                _editMode = EditMode.View;
                LoadFuturesOptionContractIds();
            });

            _optionTypeBinding = new ListBinding<ICollection<LookupTypeReadModel>>(
                "Description", 
                ddlOptionType, 
                async () => await _viewModel.GetOptionTypesAsync(), 
                selectedIndex => _viewModel.GetOptionType(selectedIndex).ShortCode).Load( LoadSymbols );
            
            _viewModel.LoadSecurityTypes(securityTypes => this.Post(() => DisplaySecurityTypes(securityTypes)));
            _viewModel.LoadCurrencies(currencyTypes => this.Post(() => DisplayCurrencyTypes(currencyTypes)));
            _viewModel.LoadExchanges(exchangeTypes => this.Post(() => DisplayExchangeTypes(exchangeTypes)));
            _viewModel.LoadMultipliers(multiplierTypes => this.Post(() => DisplayMultiplierTypes(multiplierTypes)));
            return;

            void DisplaySecurityTypes(LookupTypeReadModel[] securityTypes)
                => PopulateLookupList(ddlSecurityType, securityTypes, () => LoadSymbols());

            void DisplayCurrencyTypes(LookupTypeReadModel[] currencyTypes)
                => PopulateLookupList(ddlCurrency, currencyTypes, () => LoadSymbols());

            void DisplayExchangeTypes(LookupTypeReadModel[] exchangeTypes)
                => PopulateLookupList(ddlExchange, exchangeTypes, () => LoadSymbols());

            void DisplayMultiplierTypes(LookupTypeReadModel[] multiplierTypes)
                => PopulateLookupList(ddlMultiplier, multiplierTypes, () => LoadSymbols());

            void LoadSymbols()
            {
                if (!AllLookupTypesLoaded()) return;
                _viewModel.LoadSymbols(symbolTypes => this.Post(() => PopulateLookupList(ddlSymbol, symbolTypes)));
            }

            bool AllLookupTypesLoaded()
               => ddlOptionType.Items.Count > 0 &&
                   ddlSecurityType.Items.Count > 0 &&
                   ddlCurrency.Items.Count > 0 &&
                   ddlExchange.Items.Count > 0 &&
                   ddlMultiplier.Items.Count > 0;

            void PopulateLookupList(ComboBox ddlLookup, LookupTypeReadModel[] lookupTypes, Action onCompletion = null)
            {
                ddlLookup.DisplayMember = "Description";
                ddlLookup.DataSource = lookupTypes;
                ddlLookup.SelectedIndex = 0;
                onCompletion?.Invoke();
            }

        }

        void IControlCommand.Unload()
        {
            _viewModel.StopListener();
        }

        /// <summary>
        /// add futures option contract
        /// </summary>
        public void Add( Action<bool> addAction)
        {
            switch (_editMode)
            {
                case EditMode.View:
                    txtDescription.Enabled = true;
                    dtmContractMonth.Value = DateTime.Now;
                    dtmContractMonth.Enabled = true;
                    txtStrikePrice.Text = string.Empty;
                    txtStrikePrice.Enabled = true;
                    ddlOptionType.SelectedIndex = 0;
                    ddlOptionType.Enabled = true;
                    ddlSecurityType.SelectedIndex = 0;
                    ddlSecurityType.Enabled = true;
                    ddlCurrency.SelectedIndex = 0;
                    ddlCurrency.Enabled = true;
                    ddlExchange.SelectedIndex = 0;
                    ddlExchange.Enabled = true;
                    ddlMultiplier.SelectedIndex = 0;
                    ddlMultiplier.Enabled = true;
                    ddlSymbol.Enabled = false;
                    SetLocalSymbol(dtmContractMonth.Value);
                    txtContractId.Text = string.Empty;
                    txtDescription.Text = string.Empty;
                    _lastContractIndex = lstFuturesOptionContractIds.SelectedIndex;
                    _editMode = EditMode.Add;
                    addAction(false);
                    break;
                case EditMode.Add:
                    var strikePrice = 0;
                    if (int.TryParse(txtStrikePrice.Text, out strikePrice))
                    {
                        var symbol = _viewModel.GetSymbol(ddlSymbol.SelectedIndex).ShortCode;
                        var maturityDate = $"{dtmContractMonth.Value:yyyyMMdd}";
                        var optionType = _viewModel.GetOptionType(ddlOptionType.SelectedIndex).ShortCode.Substring(0, 1);
                        txtContractId.Text = $"{symbol}{maturityDate}{optionType}{strikePrice}";
                        var futuresOptionContract = new FuturesOptionContractReadModel
                        (
                            ContractId: txtContractId.Text,
                            Symbol: symbol,
                            LocalSymbol: txtLocalSymbol.Text,
                            SecurityType: _viewModel.GetSecurityType(ddlSecurityType.SelectedIndex).ShortCode,
                            Currency: _viewModel.GetCurrency(ddlCurrency.SelectedIndex).ShortCode,
                            Exchange: _viewModel.GetExchange(ddlExchange.SelectedIndex).ShortCode,
                            Multiplier: _viewModel.GetMultiplier(ddlMultiplier.SelectedIndex).ShortCode,
                            ContractMonth: dtmContractMonth.Value,
                            OptionType: _viewModel.GetOptionType(ddlOptionType.SelectedIndex).ShortCode,
                            StrikePrice: strikePrice,
                            Description: txtDescription.Text
                        );
                        _viewModel.OnAddAction = addAction;
                        _viewModel.AddFuturesOptionContract(futuresOptionContract);
                    }
                    else
                        MessageBox.Show("Invalid StrikePrice entered", "System Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    break;
            }

        }

        public bool Close(Action<bool> closeAction)
        {
            switch(_editMode)
            {
                case EditMode.Add:
                case EditMode.Change:
                    ShowSelectedFuturesOptionContract(_lastContractIndex);
                    _editMode = EditMode.View;
                    closeAction(lstFuturesOptionContractIds.Items.Count > 0);
                    return false;
            }
            return true;
        }

        public void Change(Action<bool> changeAction)
        {
            switch (_editMode)
            {
                case EditMode.View:
                    txtDescription.Enabled = true;
                    dtmContractMonth.Enabled = true;
                    txtStrikePrice.Enabled = true;
                    ddlOptionType.Enabled = true;
                    ddlSecurityType.Enabled = true;
                    ddlCurrency.Enabled = true;
                    ddlExchange.Enabled = true;
                    ddlMultiplier.Enabled = true;
                    ddlSymbol.Enabled = false;
                    txtLocalSymbol.Enabled = true;
                    _lastContractIndex = lstFuturesOptionContractIds.SelectedIndex;
                    _originalContractId = txtContractId.Text;
                    _editMode = EditMode.Change;
                    changeAction(false);
                    break;
                case EditMode.Change:
                    var strikePrice = 0;
                    if (int.TryParse(txtStrikePrice.Text, out strikePrice))
                    {
                        var symbol = _viewModel.GetSymbol(ddlSymbol.SelectedIndex).ShortCode;
                        var maturityDate = $"{dtmContractMonth.Value:yyyyMMdd}";
                         var optionType = _optionTypeBinding.GetValue().Substring(0, 1);
                        txtContractId.Text = $"{symbol}{maturityDate}{optionType}{strikePrice}";
                        var futuresOptionContract = new FuturesOptionContractReadModel (
                            ContractId: txtContractId.Text,
                            Symbol: symbol,
                            LocalSymbol: txtLocalSymbol.Text,
                            SecurityType: _viewModel.GetSecurityType(ddlSecurityType.SelectedIndex).ShortCode,
                            Currency: _viewModel.GetCurrency(ddlCurrency.SelectedIndex).ShortCode,
                            Exchange: _viewModel.GetExchange(ddlExchange.SelectedIndex).ShortCode,
                            Multiplier: _viewModel.GetMultiplier(ddlMultiplier.SelectedIndex).ShortCode,
                            ContractMonth: dtmContractMonth.Value,
                            OptionType: _viewModel.GetOptionType(ddlOptionType.SelectedIndex).ShortCode,
                            StrikePrice: strikePrice,
                            Description: txtDescription.Text
                        );
                        _viewModel.OnChangeAction = changeAction;
                        _viewModel.ChangeFuturesOptionContract(_originalContractId, futuresOptionContract);
                    }
                    else
                        MessageBox.Show("Invalid StrikePrice entered", "System Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    break;
            }
        }

        public void Remove()
        {
            var contractId = _viewModel.GetFuturesOptionContract(lstFuturesOptionContractIds.SelectedIndex).ContractId;
            if (MessageBox.Show($"Are you sure you want ro remove Futures Option Contract: {contractId} ?", "Remove Futures Option Contract", MessageBoxButtons.YesNo) == DialogResult.Yes)
                _viewModel.RemoveFuturesOptionContract(contractId);
        }

        private void LoadFuturesOptionContractIds(string contractId = null)
        {
            var symbol = _viewModel.GetSymbol(ddlSymbol.SelectedIndex).ShortCode;
            _viewModel.LoadFuturesOptionContracts(symbol, futuresOptionContracts =>
            {
                lstFuturesOptionContractIds.DataSource = futuresOptionContracts.Select(e => e.ContractId).ToList();
                var selectedIndex = 0;
                if (!string.IsNullOrWhiteSpace(contractId) && lstFuturesOptionContractIds.Items.IndexOf(contractId) > -1)
                    selectedIndex = lstFuturesOptionContractIds.Items.IndexOf(contractId);
                lstFuturesOptionContractIds.SelectedIndex = selectedIndex;
            });
        }

        private void ddlSymbol_SelectedIndexChanged(object sender, EventArgs e)
        {
            var symbol = _viewModel.GetSymbol(ddlSymbol.SelectedIndex).ShortCode;
            _viewModel.LoadFuturesOptionContracts(symbol, futuresOptionContracts => 
                this.Post(() => {
                    lstFuturesOptionContractIds.DataSource = futuresOptionContracts.Select(o => o.ContractId).ToList();
                    lstFuturesOptionContractIds.SelectedIndex = 0;
                    _viewModel.OnDataLoaded(futuresOptionContracts.Count > 0);
                    ddlSymbol.Enabled = true;
                    SetDescription();
                    SetContractId();
                }));
        }

        private void lstFuturesOptionContractIds_SelectedIndexChanged(object sender, EventArgs e)
            => ShowSelectedFuturesOptionContract(lstFuturesOptionContractIds.SelectedIndex);

        private void ShowSelectedFuturesOptionContract(int selectedIndex)
        {
            var foc = _viewModel.GetFuturesOptionContract(selectedIndex);
            txtContractId.Text = foc.ContractId;
            txtDescription.Text = foc.Description ?? string.Empty;
            txtDescription.Enabled = false;
            dtmContractMonth.Value = foc.ContractMonth;
            dtmContractMonth.Enabled = false;
            txtStrikePrice.Text = $"{foc.StrikePrice:F0}";
            txtStrikePrice.Enabled = false;
            ddlOptionType.SelectedIndex = _viewModel.GetOptionTypeIndex(foc.OptionType);
            ddlOptionType.Enabled = false;
            txtLocalSymbol.Text = foc.LocalSymbol;
            txtLocalSymbol.Enabled = false;
            ddlSecurityType.SelectedIndex = _viewModel.GetSecurityTypeIndex(foc.SecurityType);
            ddlSecurityType.Enabled = false;
            ddlCurrency.SelectedIndex = _viewModel.GetCurrencyIndex(foc.Currency);
            ddlCurrency.Enabled = false;
            ddlExchange.SelectedIndex = _viewModel.GetExchangeIndex(foc.Exchange);
            ddlExchange.Enabled = false;
            ddlMultiplier.SelectedIndex = _viewModel.GetMultiplierIndex(foc.Multiplier);
            ddlMultiplier.Enabled = false;
            ddlSymbol.Enabled = true;
        }

        private enum EditMode
        {
            View,
            Add,
            Change
        }

        private void dtmContractMonth_ValueChanged(object sender, EventArgs e)
        {
            var valueDate = new DateTime(dtmContractMonth.Value.Year, dtmContractMonth.Value.Month, dtmContractMonth.Value.Day);
            SetLocalSymbol(valueDate);
            SetDescription();
            SetContractId();
        }

        private void SetLocalSymbol(DateTime valueDate) => txtLocalSymbol.Text = FuturesOptionContractReadModel.GetLocalSymbol(ddlSymbol.Text, valueDate);

        private void txtStrikePrice_Leave(object sender, EventArgs e)
        {
            SetDescription();
            SetContractId();
        }

        private void SetDescription()
        {
            if (ddlSymbol.SelectedIndex < 0 || ddlOptionType.SelectedIndex < 0)
                return;
            var asset = _viewModel.GetSymbol(ddlSymbol.SelectedIndex).Description;
            var year = dtmContractMonth.Value.Year;
            var month = $"{dtmContractMonth.Value:MMM}";
            var day = dtmContractMonth.Value.Day;
            var optionType = _viewModel.GetOptionType(ddlOptionType.SelectedIndex).Description;
            var strike = txtStrikePrice.Text;
            var exchange = ddlExchange.Text;
            if (int.TryParse(strike, out var strikePrice))
                txtDescription.Text = $"{asset} {year} {month} {day} {optionType} {strikePrice} @ {exchange}";
        }

        private void SetContractId()
        {
            if (ddlSymbol.SelectedIndex < 0 || ddlOptionType.SelectedIndex < 0)
                return;
            var asset = _viewModel.GetSymbol(ddlSymbol.SelectedIndex).ShortCode;
            var date = $"{dtmContractMonth.Value:yyyyMMdd}";
            var optionType = _viewModel.GetOptionType(ddlOptionType.SelectedIndex).ShortCode.Substring(0, 1);
            var strike = txtStrikePrice.Text;
            var strikePrice = 0;
            if (int.TryParse(strike, out strikePrice))
                txtContractId.Text = $"{asset}{date}{optionType}{strike}";
        }

        private void ddlExchange_SelectedIndexChanged(object sender, EventArgs e)
        {
            SetDescription();
        }

        private void ddlOptionType_SelectedIndexChanged(object sender, EventArgs e)
        {
            SetContractId();
            SetDescription();
        }

        public void Import()
        {
            throw new NotImplementedException();
        }

        public void Open()
        {
            throw new NotImplementedException();
        }

        void IFormControl.Resize(Control parentControl)
        {
            throw new NotImplementedException();
        }

        public void Close()
        {
            throw new NotImplementedException();
        }
    }
    
}
