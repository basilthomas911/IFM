using System;
using System.Collections.Generic;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Windows.Forms;
using TomasAI.IFM.Contracts;
using TomasAI.IFM.Shared.Reference.ViewModels;
using TomasAI.IFM.Shared.MarketData;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.ViewModels.MarketData;
using TomasAI.IFM.Views.SystemInfo;

namespace TomasAI.IFM.Views.MarketData
{
    /// <summary>
    /// futures contract editor
    /// </summary>
    public partial class FuturesContractEditorControl : UserControl, IControlCommand, IFormControl
    {
        FuturesContractEditorViewModel _viewModel;
        EditMode _editMode;
        int _lastContractIndex;
        string _originalContractId;
        Action<bool> _dataLoaded;
        Action<bool> _addAction;
        Action<bool> _changeAction;
        bool _canChangeRemove;
        Action _refreshAction;
   
        /// <summary>
        /// futures contract editor constructor
        /// </summary>
        /// <param name="viewModel"></param>
        /// <param name="refreshAction"></param>
        public FuturesContractEditorControl(FuturesContractEditorViewModel viewModel, Action refreshAction)
        {
            _viewModel = viewModel;
            _refreshAction = refreshAction;
            InitializeComponent();
        }

        public bool CanChangeRemove => _canChangeRemove;

        public bool CanImport => false;

        /// <summary>
        /// load reference data
        /// </summary>
        /// <param name="appRoot"></param>
        /// <param name="dataLoaded"></param>
        void IControlCommand.Load(IAppRoot appRoot, Action<bool> dataLoaded)
        {
            _editMode = EditMode.View;
            _dataLoaded = dataLoaded;
            _viewModel.StartListener();
            _viewModel.OnError = (_, errorMsg) => this.Post(() =>
                MessageBox.Show(
                    text: errorMsg,
                    caption: "Reference Data Editor Error",
                    buttons: MessageBoxButtons.OK,
                    icon: MessageBoxIcon.Error));

            _viewModel.OnCurrenciesLoaded = lookupTypes => this.Post(() => {
                ddlCurrency.Items.Clear();
                if ((lookupTypes?.Length ?? 0) == 0) return;
                foreach (var lookupType in lookupTypes)
                    ddlCurrency.Items.Add(lookupType.Description);
                ddlCurrency.SelectedIndex = 0;
                LoadAllFuturesContracts();
            });

            _viewModel.OnSecurityTypesLoaded = lookupTypes => this.Post(() => {
                ddlSecurityType.Items.Clear();
                if ((lookupTypes?.Length ?? 0) == 0) return;
                foreach (var lookupType in lookupTypes)
                    ddlSecurityType.Items.Add(lookupType.Description);
                ddlSecurityType.SelectedIndex = 0;
                LoadAllFuturesContracts();
            });

            _viewModel.OnExchangesLoaded = lookupTypes => this.Post(() => {
                ddlExchange.Items.Clear();
                if ((lookupTypes?.Length ?? 0) == 0) return;
                foreach (var lookupType in lookupTypes)
                    ddlExchange.Items.Add(lookupType.Description);
                ddlExchange.SelectedIndex = 0;
                LoadAllFuturesContracts();
            });

            _viewModel.OnMultipliersLoaded = lookupTypes => this.Post(() => {
                ddlMultiplier.Items.Clear();
                if ((lookupTypes?.Length ?? 0) == 0) return;
                foreach (var lookupType in lookupTypes)
                    ddlMultiplier.Items.Add(lookupType.Description);
                ddlMultiplier.SelectedIndex = 0;
                LoadAllFuturesContracts();
            });

            _viewModel.OnSymbolsLoaded = lookupTypes => this.Post(() => {
                ddlSymbol.Items.Clear();
                if ((lookupTypes?.Length ?? 0) == 0) return;
                foreach (var lookupType in lookupTypes)
                    ddlSymbol.Items.Add(lookupType.Description);
                ddlSymbol.SelectedIndex = 0;
                LoadAllFuturesContracts();
            });

            _viewModel.OnCurrentlyTraded = currentlyTraded => this.Post(() => {
                ddlCurrentlyTraded.Items.Clear();
                foreach (var e in currentlyTraded)
                    ddlCurrentlyTraded.Items.Add(e);
                ddlCurrentlyTraded.SelectedIndex = 0;
                LoadAllFuturesContracts();
            });

            _viewModel.OnFuturesContractAdded = () => this.Post(() => {
                _editMode = EditMode.View;
                _addAction(true);
                _refreshAction?.Invoke();
            });

            _viewModel.OnFuturesContractChanged = () => this.Post(() => {
                _editMode = EditMode.View;
                _changeAction(true);
                lstFuturesContractIds_SelectedIndexChanged(this, EventArgs.Empty);
            });

            _viewModel.OnFuturesContractRemoved = () => this.Post(() => {
                _editMode = EditMode.View;
                _refreshAction?.Invoke();
            });

            _viewModel.OnFuturesContractIdsLoaded = () => this.Post(() => {
                _refreshAction?.Invoke();
            });

            _viewModel.OnFuturesContractsLoaded = (futuresContracts) => this.Post(() => {
                _canChangeRemove = false;
                UpdateFuturesContractIds(null, futuresContracts);
                _canChangeRemove = true;
                _dataLoaded(futuresContracts.Length > 0);
                ddlSymbol.Enabled = false;
                SetDescription();
                SetContractId();
            });
            _viewModel.LoadContractMonths();
            _viewModel.LoadCurrentlyTraded();
            _viewModel.LoadSecurityTypes();
            _viewModel.LoadCurrencies();
            _viewModel.LoadExchanges();
            _viewModel.LoadMultipliers();
            _viewModel.LoadSymbols();
            return;

            void UpdateFuturesContractIds(string contractId, FuturesContractViewModel[] futuresContracts)
            {
                lstFuturesContractIds.Items.Clear();
                if ((futuresContracts?.Length ?? 0) == 0) return;
                foreach (var fc in futuresContracts)
                    lstFuturesContractIds.Items.Add(fc.ContractId);
                var selectedIndex = 0;
                if (!string.IsNullOrWhiteSpace(contractId))
                {
                    for (var index = 0; index < futuresContracts.Length; index++)
                        if (futuresContracts[index].ContractId == contractId)
                        {
                            selectedIndex = index;
                            break;
                        }
                }
                lstFuturesContractIds.SelectedIndex = selectedIndex;
            }
        }

        /// <summary>
        /// unload futures contract editor
        /// </summary>
        void IControlCommand.Unload()
        {
            _viewModel.StopListener();
        }

        /// <summary>
        /// add futures contract
        /// </summary>
        public void Add( Action<bool> addAction)
        {
            _addAction = addAction;
            switch (_editMode)
            {
                case EditMode.View:
                    txtDescription.Enabled = true;
                    dtmLastTradeDate.Value = DateTime.Now;
                    dtmLastTradeDate.Enabled = true;
                    txtLocalSymbol.Enabled = false;
                    ddlSecurityType.SelectedIndex = GetSelectedIndex(_viewModel.SecurityTypes, $"{SecurityType.FUT}");
                    ddlSecurityType.Enabled = true;
                    ddlCurrency.SelectedIndex = 0;
                    ddlCurrency.Enabled = true;
                    ddlExchange.SelectedIndex = 0;
                    ddlExchange.Enabled = true;
                    ddlMultiplier.SelectedIndex = 0;
                    ddlMultiplier.Enabled = true;
                    ddlCurrentlyTraded.SelectedIndex = 0;
                    ddlCurrentlyTraded.Enabled = true;
                    ddlSymbol.SelectedIndex = 0;
                    ddlSymbol.Enabled = true;
                    SetLocalSymbol(dtmLastTradeDate.Value);
                    txtContractId.Text = string.Empty;
                    txtDescription.Text = string.Empty;
                    _lastContractIndex = lstFuturesContractIds.SelectedIndex;
                    _editMode = EditMode.Add;
                    addAction(false);
                    break;
                case EditMode.Add:
                    var symbol = _viewModel.GetSymbol(ddlSymbol.SelectedIndex);
                    var maturityDate = $"{dtmLastTradeDate.Value:yyyyMMdd}";
                    txtContractId.Text = $"{symbol}{maturityDate}";
                    var futuresContract = new FuturesContractViewModel
                    (
                        ContractId: txtContractId.Text,
                        Description: txtDescription.Text,
                        Symbol: symbol,
                        SecurityType: _viewModel.GetSecurityType(ddlSecurityType.SelectedIndex),
                        LastTradeDate: dtmLastTradeDate.Value,
                        Multiplier: _viewModel.GetMultiplier(ddlMultiplier.SelectedIndex),
                        Exchange: _viewModel.GetExchange(ddlExchange.SelectedIndex),
                        Currency: _viewModel.GetCurrency(ddlCurrency.SelectedIndex),
                        LocalSymbol: txtLocalSymbol.Text,
                        CurrentlyTraded: ddlCurrentlyTraded.SelectedIndex == 0
                    );
                    _viewModel.AddFuturesContract(futuresContract);
                    break;
            }
        }

        /// <summary>
        /// close/cancel editor
        /// </summary>
        /// <param name="closeAction"></param>
        /// <returns></returns>
        public bool Close(Action<bool> closeAction)
        {
            switch(_editMode)
            {
                case EditMode.Add:
                case EditMode.Change:
                    ShowSelectedFuturesContract(_lastContractIndex);
                    _editMode = EditMode.View;
                    closeAction?.Invoke(lstFuturesContractIds.Items.Count > 0);
                    return false;
            }
            return true;
        }

        /// <summary>
        /// change futures contract
        /// </summary>
        /// <param name="changeAction"></param>
        public void Change(Action<bool> changeAction)
        {
            _changeAction = changeAction;
            switch (_editMode)
            {
                case EditMode.View:
                    txtDescription.Enabled = false;
                    dtmLastTradeDate.Enabled = false;
                    txtLocalSymbol.Enabled = true;
                    ddlSecurityType.Enabled = true;
                    ddlCurrency.Enabled = true;
                    ddlExchange.Enabled = true;
                    ddlMultiplier.Enabled = true;
                    ddlCurrentlyTraded.Enabled = true;
                    ddlSymbol.Enabled = true;
                    _lastContractIndex = lstFuturesContractIds.SelectedIndex;
                    _originalContractId = txtContractId.Text;
                    _editMode = EditMode.Change;
                    changeAction?.Invoke(false);
                    break;
                case EditMode.Change:
                    var symbol = _viewModel.GetSymbol(ddlSymbol.SelectedIndex);
                    var originalContractId = _viewModel.GetFuturesContract(lstFuturesContractIds.SelectedIndex).ContractId;
                    var maturityDate = $"{dtmLastTradeDate.Value:yyyyMMdd}";
                    txtContractId.Text = $"{symbol}{maturityDate}";
                    var futuresContract = new FuturesContractViewModel
                    (
                        ContractId: txtContractId.Text,
                        Description: txtDescription.Text,
                        Symbol: symbol,
                        SecurityType: _viewModel.GetSecurityType(ddlSecurityType.SelectedIndex),
                        LastTradeDate: dtmLastTradeDate.Value,
                        Multiplier: _viewModel.GetMultiplier(ddlMultiplier.SelectedIndex),
                        Exchange: _viewModel.GetExchange(ddlExchange.SelectedIndex),
                        Currency: _viewModel.GetCurrency(ddlCurrency.SelectedIndex),
                        LocalSymbol: txtLocalSymbol.Text,
                        CurrentlyTraded: ddlCurrentlyTraded.SelectedIndex == 0
                    );
                    this.Cursor = Cursors.WaitCursor;
                    _viewModel.ChangeFuturesContract(originalContractId, futuresContract);
                    this.Cursor = Cursors.Default;
                    break;
            }

        }

        /// <summary>
        /// remove selected futures contract
        /// </summary>
        public void Remove()
        {
            var contractId = _viewModel.GetFuturesContract(lstFuturesContractIds.SelectedIndex)?.ContractId;
            if (!string.IsNullOrWhiteSpace(contractId))
                if (MessageBox.Show($"Are you sure you want to remove Futures Contract: {contractId} ?", "Remove Futures Contract", MessageBoxButtons.YesNo) == DialogResult.Yes)
                    _viewModel.RemoveFuturesContract(contractId);
        }

        /// <summary>
        /// load all futures contracts once all reference dependencies have been loaded
        /// </summary>
        private void LoadAllFuturesContracts()
        {
            if (!_viewModel.AllLookupTypesLoaded()) return;
            _viewModel.LoadFuturesContracts();
        }

        /// <summary>
        /// return index value for lookup type short code
        /// </summary>
        /// <param name="lookupTypes"></param>
        /// <param name="shortCode"></param>
        /// <returns></returns>
        private int GetSelectedIndex(ICollection<LookupTypeReadModel> lookupTypes, string shortCode)
            => lookupTypes
            .Where(e => e.ShortCode == shortCode.ToUpper())
            .Select(e => e.OrderId)
            .Single();

        /// <summary>
        /// set local symbol when selected symbol changes
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ddlSymbol_SelectedIndexChanged(object sender, EventArgs e) => SetLocalSymbol(dtmLastTradeDate.Value);

        /// <summary>
        /// show futures contract details
        /// </summary>
        /// <param name="selectedIndex"></param>
        private void ShowSelectedFuturesContract(int selectedIndex)
        {
            var fc = _viewModel.GetFuturesContract(selectedIndex);
            if (fc is null) return;
            txtDescription.Enabled = false;
            dtmLastTradeDate.Value = fc.LastTradeDate;
            dtmLastTradeDate.Enabled = false;
            txtLocalSymbol.Enabled = false;
            ddlSecurityType.SelectedIndex = GetSelectedIndex(_viewModel.SecurityTypes, fc.SecurityType);
            ddlSecurityType.Enabled = false;
            ddlCurrency.SelectedIndex = GetSelectedIndex(_viewModel.Currencies, fc.Currency);
            ddlCurrency.Enabled = false;
            ddlExchange.SelectedIndex = GetSelectedIndex(_viewModel.Exchanges, fc.Exchange);
            ddlExchange.Enabled = false;
            ddlMultiplier.SelectedIndex = GetSelectedIndex(_viewModel.Multipliers, fc.Multiplier);
            ddlMultiplier.Enabled = false;
            ddlCurrentlyTraded.SelectedIndex = fc.CurrentlyTraded ? 0 : 1;
            ddlCurrentlyTraded.Enabled = false;
            ddlSymbol.SelectedIndex = GetSelectedIndex(_viewModel.Symbols, fc.Symbol);
            ddlSymbol.Enabled = false;
            SetLocalSymbol(dtmLastTradeDate.Value);
            SetDescription();
            SetContractId();
        }

        private enum EditMode
        {
            View,
            Add,
            Change
        }

        private void SetLocalSymbol(DateTime valueDate)
        {
            var symbol = _viewModel.GetSymbol(ddlSymbol.SelectedIndex);
            var assetSymbol = symbol.Substring(0, 2);
            var monthSymbol = _viewModel.GetContractMonth(valueDate.Month);
            var yearSymbol = $"{valueDate.Year}".Substring(3, 1);
            txtLocalSymbol.Text = $"{assetSymbol}{monthSymbol}{yearSymbol}";
        }
      
        private void SetDescription()
        {
            if (ddlSymbol.SelectedIndex < 0)
                return;
            var asset = _viewModel.GetSymbolDescription(ddlSymbol.SelectedIndex);
            var year = dtmLastTradeDate.Value.Year;
            var month = $"{dtmLastTradeDate.Value:MMM}";
            var day = dtmLastTradeDate.Value.Day;
            var exchange = ddlExchange.Text;
            txtDescription.Text = $"{asset} {year} {month} {day} @ {exchange}";
        }

        private void SetContractId()
        {
            if (ddlSymbol.SelectedIndex < 0)
                return;
            var asset = _viewModel.GetSymbol(ddlSymbol.SelectedIndex);
            var date = $"{dtmLastTradeDate.Value:yyyyMMdd}";
            txtContractId.Text = $"{asset}{date}";
        }

        private void dtmContractMonth_ValueChanged(object sender, EventArgs e)
        {
            SetLocalSymbol(dtmLastTradeDate.Value);
            SetDescription();
            SetContractId();
        }

        private void ddlExchange_SelectedIndexChanged(object sender, EventArgs e) => SetDescription();

        public void Import()
        {
            throw new NotImplementedException();
        }

        private void lstFuturesContractIds_SelectedIndexChanged(object sender, EventArgs e)
        {
            ShowSelectedFuturesContract(lstFuturesContractIds.SelectedIndex);
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
