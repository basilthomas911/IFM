using System.Data;
using TomasAI.IFM.Contracts;
using TomasAI.IFM.Shared.Reference.ViewModels;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.ViewModels.MarketData;

namespace TomasAI.IFM.Views.MarketData;

/// <summary>
/// Represents a user control for managing futures option contracts, including adding, editing,  and removing contracts.
/// This control provides a user interface for interacting with  <see cref="FuturesOptionContractEditorViewModel"/> to
/// perform operations on futures option contracts.
/// </summary>
/// <remarks>This control is designed to handle futures option contract data, including loading contract details, 
/// managing lookup types (e.g., option types, security types, currencies, exchanges, multipliers),  and providing user
/// interaction for adding, changing, and removing contracts.  It implements <see cref="IControlCommand"/> and <see
/// cref="IFormControl"/> to integrate with  application workflows and lifecycle management.  The control uses a view
/// model (<see cref="FuturesOptionContractEditorViewModel"/>) to handle  data operations and provides feedback to the
/// user through UI elements such as combo boxes,  text fields, and lists.</remarks>
public partial class FuturesOptionContractEditorControl 
    : UserControl, IControlCommand, IFormControl
{
    readonly FuturesOptionContractEditorViewModel _viewModel;
    readonly MarketDataViewModel _mktDataViewModel;
    EditMode _editMode;
    int _lastContractIndex;
    string? _originalContractId;
    ListBinding<ICollection<LookupTypeReadModel>>? _optionTypeBinding;


    /// <summary>
    /// Initializes a new instance of the <see cref="FuturesOptionContractEditorControl"/> class.
    /// </summary>
    /// <param name="viewModel">The view model that provides data and logic for the futures option contract editor.</param>
    public FuturesOptionContractEditorControl(FuturesOptionContractEditorViewModel viewModel, MarketDataViewModel mktDataViewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _mktDataViewModel = mktDataViewModel;
        _editMode = EditMode.View;
    }

    /// <summary>
    /// Gets a value indicating whether the "Remove" action can be performed.
    /// </summary>
    public bool CanChangeRemove 
        => lstFuturesOptionContractIds.Items.Count > 0;

    /// <summary>
    /// Gets a value indicating whether the current instance supports importing data.
    /// </summary>
    public bool CanImport 
        => false;

    /// <summary>
    /// Initializes and loads the necessary data and configurations for the control command.
    /// </summary>
    /// <remarks>This method sets up the control in view mode, initializes event handlers for various
    /// operations (e.g., adding, changing, or removing futures option contracts), and loads lookup data such as option
    /// types, security types, currencies, exchanges, and multipliers. It also manages UI updates such as wait
    /// indicators and error messages.</remarks>
    /// <param name="appRoot">The application root object that provides access to application-level services and resources.</param>
    /// <param name="dataLoaded">A callback action that is invoked with a <see langword="true"/> value when the data has been successfully
    /// loaded.</param>
    void IControlCommand.Load(IAppRoot appRoot, Action<bool> dataLoaded)
    {
        bool showError = false;
        _editMode = EditMode.View;
        _viewModel.StartListener();
        _viewModel.OnDataLoaded = dataLoaded;
        _viewModel.OnError = (_, errorMsg) => this.Post(() =>
        {
            if (!showError)
            {
                showError = true;
                MessageBox.Show(
                        text: errorMsg,
                        caption: "Futures Option Contract Editor Error",
                        buttons: MessageBoxButtons.OK,
                        icon: MessageBoxIcon.Error);
                showError = false;
            }
        });
        
        _viewModel.OnWaitCursor = () => this.Post(() =>
        {
            this.Cursor = Cursors.WaitCursor;
            _mktDataViewModel.OnDisableAllButtons?.Invoke();
            _mktDataViewModel.OnEnableMarketSelector?.Invoke(false);
            lstFuturesOptionContractIds.Enabled = false;
            tlpFuturesOptionContract.Enabled = false;
        });
        _viewModel.OnDefaultCursor = () => this.Post(() => {
            this.Cursor = Cursors.Default;
            _mktDataViewModel.OnEnableMarketSelector?.Invoke(true);
            lstFuturesOptionContractIds.Enabled = true;
            tlpFuturesOptionContract.Enabled = true;
        });
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

        _viewModel.OnFuturesOptionContractLoaded = () => this.Post(() =>
        {
            _editMode = EditMode.View;
            LoadFuturesOptionContractIds();
            dataLoaded?.Invoke(lstFuturesOptionContractIds.Items.Count > 0);
        });

        _viewModel.LoadSecurityTypes(securityTypes => this.Post(() => DisplaySecurityTypes(securityTypes)));
        _viewModel.LoadCurrencies(currencyTypes => this.Post(() => DisplayCurrencyTypes(currencyTypes)));
        _viewModel.LoadExchanges(exchangeTypes => this.Post(() => DisplayExchangeTypes(exchangeTypes)));
        _viewModel.LoadMultipliers(multiplierTypes => this.Post(() => DisplayMultiplierTypes(multiplierTypes)));
        _viewModel.LoadOptionTypes(optionTypes => this.Post(() =>
        {
            _optionTypeBinding = new ListBinding<ICollection<LookupTypeReadModel>>(
            "Description",
            ddlOptionType,
            optionTypes,
            selectedIndex => _viewModel.GetOptionType(selectedIndex).ShortCode).Load(LoadSymbols);
        }));
        return;

        /// Display the initial state of the control in view mode
        void DisplaySecurityTypes(LookupTypeReadModel[] securityTypes)
            => PopulateLookupList(ddlSecurityType, securityTypes, LoadSymbols);

        /// Display the currency types in the dropdown list and load symbols
        void DisplayCurrencyTypes(LookupTypeReadModel[] currencyTypes)
            => PopulateLookupList(ddlCurrency, currencyTypes, LoadSymbols);

        /// Display the exchange types in the dropdown list and load symbols    
        void DisplayExchangeTypes(LookupTypeReadModel[] exchangeTypes)
            => PopulateLookupList(ddlExchange, exchangeTypes, LoadSymbols);

        /// Display the multiplier types in the dropdown list and load symbols
        void DisplayMultiplierTypes(LookupTypeReadModel[] multiplierTypes)
            => PopulateLookupList(ddlMultiplier, multiplierTypes, LoadSymbols);

        /// Load symbols and populate the symbol dropdown list
        void LoadSymbols()
        {
            if (!AllLookupTypesLoaded()) 
                return;
            _viewModel.LoadSymbols(symbolTypes => this.Post(() => PopulateLookupList(ddlSymbol, symbolTypes)));
        }

        /// Check if all lookup types are loaded and available in the dropdown lists
        bool AllLookupTypesLoaded()
           => ddlOptionType.Items.Count > 0 &&
               ddlSecurityType.Items.Count > 0 &&
               ddlCurrency.Items.Count > 0 &&
               ddlExchange.Items.Count > 0 &&
               ddlMultiplier.Items.Count > 0;

        /// Populate the specified dropdown list with the provided lookup types and invoke an optional completion action.
        void PopulateLookupList(ComboBox ddlLookup, LookupTypeReadModel[] lookupTypes, Action onCompletion = null!)
        {
            if (lookupTypes is not null && lookupTypes.Length > 0)
            {
                ddlLookup.DisplayMember = "Description";
                ddlLookup.DataSource = lookupTypes;
                ddlLookup.SelectedIndex = 0;
                onCompletion?.Invoke();
            }
        }

    }

    /// <summary>
    /// Unloads the control and stops any active listeners associated with it.
    /// </summary>
    /// <remarks>This method is typically used to release resources or stop background operations  when the
    /// control is no longer needed. Ensure that any dependent operations are  completed before calling this
    /// method.</remarks>
    void IControlCommand.Unload()
    {
        _viewModel.StopListener();
    }

    /// <summary>
    /// Adds a new futures option contract or prepares the form for adding a new contract, depending on the current edit
    /// mode.
    /// </summary>
    /// <remarks>In "View" mode, this method prepares the form for adding a new contract by enabling input
    /// fields and resetting their values. In "Add" mode, it validates the input, generates a contract ID, and adds the
    /// contract to the system. If the input is invalid, an error message is displayed.</remarks>
    /// <param name="addAction">An action to be executed after the operation. The parameter passed to the action indicates whether the operation
    /// was successful.</param>
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
                ddlSymbol.Enabled = true;
                SetLocalSymbol(DateOnly.FromDateTime(dtmContractMonth.Value));
                txtContractId.Text = string.Empty;
                txtDescription.Text = string.Empty;
                _lastContractIndex = lstFuturesOptionContractIds.SelectedIndex;
                _editMode = EditMode.Add;
                addAction(false);
                break;
            case EditMode.Add:
                if (int.TryParse(txtStrikePrice.Text, out int strikePrice))
                {
                    var symbol = _viewModel.GetSymbol(ddlSymbol.SelectedIndex).ShortCode;
                    var maturityDate = $"{dtmContractMonth.Value:yyyyMMdd}";
                    var optionType = _viewModel.GetOptionType(ddlOptionType.SelectedIndex).ShortCode.Substring(0, 1);
                    txtContractId.Text = $"{symbol}{maturityDate}{optionType}{strikePrice}";
                    var futuresOptionContract = new FuturesOptionContractReadModel
                    (
                        contractId: txtContractId.Text,
                        symbol: symbol,
                        localSymbol: txtLocalSymbol.Text,
                        securityType: _viewModel.GetSecurityType(ddlSecurityType.SelectedIndex).ShortCode,
                        currency: _viewModel.GetCurrency(ddlCurrency.SelectedIndex).ShortCode,
                        exchange: _viewModel.GetExchange(ddlExchange.SelectedIndex).ShortCode,
                        multiplier: _viewModel.GetMultiplier(ddlMultiplier.SelectedIndex).ShortCode,
                        contractMonth: DateOnly.FromDateTime(dtmContractMonth.Value),
                        optionType: _viewModel.GetOptionType(ddlOptionType.SelectedIndex).ShortCode,
                        strikePrice: strikePrice,
                        description: txtDescription.Text
                    );
                    _viewModel.OnAddAction = addAction;
                    _viewModel.AddFuturesOptionContract(futuresOptionContract, true);
                }
                else
                    MessageBox.Show("Invalid StrikePrice entered", "System Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                break;
        }

    }

    /// <summary>
    /// Attempts to close the current operation and invokes the specified callback with the result.
    /// </summary>
    /// <param name="closeAction">A callback action that is invoked with a <see langword="true"/> value if there are items in the  futures option
    /// contract list; otherwise, <see langword="false"/>.</param>
    /// <returns><see langword="true"/> if the operation is already in a view mode and no further action is required;  otherwise,
    /// <see langword="false"/>.</returns>
    public bool Close(Action<bool> closeAction)
    {
        switch(_editMode)
        {
            case EditMode.Add:
            case EditMode.Change:
                ShowSelectedFuturesOptionContract(_lastContractIndex);
                _editMode = EditMode.View;
                closeAction(lstFuturesOptionContractIds.Items.Count > 0);
                lstFuturesOptionContractIds.Enabled = true;
                return false;
        }
        return true;
    }

    /// <summary>
    /// Modifies the state of the application based on the current edit mode and performs the specified action.
    /// </summary>
    /// <remarks>This method transitions between different edit modes, enabling or disabling UI elements     
    /// and updating the contract details as necessary. The behavior of the method depends on the      current edit
    /// mode:     <list type="bullet">     <item>     <description>     In <see cref="EditMode.View"/>, the method
    /// enables editing of contract-related fields      and prepares the application for changes.     </description>    
    /// </item>     <item>     <description>     In <see cref="EditMode.Change"/>, the method validates and updates the
    /// contract details      and invokes the provided <paramref name="changeAction"/> delegate.     </description>    
    /// </item>     </list>     If the strike price is invalid during the <see cref="EditMode.Change"/> state, an error 
    /// message is displayed to the user.</remarks>
    /// <param name="changeAction">An <see cref="Action{T}"/> delegate that is invoked with a <see langword="false"/> value      to indicate the
    /// completion of the state change operation.</param>
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
                ddlSymbol.Enabled = true;
                txtLocalSymbol.Enabled = true;
                _lastContractIndex = lstFuturesOptionContractIds.SelectedIndex;
                _originalContractId = $"{lstFuturesOptionContractIds.SelectedItem}";
                _editMode = EditMode.Change;
                changeAction(false);
                lstFuturesOptionContractIds.Enabled = false;
                break;
            case EditMode.Change:
                if (int.TryParse(txtStrikePrice.Text, out int strikePrice))
                {
                    var symbol = _viewModel.GetSymbol(ddlSymbol.SelectedIndex).ShortCode;
                    var maturityDate = $"{dtmContractMonth.Value:yyyyMMdd}";
                    var optionType = _optionTypeBinding?.GetValue().Substring(0, 1);
                    txtContractId.Text = $"{symbol}{maturityDate}{optionType}{strikePrice}";
                    var futuresOptionContract = new FuturesOptionContractReadModel(
                        contractId: txtContractId.Text,
                        symbol: symbol,
                        localSymbol: txtLocalSymbol.Text,
                        securityType: _viewModel.GetSecurityType(ddlSecurityType.SelectedIndex).ShortCode,
                        currency: _viewModel.GetCurrency(ddlCurrency.SelectedIndex).ShortCode,
                        exchange: _viewModel.GetExchange(ddlExchange.SelectedIndex).ShortCode,
                        multiplier: _viewModel.GetMultiplier(ddlMultiplier.SelectedIndex).ShortCode,
                        contractMonth: DateOnly.FromDateTime(dtmContractMonth.Value),
                        optionType: _viewModel.GetOptionType(ddlOptionType.SelectedIndex).ShortCode,
                        strikePrice: strikePrice,
                        description: txtDescription.Text
                    );
                    _viewModel.OnChangeAction = changeAction;
                    _viewModel.ChangeFuturesOptionContract(_originalContractId!, futuresOptionContract, true);
                }
                else
                    MessageBox.Show("Invalid StrikePrice entered", "System Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                break;
        }
    }

    /// <summary>
    /// Removes the selected futures option contract after user confirmation.
    /// </summary>
    /// <remarks>This method retrieves the contract ID of the currently selected futures option contract  and
    /// prompts the user for confirmation before removing it. If the user confirms, the contract  is removed from the
    /// underlying data source.</remarks>
    public void Remove()
    {
        var contractId = _viewModel.GetFuturesOptionContract(lstFuturesOptionContractIds.SelectedIndex)?.ContractId;
        if (contractId is not null)
        {
            if (MessageBox.Show($"Are you sure you want ro remove Futures Option Contract: {contractId} ?", "Remove Futures Option Contract", MessageBoxButtons.YesNo) == DialogResult.Yes)
                _viewModel.RemoveFuturesOptionContract(contractId, true);
        }
    }

    /// <summary>
    /// Loads the futures option contract IDs for the selected symbol and updates the data source of the contract ID
    /// list.
    /// </summary>
    /// <remarks>This method retrieves the futures option contracts associated with the currently selected
    /// symbol in the dropdown list  and populates the contract ID list control. If a valid <paramref
    /// name="contractId"/> is provided and exists in the  retrieved list, it will be preselected.</remarks>
    /// <param name="contractId">An optional contract ID to preselect in the list. If the specified contract ID is found in the list, it will be
    /// selected;  otherwise, the first item in the list will be selected by default.</param>
    void LoadFuturesOptionContractIds(string? contractId = null)
    {
        var symbol = _viewModel.GetSymbol(ddlSymbol.SelectedIndex).ShortCode;
        _viewModel.LoadFuturesOptionContracts(symbol, futuresOptionContracts =>
        {
            lstFuturesOptionContractIds.DataSource = futuresOptionContracts.Select(e => e.ContractId).ToList();
            var selectedIndex = 0;
            if (!string.IsNullOrWhiteSpace(contractId) && lstFuturesOptionContractIds.Items.IndexOf(contractId) > -1)
                selectedIndex = lstFuturesOptionContractIds.Items.IndexOf(contractId);
            lstFuturesOptionContractIds.SelectedIndex = selectedIndex;
            _viewModel.OnDataLoaded(futuresOptionContracts.Count > 0);
        });
    }

    /// <summary>
    /// Displays the details of the selected futures option contract in the user interface.
    /// </summary>
    /// <remarks>This method retrieves the futures option contract corresponding to the specified index and
    /// populates various UI fields with its details. If no contract is found for the given index, the method exits
    /// without making any changes. The UI fields are updated to reflect the contract's properties and are set to a
    /// read-only state where applicable.</remarks>
    /// <param name="selectedIndex">The zero-based index of the selected futures option contract in the data source.</param>
    void ShowSelectedFuturesOptionContract(int selectedIndex)
    {
        var foc = _viewModel.GetFuturesOptionContract(selectedIndex);
        if (foc is   null) 
            return;
        txtContractId.ReadOnly = false;
        txtContractId.Text = foc.ContractId;
        txtContractId.ReadOnly = true;
        txtDescription.ReadOnly = false;
        txtDescription.Text = foc.Description ?? string.Empty;
        txtDescription.ReadOnly = true;
        dtmContractMonth.Value = foc.ContractMonth.ToDateTime(TimeOnly.MinValue);
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
        ddlSymbol.Enabled = false;
    }

    enum EditMode
    {
        View,
        Add,
        Change
    }


    /// <summary>
    /// Sets the local symbol text based on the selected symbol and the specified value date.
    /// </summary>
    /// <param name="valueDate">The date used to generate the local symbol. Must be a valid <see cref="DateOnly"/> value.</param>
    void SetLocalSymbol(DateOnly valueDate)
        => txtLocalSymbol.Text = FuturesOptionContractReadModel.GetLocalSymbol(ddlSymbol.Text, valueDate);


    /// <summary>
    /// Sets the description text for the selected financial instrument based on the current input values.
    /// </summary>
    /// <remarks>This method constructs a description string using the selected symbol, option type, contract
    /// month, strike price, and exchange. The description is displayed in the associated text box. If either the symbol
    /// or option type is not selected, the method does nothing.</remarks>
    void SetDescription()
    {
        if (lstFuturesOptionContractIds.SelectedIndices.Count == 0)
            return;
        var foc = _viewModel.GetFuturesOptionContract(lstFuturesOptionContractIds.SelectedIndices[0]);
        txtDescription.Text = foc?.Description ?? string.Empty;
    }

    /// <summary>
    /// Sets the contract ID based on the selected symbol, option type, contract month, and strike price.
    /// </summary>
    /// <remarks>This method updates the contract ID text field by combining the short code of the selected
    /// symbol,  the formatted contract month, the short code of the selected option type, and the strike price.  If the
    /// selected indices for the symbol or option type are invalid, or if the strike price is not  a valid integer, the
    /// method does nothing.</remarks>
    void SetContractId()
    {
        if (lstFuturesOptionContractIds.SelectedIndices.Count == 0)
            return;
        var foc = _viewModel.GetFuturesOptionContract(lstFuturesOptionContractIds.SelectedIndices[0]);
        txtContractId.Text = foc?.ContractId ?? string.Empty;
    }

    public void Import()
        => throw new NotImplementedException();

    public void Open()
        => throw new NotImplementedException();

    void IFormControl.Resize(Control parentControl)
        => throw new NotImplementedException();

    public void Close()
        => throw new NotImplementedException();

    void ddlSymbol_SelectedIndexChanged(object sender, EventArgs e)
    {
        var symbol = _viewModel.GetSymbol(ddlSymbol.SelectedIndex).ShortCode;
        _viewModel.LoadFuturesOptionContracts(symbol, futuresOptionContracts =>
            this.Post(() => {
                if (futuresOptionContracts is not null && futuresOptionContracts.Count == 0)
                {
                    lstFuturesOptionContractIds.DataSource = futuresOptionContracts;
                    _viewModel.OnDataLoaded(false);
                    SetDescription();
                    SetContractId();
                }
            }));
    }

    void lstFuturesOptionContractIds_SelectedIndexChanged(object sender, EventArgs e)
        => ShowSelectedFuturesOptionContract(lstFuturesOptionContractIds.SelectedIndices[0]);

    void dtmContractMonth_ValueChanged(object sender, EventArgs e)
    {
        var valueDate = new DateOnly(dtmContractMonth.Value.Year, dtmContractMonth.Value.Month, dtmContractMonth.Value.Day);
        SetLocalSymbol(valueDate);
        SetDescription();
        SetContractId();
    }

    void txtStrikePrice_Leave(object sender, EventArgs e)
    {
        SetDescription();
        SetContractId();
    }

    void ddlExchange_SelectedIndexChanged(object sender, EventArgs e)
    {
        SetDescription();
    }

    void ddlOptionType_SelectedIndexChanged(object sender, EventArgs e)
    {
        SetContractId();
        SetDescription();
    }

}

