using System.Data;
using TomasAI.IFM.Contracts;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.Reference.ViewModels;
using TomasAI.IFM.ViewModels.MarketData;

namespace TomasAI.IFM.Views.MarketData;

/// <summary>
/// Represents a user control for managing and editing futures contracts.
/// </summary>
/// <remarks>This control provides functionality to load, add, change, and remove futures contracts. It also
/// supports loading reference data such as currencies, exchanges, and symbols required for futures contract management.
/// The control is designed to be used within a larger application that manages financial instruments.</remarks>
public partial class FuturesContractEditorControl : UserControl, IControlCommand, IFormControl
{
    FuturesContractEditorViewModel _viewModel;
    EditMode _editMode;
    int _lastContractIndex;
    Action<bool>? _dataLoaded;
    Action<bool>? _addAction;
    Action<bool>? _changeAction;
    bool _canChangeRemove;
    Action _refreshAction;

   /// <summary>
   /// Initializes a new instance of the <see cref="FuturesContractEditorControl"/> class with the specified view model
   /// and refresh action.
   /// </summary>
   /// <remarks>The <paramref name="viewModel"/> parameter must not be <c>null</c>. The <paramref
   /// name="refreshAction"/> parameter is expected to encapsulate logic for refreshing the control, such as updating
   /// the UI or reloading data.</remarks>
   /// <param name="viewModel">The view model that provides data and logic for the futures contract editor.</param>
   /// <param name="refreshAction">An action to refresh the control's state or data when invoked.</param>
   public FuturesContractEditorControl(FuturesContractEditorViewModel viewModel, Action refreshAction)
   {
       InitializeComponent();
        _viewModel = viewModel;
        _refreshAction = refreshAction;
   }

    /// <summary>
    /// Gets a value indicating whether the removal operation can be changed.
    /// </summary>
   public bool CanChangeRemove => _canChangeRemove;

    /// <summary>
    /// Gets a value indicating whether the control can import data.
    /// </summary>
    /// <remarks>This control does not support importing data, hence this property always returns <c>false</c>.</remarks>
    /// <value><c>false</c> since this control does not implement import functionality.</value>
    public bool CanImport => false;

    /// <summary>
    /// Initializes and loads the necessary data for the control, including currencies, security types, exchanges,
    /// multipliers, symbols, and futures contracts.
    /// </summary>
    /// <remarks>This method sets up event handlers for various data-loading operations and invokes the
    /// corresponding data-loading methods on the view model. It also handles error reporting and updates the UI
    /// components with the loaded data.</remarks>
    /// <param name="appRoot">The application root object used to access shared application resources.</param>
    /// <param name="dataLoaded">A callback action invoked with a boolean value indicating whether the data was successfully loaded.</param>
    void IControlCommand.Load(IAppRoot appRoot, Action<bool> dataLoaded)
    {
        bool showError = false;
        _editMode = EditMode.View;
        _dataLoaded = dataLoaded;
        _viewModel.OnError = (_, errorMsg) => this.Post(() =>
        {
            if ((!showError))
            {
                Cursor = Cursors.Default;
                showError = true;
                MessageBox.Show(
                    text: errorMsg,
                    caption: "Futures Contract Editor Error",
                    buttons: MessageBoxButtons.OK,
                    icon: MessageBoxIcon.Error);
                showError = false;
            }
        });

        _viewModel.OnCurrenciesLoaded = lookupTypes => this.Post(() => {
            ddlCurrency.Items.Clear();
            if (lookupTypes?.Length == 0)
                return;
            foreach (var lookupType in lookupTypes!)
                ddlCurrency.Items.Add(lookupType.Description);
            ddlCurrency.SelectedIndex = 0;
            LoadAllFuturesContracts();
        });

        _viewModel.OnSecurityTypesLoaded = lookupTypes => this.Post(() => {
            ddlSecurityType.Items.Clear();
            if (lookupTypes?.Length == 0)
                return;
            foreach (var lookupType in lookupTypes!)
                ddlSecurityType.Items.Add(lookupType.Description);
            ddlSecurityType.SelectedIndex = 0;
            LoadAllFuturesContracts();
        });

        _viewModel.OnExchangesLoaded = lookupTypes => this.Post(() => {
            ddlExchange.Items.Clear();
            if (lookupTypes?.Length == 0)
                return;
            foreach (var lookupType in lookupTypes!)
                ddlExchange.Items.Add(lookupType.Description);
            ddlExchange.SelectedIndex = 0;
            LoadAllFuturesContracts();
        });

        _viewModel.OnMultipliersLoaded = lookupTypes => this.Post(() => {
            ddlMultiplier.Items.Clear();
            if (lookupTypes?.Length == 0)
                return;
            foreach (var lookupType in lookupTypes!)
                ddlMultiplier.Items.Add(lookupType.Description);
            ddlMultiplier.SelectedIndex = 0;
            LoadAllFuturesContracts();
        });

        _viewModel.OnSymbolsLoaded = lookupTypes => this.Post(() => {
            ddlSymbol.Items.Clear();
            if (lookupTypes?.Length == 0)
                return;
            foreach (var lookupType in lookupTypes!)
                ddlSymbol.Items.Add(lookupType.Description);
            ddlSymbol.SelectedIndex = 0;
            LoadAllFuturesContracts();
        });

        _viewModel.OnCurrentlyTraded = currentlyTraded => this.Post(() => {
            ddlCurrentlyTraded.Items.Clear();
            if (currentlyTraded?.Length == 0)
                return;
            foreach (var e in currentlyTraded!)
                ddlCurrentlyTraded.Items.Add(e);
            ddlCurrentlyTraded.SelectedIndex = 0;
            LoadAllFuturesContracts();
        });

        _viewModel.OnFuturesContractAdded = () => this.Post(() => {
            _editMode = EditMode.View;
            _addAction?.Invoke(true);
            _refreshAction?.Invoke();
            lstFuturesContractIds.Enabled = true;
        });

        _viewModel.OnFuturesContractChanged = () => this.Post(() => {
            _editMode = EditMode.View;
            _changeAction?.Invoke(true);
            lstFuturesContractIds.Enabled = true;
            lstFuturesContractIds_SelectedIndexChanged(this, EventArgs.Empty);
        });

        _viewModel.OnFuturesContractRemoved = () => this.Post(() => {
            _editMode = EditMode.View;
            _refreshAction?.Invoke();
            lstFuturesContractIds.Enabled = true;
        });

        _viewModel.OnFuturesContractIdsLoaded = () => this.Post(() => {
            _refreshAction?.Invoke();
        });

        _viewModel.OnFuturesContractsLoaded = (futuresContracts) => this.Post(() => {
            _canChangeRemove = false;
            LoadFuturesContractIds(default!, futuresContracts);
            _canChangeRemove = true;
            _dataLoaded?.Invoke(futuresContracts.Length > 0);
            ddlSymbol.Enabled = false;
            SetDescription();
            SetContractId();
        });

        _viewModel.OnWaitCursor = () => this.Post(() =>
        {
            Cursor = Cursors.WaitCursor;
        });

        _viewModel.OnDefaultCursor = () => this.Post(() =>
        {
            Cursor = Cursors.Default;
        });

        _viewModel.LoadContractMonths();
        _viewModel.LoadCurrentlyTraded();
        _viewModel.LoadSecurityTypes();
        _viewModel.LoadCurrencies();
        _viewModel.LoadExchanges();
        _viewModel.LoadMultipliers();
        _viewModel.LoadSymbols();
    }
    
    /// <summary>
    /// unload futures contract editor
    /// </summary>
    void IControlCommand.Unload()
    {
    }

    /// <summary>
    /// Configures the UI and adds a new futures contract based on the current edit mode.
    /// </summary>
    /// <remarks>This method behaves differently depending on the current edit mode: <list type="bullet">
    /// <item> <description> In <see cref="EditMode.View"/>, the method prepares the UI for adding a new futures
    /// contract  by enabling relevant controls and resetting fields. The <paramref name="addAction"/> delegate  is
    /// invoked with <see langword="false"/>. </description> </item> <item> <description> In <see cref="EditMode.Add"/>,
    /// the method creates a new futures contract using the current  UI values and adds it to the underlying data model.
    /// </description> </item> </list></remarks>
    /// <param name="addAction">An <see cref="Action{T}"/> delegate that is invoked with a <see langword="false"/> value  when the method
    /// transitions to Add mode. The delegate can be used to perform additional  actions during the add operation.</param>
    public void Add(Action<bool> addAction)
    {
        _addAction = addAction;
        switch (_editMode)
        {
            case EditMode.View:
                txtDescription.Enabled = true;
                dtmLastTradeDate.Value = DateTime.Now;
                dtmLastTradeDate.Enabled = true;
                txtLocalSymbol.Enabled = false;
                ddlSecurityType.SelectedIndex = GetSelectedIndex(_viewModel.SecurityTypes, $"{Shared.MarketData.SecurityType.FUT}");
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
                SetLocalSymbol(DateOnly.FromDateTime(dtmLastTradeDate.Value));
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
                var futuresContract = new FuturesContractV2ReadModel
                (
                    contractId: txtContractId.Text,
                    description: txtDescription.Text,
                    symbol: symbol,
                    securityType: _viewModel.GetSecurityType(ddlSecurityType.SelectedIndex),
                    lastTradeDate: DateOnly.FromDateTime(dtmLastTradeDate.Value),
                    multiplier: _viewModel.GetMultiplier(ddlMultiplier.SelectedIndex),
                    exchange: _viewModel.GetExchange(ddlExchange.SelectedIndex),
                    currency: _viewModel.GetCurrency(ddlCurrency.SelectedIndex),
                    localSymbol: txtLocalSymbol.Text,
                    currentlyTraded: ddlCurrentlyTraded.SelectedIndex == 0
                );
                _viewModel.AddFuturesContract(futuresContract, true);
                break;
        }
    }

    /// <summary>
    /// Attempts to close the current operation and transitions to the view mode if applicable.
    /// </summary>
    /// <param name="closeAction">An optional callback that is invoked with a value indicating whether there are any items  in the futures
    /// contract list. The value is <see langword="true"/> if the list contains  items; otherwise, <see
    /// langword="false"/>.</param>
    /// <returns><see langword="true"/> if the operation was successfully closed without requiring a  transition; otherwise, <see
    /// langword="false"/> if the operation transitioned to view mode.</returns>
    public bool Close(Action<bool> closeAction)
    {
        switch (_editMode)
        {
            case EditMode.Add:
            case EditMode.Change:
                ShowSelectedFuturesContract(_lastContractIndex);
                _editMode = EditMode.View;
                closeAction?.Invoke(lstFuturesContractIds.Items.Count > 0);
                lstFuturesContractIds.Enabled = true;
                return false;
        }
        return true;
    }

    /// <summary>
    /// Toggles the edit mode of the form and applies the specified action during the transition.
    /// </summary>
    /// <remarks>This method switches between "View" and "Change" modes. In "View" mode, certain controls  are
    /// disabled, and the form transitions to "Change" mode. In "Change" mode, the method  updates the futures contract
    /// details and saves the changes.</remarks>
    /// <param name="changeAction">An <see cref="Action{T}"/> to be executed during the transition. The action receives a  <see langword="false"/>
    /// when entering edit mode and is not invoked when saving changes.</param>
    public void Change(Action<bool> changeAction)
    {
        _changeAction = changeAction;
        switch (_editMode)
        {
            case EditMode.View:
                txtDescription.Enabled = false;
                dtmLastTradeDate.Enabled = false;
                ddlSecurityType.Enabled = true;
                ddlCurrency.Enabled = true;
                ddlExchange.Enabled = true;
                ddlMultiplier.Enabled = true;
                ddlCurrentlyTraded.Enabled = true;
                ddlSymbol.Enabled = true;
                _lastContractIndex = lstFuturesContractIds.SelectedIndex;
                _editMode = EditMode.Change;
                lstFuturesContractIds.Enabled = false;
                changeAction?.Invoke(false);
                break;
            case EditMode.Change:
                var symbol = _viewModel.GetSymbol(ddlSymbol.SelectedIndex);
                var futuresContractId = _viewModel.GetFuturesContract(lstFuturesContractIds.SelectedIndex).Id;
                var maturityDate = $"{dtmLastTradeDate.Value:yyyyMMdd}";
                txtContractId.Text = $"{symbol}{maturityDate}";
                var futuresContract = new FuturesContractV2ReadModel
                (
                    contractId: txtContractId.Text,
                    description: txtDescription.Text,
                    symbol: symbol,
                    securityType: _viewModel.GetSecurityType(ddlSecurityType.SelectedIndex),
                    lastTradeDate: DateOnly.FromDateTime(dtmLastTradeDate.Value),
                    multiplier: _viewModel.GetMultiplier(ddlMultiplier.SelectedIndex),
                    exchange: _viewModel.GetExchange(ddlExchange.SelectedIndex),
                    currency: _viewModel.GetCurrency(ddlCurrency.SelectedIndex),
                    localSymbol: txtLocalSymbol.Text,
                    currentlyTraded: ddlCurrentlyTraded.SelectedIndex == 0
                );
                _viewModel.ChangeFuturesContract(futuresContractId, futuresContract, true, changeAction);
                break;
        }
    }

    /// <summary>
    /// Removes the selected futures contract after user confirmation.
    /// </summary>
    /// <remarks>This method retrieves the currently selected futures contract and prompts the user for
    /// confirmation  before removing it. If the user confirms, the contract is removed from the underlying data
    /// source.</remarks>
    public void Remove()
    {
        var contract = _viewModel.GetFuturesContract(lstFuturesContractIds.SelectedIndex);
        var contractId = contract?.ContractId;
        if (!string.IsNullOrWhiteSpace(contract?.ContractId))
            if (MessageBox.Show($"Are you sure you want to remove Futures Contract: {contractId} ?", "Remove Futures Contract", MessageBoxButtons.YesNo) == DialogResult.Yes)
                _viewModel.RemoveFuturesContract(contract.Id, true);
    }

    public void Import()
    {
        throw new NotImplementedException();
    }

    public void Open()
    {
        throw new NotImplementedException();
    }

    public void Close()
    {
        throw new NotImplementedException();
    }


    /// <summary>
    /// load all futures contracts once all reference dependencies have been loaded
    /// </summary>
    void LoadAllFuturesContracts()
    {
        if (ddlCurrency.Items.Count > 0 &&
            ddlSecurityType.Items.Count > 0 &&
            ddlExchange.Items.Count > 0 &&
            ddlMultiplier.Items.Count > 0 &&
            ddlSymbol.Items.Count > 0 &&
            ddlCurrentlyTraded.Items.Count > 0)
        {
            _viewModel.LoadFuturesContracts();
        }
    }

    /// <summary>
    /// Populates a list control with futures contract IDs and selects the specified contract ID if it exists.
    /// </summary>
    /// <remarks>If the specified <paramref name="contractId"/> is not found in the provided <paramref
    /// name="futuresContracts"/>, the first item in the list will be selected by default.</remarks>
    /// <param name="contractId">The contract ID to select in the list. If null or whitespace, the first item will be selected.</param>
    /// <param name="futuresContracts">An array of futures contracts to populate the list. If null or empty, the list will remain empty.</param>
    void LoadFuturesContractIds(string contractId, FuturesContractV2ReadModel[] futuresContracts)
    {
        lstFuturesContractIds.Items.Clear();
        if (futuresContracts is null || futuresContracts.Length  == 0)
            return;
        foreach (var fc in futuresContracts!)
            lstFuturesContractIds.Items.Add(fc.ContractId);
        var selectedIndex = 0;
        if (!string.IsNullOrEmpty(contractId))
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

    /// <summary>
    /// return index value for lookup type short code
    /// </summary>
    /// <param name="lookupTypes"></param>
    /// <param name="shortCode"></param>
    /// <returns></returns>
   static  int GetSelectedIndex(ICollection<LookupTypeReadModel> lookupTypes, string shortCode)
        => lookupTypes
            .Where(e => e.ShortCode.Equals(shortCode, StringComparison.CurrentCultureIgnoreCase))
            .Select(e => e.OrderId)
            .FirstOrDefault();

    /// <summary>
    /// show futures contract details
    /// </summary>
    /// <param name="selectedIndex"></param>
    void ShowSelectedFuturesContract(int selectedIndex)
    {
        txtContractId.Enabled = false;
        txtContractId.BackColor = Color.Black;
        txtDescription.Enabled = false;
        txtDescription.BackColor = Color.Black;
        txtLocalSymbol.Enabled = false;
        txtLocalSymbol.BackColor = Color.Black;
        var fc = _viewModel.GetFuturesContract(selectedIndex);
        if (fc is null) 
            return;
        dtmLastTradeDate.Value = fc.LastTradeDate.ToDateTime(TimeOnly.MinValue);
        dtmLastTradeDate.Enabled = false;
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
        SetLocalSymbol(DateOnly.FromDateTime(dtmLastTradeDate.Value));
        SetDescription();
        SetContractId();
    }

    void SetLocalSymbol(DateOnly valueDate)
    {
        if (ddlSymbol.SelectedIndex < 0)
            return;
        var symbol = _viewModel.GetSymbol(ddlSymbol.SelectedIndex);
        var assetSymbol = string.IsNullOrEmpty(symbol) ? "??" : symbol[..2];
        var monthSymbol = _viewModel.GetContractMonth(valueDate.Month);
        var yearSymbol = $"{valueDate.Year}".Substring(3, 1);
        txtLocalSymbol.Text = $"{assetSymbol}{monthSymbol}{yearSymbol}";
    }

    void SetDescription()
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

    void SetContractId()
    {
        if (ddlSymbol.SelectedIndex < 0)
            return;
        var asset = _viewModel.GetSymbol(ddlSymbol.SelectedIndex);
        var date = $"{dtmLastTradeDate.Value:yyyyMMdd}";
        txtContractId.Text = $"{asset}{date}";
    }

    enum EditMode
    {
        View,
        Add,
        Change
    }

    /// <summary>
    /// set local symbol when selected symbol changes
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    void ddlSymbol_SelectedIndexChanged(object sender, EventArgs e)
        => SetLocalSymbol(DateOnly.FromDateTime(dtmLastTradeDate.Value));

    void dtmContractMonth_ValueChanged(object sender, EventArgs e)
    {
        SetLocalSymbol(DateOnly.FromDateTime(dtmLastTradeDate.Value));
        SetDescription();
        SetContractId();
    }

    void ddlExchange_SelectedIndexChanged(object sender, EventArgs e) => SetDescription();
    
    void lstFuturesContractIds_SelectedIndexChanged(object sender, EventArgs e)
    {
        ShowSelectedFuturesContract(lstFuturesContractIds.SelectedIndex);
    }

    void IFormControl.Resize(Control parentControl)
    {
        throw new NotImplementedException();
    }
    
}

