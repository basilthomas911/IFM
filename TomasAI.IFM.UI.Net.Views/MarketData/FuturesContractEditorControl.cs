using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.ViewModels.MarketData;
using TomasAI.IFM.UI.Net.Models;
using TomasAI.IFM.UI.Net.Models.Reference;

namespace TomasAI.IFM.UI.Net.Views.MarketData;

/// <summary>
/// Represents a user control for managing and editing futures contracts.
/// </summary>
/// <remarks>This control provides functionality to load, add, change, and remove futures contracts. It also
/// supports loading reference data such as currencies, exchanges, and symbols required for futures contract management.
/// The control is designed to be used within a larger application that manages financial instruments.</remarks>
public partial class FuturesContractEditorControl
    : UserControl, IControlCommand, IAsyncFormControl
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
       MarketDataTypography.Apply(this);
       MarketDataInputPalette.Apply(this);
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
        _editMode = EditMode.View;
        _dataLoaded = dataLoaded;
        _ = LoadEditorAsync();
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
                dtmLastTradeDate.Value = EasternTime.GetNow(TimeProvider.System);
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
                ddlOnTheRun.SelectedIndex = 0;
                ddlOnTheRun.Enabled = true;
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
                var futuresContract = new FuturesContractV3ReadModel
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
                    onTheRun: ddlOnTheRun.SelectedIndex == 0
                );
                _viewModel.PrepareAdd(futuresContract);
                _ = AddPreparedContractAsync(futuresContract.ContractId);
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
                txtDescription.Enabled = true;
                dtmLastTradeDate.Enabled = false;
                ddlSecurityType.Enabled = true;
                ddlCurrency.Enabled = true;
                ddlExchange.Enabled = true;
                ddlMultiplier.Enabled = true;
                ddlOnTheRun.Enabled = true;
                ddlSymbol.Enabled = true;
                _lastContractIndex = lstFuturesContractIds.SelectedIndex;
                _editMode = EditMode.Change;
                lstFuturesContractIds.Enabled = false;
                changeAction?.Invoke(false);
                break;
            case EditMode.Change:
                var symbol = _viewModel.GetSymbol(ddlSymbol.SelectedIndex);
                var futuresContractId = _viewModel.GetFuturesContract(lstFuturesContractIds.SelectedIndex)!.Id;
                var maturityDate = $"{dtmLastTradeDate.Value:yyyyMMdd}";
                txtContractId.Text = $"{symbol}{maturityDate}";
                var futuresContract = new FuturesContractV3ReadModel
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
                    onTheRun: ddlOnTheRun.SelectedIndex == 0
                );
                _viewModel.PrepareChange(futuresContractId, futuresContract);
                _ = ChangePreparedContractAsync(futuresContract.ContractId);
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
            {
                _viewModel.PrepareRemove(contract!.Id);
                _ = RemovePreparedContractAsync();
            }
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
        _ = ((IAsyncFormControl)this).CloseAsync();
    }

    async ValueTask IAsyncFormControl.CloseAsync()
        => await _viewModel.StopAsync(CancellationToken.None);


    async Task LoadEditorAsync()
        => await ExecuteOperationAsync(_viewModel.LoadOperation, () =>
        {
            BindLookup(ddlCurrency, _viewModel.Currencies.Select(value => value.Description));
            BindLookup(ddlSecurityType, _viewModel.SecurityTypes.Select(value => value.Description));
            BindLookup(ddlExchange, _viewModel.Exchanges.Select(value => value.Description));
            BindLookup(ddlMultiplier, _viewModel.Multipliers.Select(value => value.Description));
            BindLookup(ddlSymbol, _viewModel.Symbols.Select(value => value.Description));
            BindLookup(ddlOnTheRun, _viewModel.OnTheRun);
            BindContracts();
            _dataLoaded?.Invoke(_viewModel.FuturesContracts.Count > 0);
        });

    async Task AddPreparedContractAsync(string contractId)
        => await ExecuteOperationAsync(_viewModel.AddOperation, () =>
        {
            _editMode = EditMode.View;
            BindContracts(contractId);
            _addAction?.Invoke(true);
            _refreshAction();
            lstFuturesContractIds.Enabled = true;
        });

    async Task ChangePreparedContractAsync(string contractId)
        => await ExecuteOperationAsync(_viewModel.ChangeOperation, () =>
        {
            _editMode = EditMode.View;
            BindContracts(contractId);
            _changeAction?.Invoke(true);
            _refreshAction();
            lstFuturesContractIds.Enabled = true;
        });

    async Task RemovePreparedContractAsync()
        => await ExecuteOperationAsync(_viewModel.RemoveOperation, () =>
        {
            _editMode = EditMode.View;
            BindContracts();
            _refreshAction();
            lstFuturesContractIds.Enabled = true;
        });

    async Task ExecuteOperationAsync(IAsyncOperation operation, Action onCompleted)
    {
        Cursor = Cursors.WaitCursor;
        try
        {
            await operation.ExecuteAsync();
            onCompleted();
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                text: exception.Message,
                caption: "Futures Contract Editor Error",
                buttons: MessageBoxButtons.OK,
                icon: MessageBoxIcon.Error);
        }
        finally
        {
            Cursor = Cursors.Default;
        }
    }

    static void BindLookup(ComboBox comboBox, IEnumerable<string> values)
    {
        comboBox.Items.Clear();
        foreach (var value in values)
            comboBox.Items.Add(value);
        comboBox.SelectedIndex = comboBox.Items.Count > 0 ? 0 : -1;
    }

    void BindContracts(string? contractId = null)
    {
        _canChangeRemove = false;
        LoadFuturesContractIds(contractId ?? string.Empty, [.. _viewModel.FuturesContracts]);
        _canChangeRemove = _viewModel.FuturesContracts.Count > 0;
        ddlSymbol.Enabled = false;
    }

    /// <summary>
    /// Populates a list control with futures contract IDs and selects the specified contract ID if it exists.
    /// </summary>
    /// <remarks>If the specified <paramref name="contractId"/> is not found in the provided <paramref
    /// name="futuresContracts"/>, the first item in the list will be selected by default.</remarks>
    /// <param name="contractId">The contract ID to select in the list. If null or whitespace, the first item will be selected.</param>
    /// <param name="futuresContracts">An array of futures contracts to populate the list. If null or empty, the list will remain empty.</param>
    void LoadFuturesContractIds(string contractId, FuturesContractV3ReadModel[] futuresContracts)
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
   static int GetSelectedIndex(IEnumerable<LookupTypeUiModel> lookupTypes, string shortCode)
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
        ddlOnTheRun.SelectedIndex = fc.OnTheRun ? 0 : 1;
        ddlOnTheRun.Enabled = false;
        ddlSymbol.SelectedIndex = GetSelectedIndex(_viewModel.Symbols, fc.Symbol);
        ddlSymbol.Enabled = false;
        SetLocalSymbol(DateOnly.FromDateTime(dtmLastTradeDate.Value));
        txtDescription.Text = fc.Description ?? string.Empty;
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

