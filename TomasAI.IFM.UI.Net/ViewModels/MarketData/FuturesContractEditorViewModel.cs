using TomasAI.IFM.Contracts;
using TomasAI.IFM.Extensions;
using TomasAI.IFM.Models;
using TomasAI.IFM.Shared.Reference.ViewModels;
using TomasAI.IFM.Shared.MarketData;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.ViewModels.MarketData;

/// <summary>
/// Represents a view model for managing and editing futures contracts.
/// </summary>
/// <remarks>The <see cref="FuturesContractEditorViewModel"/> class provides functionality for managing futures
/// contracts, including adding, updating, and removing contracts, as well as loading related lookup data such as
/// symbols, security types, currencies, exchanges, and multipliers. It also supports asynchronous operations for
/// retrieving and updating futures contract data and raises events to notify subscribers of changes to the underlying
/// data.</remarks>
public class FuturesContractEditorViewModel 
    : BaseEditorViewModel
{
    List<LookupTypeReadModel> _symbols = [];
    List<LookupTypeReadModel> _securityTypes = [];
    List<LookupTypeReadModel> _currencies = [];
    List<LookupTypeReadModel> _exchanges = [];
    List<LookupTypeReadModel> _multipliers = [];
    List<string> _currentlyTraded = [];
    List<FuturesContractV2ReadModel> _futuresContracts = [];
    Dictionary<int, string> _contractMonthMap = [];
    Guid _commandId;
    Action<Guid> _setCommandId;
    MarketDataEventModel _eventModel;
    MarketDataCommandModel _commandModel;
    MarketDataQueryModel _queryModel;
    ReferenceQueryModel _referenceQueryModel;

    /// <summary>
    /// Initializes a new instance of the <see cref="FuturesContractEditorViewModel"/> class.
    /// </summary>
    /// <remarks>This constructor initializes the view model by retrieving required models from the provided
    /// <paramref name="appRoot"/>. The models include <see cref="MarketDataEventModel"/>, <see
    /// cref="MarketDataCommandModel"/>, <see cref="MarketDataQueryModel"/>, and <see cref="ReferenceQueryModel"/>.
    /// Additionally, it sets up an internal command ID and a delegate for updating the command ID.</remarks>
    /// <param name="appRoot">The application root object that provides access to shared models and services.</param>
    public FuturesContractEditorViewModel(IAppRoot appRoot):base(appRoot)
    {
        _eventModel = AppRoot.GetModel<MarketDataEventModel>();
        _commandModel = AppRoot.GetModel<MarketDataCommandModel>();
        _queryModel = AppRoot.GetModel<MarketDataQueryModel>();
        _referenceQueryModel = AppRoot.GetModel<ReferenceQueryModel>();
        _commandId = Guid.Empty;
        _setCommandId = e => _commandId = e;
    }

    public ICollection<LookupTypeReadModel> Currencies => _currencies;
    public ICollection<LookupTypeReadModel> SecurityTypes => _securityTypes;
    public ICollection<LookupTypeReadModel> Exchanges => _exchanges;
    public ICollection<LookupTypeReadModel> Multipliers => _multipliers;
    public ICollection<LookupTypeReadModel> Symbols => _symbols;
    public ICollection<FuturesContractV2ReadModel> FuturesContracts => _futuresContracts;

    public Action<LookupTypeReadModel[]> OnSecurityTypesLoaded = e => { };
    public Action<LookupTypeReadModel[]> OnCurrenciesLoaded = e => { };
    public Action<LookupTypeReadModel[]> OnExchangesLoaded = e => { };
    public Action<LookupTypeReadModel[]> OnMultipliersLoaded = e => { };
    public Action<LookupTypeReadModel[]> OnSymbolsLoaded = e => { };
    public Action<string[]> OnCurrentlyTraded = e => { };
    public Action<FuturesContractV2ReadModel[]> OnFuturesContractsLoaded = e => { };
    public Action OnFuturesContractAdded = () => { };
    public Action OnFuturesContractChanged = () => { };
    public Action OnFuturesContractRemoved = () => { };
    public Action OnFuturesContractIdsLoaded = () => { };
    public Action OnWaitCursor = () => { };
    public Action OnDefaultCursor = () => { };
    public Action<bool> OnChangeAction = (enabled) => { };

    /// <summary>
    /// Adds a new futures contract to the system.
    /// </summary>
    /// <remarks>This method executes the addition of a futures contract asynchronously and ensures that the 
    /// operation is processed before refreshing the list of added futures contract IDs.  The method also handles any
    /// errors that occur during the operation by invoking the appropriate error handler.</remarks>
    /// <param name="futuresContract">The futures contract to be added. This parameter cannot be <see langword="null"/>.</param>
    public void AddFuturesContract(FuturesContractV2ReadModel futuresContract, bool overwrite)
        => _commandModel.Execute(async model => {
            model.OnError((errorCode, errorMsg) => OnError(errorCode, errorMsg));
            IsArgumentNull.Check(futuresContract);
            OnWaitCursor?.Invoke();
            await model.AddFuturesContractAsync(futuresContract, overwrite);
            await Task.Delay(250); // Allow time for the command to be processed
            OnDefaultCursor?.Invoke();
            RefreshAddedFuturesContractIds($"Futures Contract {futuresContract.ContractId} Added");
        });

     /// <summary>
    /// Updates an existing futures contract with new details.
    /// </summary>
    /// <remarks>This method executes the update operation asynchronously and handles any errors that occur
    /// during the process.</remarks>
    /// <param name="originalContractId">The unique identifier of the original futures contract to be updated. Cannot be <see langword="null"/> or empty.</param>
    /// <param name="futuresContract">The updated details of the futures contract. Cannot be <see langword="null"/>.</param>
    /// <param name="overwrite">A value indicating whether to overwrite the existing contract details.  <see langword="true"/> to overwrite the
    /// existing details; otherwise, <see langword="false"/>.</param>
    public void ChangeFuturesContract(FuturesContractId originalContractId, FuturesContractV2ReadModel futuresContract, bool overwrite, Action<bool> changeCompleted)
        =>  _commandModel.Execute(async model => {
            model.OnError((errorCode, errorMsg) => OnError(errorCode, errorMsg));
            IsArgumentNull.Check(originalContractId);
            IsArgumentNull.Check(futuresContract);
            OnWaitCursor?.Invoke();
            await model.ChangeFuturesContractAsync(originalContractId, futuresContract, overwrite);
            await Task.Delay(250); // Allow time for the command to be processed
            RefreshChangedFuturesContractIds($"Futures Contract {originalContractId} Changed");
            changeCompleted?.Invoke(true);
            OnChangeAction?.Invoke(true);
            OnDefaultCursor?.Invoke();
        });

    /// <summary>
    /// Removes a specified futures contract from the system.
    /// </summary>
    /// <remarks>This method removes the specified futures contract and optionally overwrites associated data.
    /// A delay is introduced to allow time for the command to be processed. The method also triggers a refresh of the
    /// removed futures contract identifiers.</remarks>
    /// <param name="contractId">The unique identifier of the futures contract to be removed. Cannot be <see langword="null"/>.</param>
    /// <param name="overwrite">A value indicating whether to overwrite any existing data associated with the specified futures contract. <see
    /// langword="true"/> to overwrite; otherwise, <see langword="false"/>.</param>
    public void RemoveFuturesContract(FuturesContractId contractId, bool overwrite)
        =>  _commandModel.Execute(async model => {
                model.OnError((errorCode, errorMsg) => OnError(errorCode, errorMsg));
                IsArgumentNull.Check(contractId);
                OnWaitCursor?.Invoke(); 
                await model.RemoveFuturesContractAsync(contractId, overwrite);
                await Task.Delay(250); // Allow time for the command to be processed
                RefreshRemovedFuturesContractIds($"Futures Contract {contractId} Removed");
                OnDefaultCursor?.Invoke();
        });

    /// <summary>
    /// Retrieves the currency shortcode for the specified index.
    /// </summary>
    /// <param name="index">The zero-based index of the currency to retrieve. Must be within the valid range of available currencies.</param>
    /// <returns>The shortcode of the currency at the specified index.</returns>
    public string GetCurrency(int index) 
        => _currencies.GetLookupType(index).ShortCode;

    /// <summary>
    /// Retrieves the security type associated with the specified index.
    /// </summary>
    /// <param name="index">The index of the security type to retrieve. Must be a valid index within the range of available security types.</param>
    /// <returns>A string representing the short code of the security type corresponding to the specified index.</returns>
    public string GetSecurityType(int index) 
        => _securityTypes.GetLookupType(index).ShortCode;

    /// <summary>
    /// Retrieves the exchange short code for the specified index.
    /// </summary>
    /// <param name="index">The index of the exchange to retrieve. Must be a valid index within the range of available exchanges.</param>
    /// <returns>The short code of the exchange at the specified index.</returns>
    public string GetExchange(int index) 
        => _exchanges.GetLookupType(index).ShortCode;

    /// <summary>
    /// Retrieves the multiplier's short code based on the specified index.
    /// </summary>
    /// <param name="index">The zero-based index of the multiplier to retrieve. Must be within the valid range of available multipliers.</param>
    /// <returns>The short code of the multiplier corresponding to the specified index.</returns>
    public string GetMultiplier(int index) 
        => _multipliers.GetLookupType(index).ShortCode;

    /// <summary>
    /// Retrieves the symbol's short code at the specified index.
    /// </summary>
    /// <param name="index">The zero-based index of the symbol to retrieve.</param>
    /// <returns>The short code of the symbol at the specified index.</returns>
    public string GetSymbol(int index) 
        => _symbols.GetLookupType(index).ShortCode;

    /// <summary>
    /// Retrieves the description of a symbol at the specified index.
    /// </summary>
    /// <param name="index">The zero-based index of the symbol whose description is to be retrieved.</param>
    /// <returns>The description of the symbol at the specified index.</returns>
    public string GetSymbolDescription(int index) 
        => _symbols.GetLookupType(index).Description;

    /// <summary>
    /// Retrieves the contract month name corresponding to the specified month index.
    /// </summary>
    /// <param name="monthIndex">The zero-based index of the month. Must be within the valid range of indices for the contract month map.</param>
    /// <returns>The name of the contract month associated with the specified index.</returns>
    public string GetContractMonth(int monthIndex) 
        => _contractMonthMap.Count > 0 && monthIndex >= 0 && monthIndex < _contractMonthMap.Count 
                ? _contractMonthMap[monthIndex]
                : "<empty>";

    /// <summary>
    /// Retrieves the futures contract at the specified index.
    /// </summary>
    /// <param name="index">The zero-based index of the futures contract to retrieve.</param>
    /// <returns>The <see cref="FuturesContractV2ReadModel"/> at the specified index if the collection is not empty; otherwise,
    /// the default value for <see cref="FuturesContractV2ReadModel"/>.</returns>
    public FuturesContractV2ReadModel GetFuturesContract(int index) 
        => _futuresContracts?.Count > 0 ? _futuresContracts[index] : default!;

    /// <summary>
    /// Determines whether all lookup types have been successfully loaded.
    /// </summary>
    /// <remarks>This method checks the counts of various lookup type collections to ensure they are
    /// non-empty. It returns <see langword="false"/> if any of the collections are null or empty.</remarks>
    /// <returns><see langword="true"/> if all lookup types contain at least one entry; otherwise, <see langword="false"/>.</returns>
    public bool AllLookupTypesLoaded()
        => (_currencies.Count > 0) &&
            (_securityTypes.Count > 0) &&
            (_exchanges.Count > 0) &&
            (_multipliers.Count > 0) &&
            (_symbols.Count > 0) &&
            (_contractMonthMap.Count > 0);
        
    /// <summary>
    /// Loads the available security types and updates the internal collection.
    /// </summary>
    /// <remarks>This method retrieves security types asynchronously and updates the internal list.  If any
    /// security types are loaded, the <see cref="OnSecurityTypesLoaded"/> event is invoked  with the updated
    /// collection.</remarks>
    public void LoadSecurityTypes()
        => _referenceQueryModel.Execute(async model =>
            await model.LoadSecurityTypesAsync(lookupTypes =>
            {
                _securityTypes = [];
                if (lookupTypes?.Count > 0)
                {
                    _securityTypes.AddRange(lookupTypes);
                    OnSecurityTypesLoaded?.Invoke([.. _securityTypes]);
                }
            }));

    /// <summary>
    /// Loads the list of currencies and updates the internal collection.
    /// </summary>
    /// <remarks>This method retrieves currency data asynchronously and updates the internal collection of
    /// currencies. If any currencies are loaded, the <see cref="OnCurrenciesLoaded"/> event is invoked with the updated
    /// list.</remarks>
    public void LoadCurrencies()
        => _referenceQueryModel.Execute(async model =>
            await model.LoadCurrenciesAsync(lookupTypes =>
            {
                _currencies = [];
                if (lookupTypes?.Count > 0)
                {
                    _currencies.AddRange(lookupTypes);
                    OnCurrenciesLoaded?.Invoke([.. _currencies]);
                }
            }));

    /// <summary>
    /// Loads exchange data asynchronously and updates the internal collection of exchanges.
    /// </summary>
    /// <remarks>This method retrieves exchange data using the provided query model and updates the internal
    /// list of exchanges. If any exchanges are loaded, the <see cref="OnExchangesLoaded"/> event is invoked with the
    /// loaded data.</remarks>
    public void LoadExchanges()
        => _referenceQueryModel.Execute(async model =>
            await model.LoadExchangesAsync(lookupTypes =>
            {
                _exchanges = [];
                if (lookupTypes?.Count > 0)
                {
                    _exchanges.AddRange(lookupTypes);
                    OnExchangesLoaded?.Invoke([.. lookupTypes]);
                }
            }));

    /// <summary>
    /// Loads multiplier data asynchronously and updates the internal collection.
    /// </summary>
    /// <remarks>This method retrieves multiplier data using the provided query model and updates the internal
    /// collection of multipliers. If any multipliers are loaded, the <see cref="OnMultipliersLoaded">  event is invoked
    /// with the loaded data.</remarks>
    public void LoadMultipliers()
        => _referenceQueryModel.Execute(async model =>
            await model.LoadMultipliersAsync(lookupTypes =>
            {
                _multipliers = [];
                if (lookupTypes.Count > 0)
                {
                    _multipliers.AddRange(lookupTypes);
                    OnMultipliersLoaded?.Invoke([.. lookupTypes]);
                }
            }));

    /// <summary>
    /// Loads the currently traded options and triggers the associated event.
    /// </summary>
    /// <remarks>This method initializes the list of currently traded options and invokes the <see
    /// cref="OnCurrentlyTraded"/> event  to notify subscribers of the updated data. The event provides the updated list
    /// of options.</remarks>
    public void LoadCurrentlyTraded()
    {
        _currentlyTraded = ["Yes", "No"];
        OnCurrentlyTraded?.Invoke([.. _currentlyTraded]);
    }

    /// <summary>
    /// Initializes the mapping of month numbers to their corresponding contract month codes.
    /// </summary>
    /// <remarks>This method populates a dictionary where the keys represent month numbers (1 through 12), and
    /// the values are the corresponding contract month codes used in financial or commodity markets.</remarks>
    public void LoadContractMonths()
        => _contractMonthMap = new Dictionary<int, string> {
            {1, "F"},
            {2, "G"},
            {3, "H"},
            {4, "J"},
            {5, "K"},
            {6, "M"},
            {7, "N"},
            {8, "Q"},
            {9, "U"},
            {10, "V"},
            {11, "X"},
            {12, "Z"},
        };

    /// <summary>
    /// Loads symbols asynchronously and updates the internal symbol collection.
    /// </summary>
    /// <remarks>This method retrieves symbols based on the provided lookup types and updates the internal
    /// symbol list. If any symbols are loaded, the <see cref="OnSymbolsLoaded"/> event is raised with the loaded
    /// symbols.</remarks>
    public void LoadSymbols()
        => _referenceQueryModel.Execute(async model =>
                await model.LoadSymbolsAsync(lookupTypes =>
                {
                    _symbols = [];
                    if (lookupTypes?.Count > 0)
                    {
                        _symbols.AddRange(lookupTypes);
                        OnSymbolsLoaded?.Invoke([.. _symbols]);
                    }
                }));

    /// <summary>
    /// Loads futures contract IDs and updates the internal collection.
    /// </summary>
    /// <remarks>This method retrieves futures contract IDs asynchronously and updates the internal
    /// collection.  If any futures contracts are found, the <see cref="OnFuturesContractIdsLoaded"/> event is
    /// invoked.</remarks>
    /// <param name="contractId">An optional contract ID to filter the futures contracts. If not provided, all available futures contracts are
    /// loaded.</param>
    public void LoadFuturesContractIds(string? contractId = default)
        => _queryModel.Execute(async model =>  {
            await model.GetFuturesContractsAsync( futuresContracts =>
            {
                _futuresContracts = [];
                if (futuresContracts.Length > 0)
                {
                    _futuresContracts.AddRange(futuresContracts);
                    OnFuturesContractIdsLoaded?.Invoke();
                }
            });
        });

    /// <summary>
    /// Loads the available futures contracts and updates the internal collection.
    /// </summary>
    /// <remarks>This method retrieves futures contracts asynchronously and updates the internal collection
    /// with the retrieved data. If any futures contracts are loaded, the <see cref="OnFuturesContractsLoaded"/> event
    /// is invoked with the updated collection.</remarks>
    public void LoadFuturesContracts()
        => _queryModel.Execute(async model =>  {
            await model.GetFuturesContractsAsync(futuresContracts =>
            {
                _futuresContracts = [];
                if (futuresContracts.Length > 0)
                {
                    _futuresContracts.AddRange(futuresContracts);
                    OnFuturesContractsLoaded?.Invoke([.. _futuresContracts]);
                }
            });
        });

    /// <summary>
    /// refresh added futures contract id's
    /// </summary>
    /// <param name="consoleStatus"></param>
    void RefreshAddedFuturesContractIds(string consoleStatus)
        => _queryModel.Execute(async model =>  {
            await model.GetFuturesContractsAsync( futuresContracts => {
                UpdateFuturesContracts(futuresContracts);
                OnFuturesContractAdded?.Invoke();
                WriteStatusConsole(LogSourceType.MarketData, consoleStatus);
            });
        });

    /// <summary>
    /// refresh removed futures contract id's
    /// </summary>
    /// <param name="consoleStatus"></param>
    void RefreshRemovedFuturesContractIds( string consoleStatus)
        => _queryModel.Execute(async model => {
            await model.GetFuturesContractsAsync(futuresContracts => {
                UpdateFuturesContracts(futuresContracts);
                OnFuturesContractRemoved?.Invoke();
                WriteStatusConsole(LogSourceType.MarketData, consoleStatus);
            });
        });

    /// <summary>
    /// refresh changed futures contract id's
    /// </summary>
    /// <param name="consoleStatus"></param>
    void RefreshChangedFuturesContractIds(string consoleStatus)
        => _queryModel.Execute(async model =>  {
            await model.GetFuturesContractsAsync(futuresContracts => {
                UpdateFuturesContracts(futuresContracts);
                OnFuturesContractChanged?.Invoke();
                WriteStatusConsole(LogSourceType.MarketData, consoleStatus);
            });
        });

    /// <summary>
    /// update futures contract
    /// </summary>
    /// <param name="futuresContracts"></param>
    void UpdateFuturesContracts(FuturesContractV2ReadModel[] futuresContracts)
    {
        _futuresContracts = [];
        if (futuresContracts is not null && futuresContracts.Length > 0)
            _futuresContracts.AddRange(futuresContracts);
    }

}
