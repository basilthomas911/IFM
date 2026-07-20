using TomasAI.IFM.Contracts;
using TomasAI.IFM.Extensions;
using TomasAI.IFM.Models;
using TomasAI.IFM.Shared.Reference.ViewModels;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.MarketData.Events;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.ViewModels.MarketData;

public class FuturesOptionContractEditorViewModel: BaseEditorViewModel
{
    const int ErrorCode = 9241;

    List<LookupTypeReadModel> _symbols = [];
    bool _symbolsLoaded = false;    
    List<LookupTypeReadModel> _securityTypes = [];
    bool _securityTypesLoaded = false;
    List<LookupTypeReadModel> _currencies = [];
    bool _currenciesLoaded = false;
    List<LookupTypeReadModel> _exchanges = [];
    bool _exchangesLoaded = false;
    List<LookupTypeReadModel> _multipliers = [];
    bool _multipliersLoaded = false;
    List<LookupTypeReadModel> _optionTypes = [];
    bool _optionTypesLoaded = false;
    List<FuturesOptionContractReadModel> _futuresOptionContracts = [];
    ICollection<IEvent> _consumeEvents = [];
    Guid? _commandId;
    Action<Guid>? _setCommandId = _ => { };
    MarketDataEventModel? _eventModel;
    MarketDataCommandModel? _commandModel;
    MarketDataQueryModel? _queryModel;
    ReferenceQueryModel? _referenceQueryModel;

    /// <summary>
    /// Initializes a new instance of the <see cref="FuturesOptionContractEditorViewModel"/> class.
    /// </summary>
    /// <remarks>This constructor sets up the view model by initializing required models and event handlers
    /// for managing  futures option contract operations. The <paramref name="appRoot"/> parameter is used to retrieve
    /// instances  of the necessary models, including <see cref="MarketDataEventModel"/>, <see
    /// cref="MarketDataCommandModel"/>,  <see cref="MarketDataQueryModel"/>, and <see cref="ReferenceQueryModel"/>. 
    /// The view model also subscribes to a predefined set of market data events related to futures option contracts, 
    /// such as addition, modification, and removal events, both for success and failure scenarios.</remarks>
    /// <param name="appRoot">The application root object that provides access to shared models and services.</param>
    public FuturesOptionContractEditorViewModel(IAppRoot appRoot)
        : base(appRoot)
    {
        _commandId = Guid.Empty;
        _eventModel = AppRoot.GetModel<MarketDataEventModel>();
        _commandModel = AppRoot.GetModel<MarketDataCommandModel>();
        _queryModel = AppRoot.GetModel<MarketDataQueryModel>();
        _referenceQueryModel = AppRoot.GetModel<ReferenceQueryModel>();
        _setCommandId = commandId => _commandId = commandId;
        _consumeEvents = [
            new FuturesOptionContractAddedCompleteEvent { }.SetEventSource($"{EventTopic.MarketDataEvents}"),
            new FuturesOptionContractAddedFailEvent{ }.SetEventSource($"{EventTopic.MarketDataEvents}"),
            new FuturesOptionContractChangedCompleteEvent { }.SetEventSource($"{EventTopic.MarketDataEvents}"),
            new FuturesOptionContractChangedFailEvent{ }.SetEventSource($"{EventTopic.MarketDataEvents}"),
            new FuturesOptionContractRemovedCompleteEvent { }.SetEventSource($"{EventTopic.MarketDataEvents}"),
            new FuturesOptionContractRemovedFailEvent{ }.SetEventSource($"{EventTopic.MarketDataEvents}")
        ];
    }

    // public event properties set by editor control...
    public Action<bool> OnDataLoaded = _ => { };
    public Action<bool> OnAddAction = _ => { };
    public Action<bool> OnChangeAction = _ => { };
    public Action OnFuturesOptionContractLoaded = () => { };
    public Action OnFuturesOptionContractAdded = () => { };
    public Action OnFuturesOptionContractRemoved = () => { };
    public Action OnFuturesOptionContractChanged = () => { };
    public Action OnWaitCursor = () => { };
    public Action OnDefaultCursor = () => { };


    /// <summary>
    /// start listening for futures options data editor events
    /// </summary>
    public void StartListener()
        => _eventModel?.Execute(async e => {
            e.OnError((errorCode, errorMsg) => OnError(errorCode, errorMsg));
            await e.StartMarketDataListenerAsync(_consumeEvents, HandleEventAsync);
        });

    /// <summary>
    /// stop listening for futures options editor events
    /// </summary>
    public void StopListener()
        => _eventModel?.Execute(async e => {
            e.OnError((errorCode, errorMsg) => OnError(errorCode, errorMsg));
            await e.StopMarketDataListenerAsync();
        });

    /// <summary>
    /// execute futures options editor event actions
    /// </summary>
    /// <param name="e"></param>
    /// <returns></returns>
    ValueTask HandleEventAsync(IEvent e)
    {
        if (_commandId != e.CommandId)
            return ValueTask.CompletedTask;
        return e switch
        {
            FuturesOptionContractAddedCompleteEvent o => AddFuturesOptionContractCompleted(o),
            FuturesOptionContractAddedFailEvent o => AddFuturesOptionContractFailed(o),
            FuturesOptionContractChangedCompleteEvent o => ChangeFuturesOptionContractCompleted(o),
            FuturesOptionContractChangedFailEvent o => ChangeFuturesOptionContractFailed(o),
            FuturesOptionContractRemovedCompleteEvent o => RemoveFuturesOptionContractCompleted(o),
            FuturesOptionContractRemovedFailEvent o => RemoveFuturesOptionContractFailed(o),
            _ => ValueTask.CompletedTask
        };
    }

    /// <summary>
    /// add futures option contract
    /// </summary>
    /// <param name="futuresOptionContract">new futures option contract</param>
    public void AddFuturesOptionContract(FuturesOptionContractReadModel futuresOptionContract, bool overwrite)
        => _commandModel?.Execute(async model => {
             model.OnError((errorCode, errorMsg) => OnError(errorCode, errorMsg));
             IsArgumentNull.Check(futuresOptionContract);
            OnWaitCursor?.Invoke();
            await model.AddFuturesOptionContractAsync(futuresOptionContract, overwrite);
            //await Task.Delay(250); // delay to allow UI to refresh
            //RefreshFuturesOptionContracts($"Futures Option Contract {futuresOptionContract.ContractId} Added", OnFuturesOptionContractAdded);
        });

    async ValueTask AddFuturesOptionContractCompleted(FuturesOptionContractAddedCompleteEvent e)
    {
        RefreshFuturesOptionContracts($"Futures Option Contract {e.Contract!.ContractId} Added", OnFuturesOptionContractAdded);
        OnDefaultCursor?.Invoke();
        _commandId = Guid.Empty;
        await ValueTask.CompletedTask;
    }

    ValueTask AddFuturesOptionContractFailed(FuturesOptionContractAddedFailEvent e)
    {
        OnDefaultCursor?.Invoke();
        WriteStatusConsole(LogSourceType.MarketData, $"Futures Option Contract Add failed due to: {e.ErrorMessage}");
        OnError?.Invoke(e.ErrorCode, $"Futures Option Contract Add failed due to: {e.ErrorMessage}");
        _commandId = Guid.Empty;
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// change futures option contract
    /// </summary>
    /// <param name="originalContractId"></param>
    /// <param name="futuresOptionContract">updated futures option contract</param>
    /// <param name="overwrite"></param>
    public void ChangeFuturesOptionContract(string originalContractId, FuturesOptionContractReadModel futuresOptionContract, bool overwrite)
        =>  _commandModel?.Execute(async model => {
            model.OnError((errorCode, errorMsg) => OnError(errorCode, errorMsg));
            IsArgumentNull.Check(originalContractId);
            IsArgumentNull.Check(futuresOptionContract);
            OnWaitCursor?.Invoke();
            WriteStatusConsole(LogSourceType.MarketData,  $"Changing Futures Option Contract {originalContractId} ...please wait");
            await model.ChangeFuturesOptionContractAsync(originalContractId, futuresOptionContract, overwrite);
            //await Task.Delay(250); // delay to allow UI to refresh
            //RefreshFuturesOptionContracts($"Futures Option Contract {originalContractId} Changed", OnFuturesOptionContractChanged);
        });

    ValueTask ChangeFuturesOptionContractCompleted(FuturesOptionContractChangedCompleteEvent e)
    {
        RefreshFuturesOptionContracts($"Futures Option Contract {e.Contract!.ContractId} Changed", OnFuturesOptionContractChanged);
        OnDefaultCursor?.Invoke();
        OnDataLoaded?.Invoke(true);
        _commandId = Guid.Empty;
        return ValueTask.CompletedTask;
    }

    ValueTask ChangeFuturesOptionContractFailed(FuturesOptionContractChangedFailEvent e)
    {
        OnDefaultCursor?.Invoke();
        OnDataLoaded?.Invoke(true);
        WriteStatusConsole(LogSourceType.MarketData, e.ErrorCode, $"Futures Option Contract Change failed due to: {e.ErrorMessage}");
        _commandId = Guid.Empty;
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// remove futures contract
    /// </summary>
    /// <param name="contractId"></param>
    /// <param name="overwrite"></param>
    public void RemoveFuturesOptionContract(string contractId, bool overwrite)
        => _commandModel?.Execute(async model => {
                model.OnError((errorCode, errorMsg) => OnError(errorCode, errorMsg));
                IsArgumentNull.Check(contractId);
                OnWaitCursor?.Invoke();
                await model.RemoveFuturesOptionContractAsync(contractId, overwrite);
                //await Task.Delay(250); // delay to allow UI to refresh
                //RefreshFuturesOptionContracts($"Futures Option Contract {contractId} Removed", OnFuturesOptionContractRemoved);
        });

    ValueTask RemoveFuturesOptionContractCompleted(FuturesOptionContractRemovedCompleteEvent e)
    {
        RefreshFuturesOptionContracts($"Futures Option Contract {e.ContractId} Removed", OnFuturesOptionContractRemoved);
        OnDefaultCursor?.Invoke();
        _commandId = Guid.Empty;
        return ValueTask.CompletedTask;
    }

    ValueTask RemoveFuturesOptionContractFailed(FuturesOptionContractRemovedFailEvent e)
    {
        OnDefaultCursor?.Invoke();
        WriteStatusConsole(LogSourceType.MarketData, e.ErrorCode, $"Futures Option Contract Remove failed due to: {e.ErrorMessage}");
        OnError?.Invoke(e.ErrorCode, $"Futures Option Contract Remove failed due to: {e.ErrorMessage}");
        _commandId = Guid.Empty;
        return ValueTask.CompletedTask;
    }

    public LookupTypeReadModel GetSymbol(int index) 
        => _symbols.GetLookupType(index);

    public LookupTypeReadModel GetOptionType(int index) 
        => _optionTypes.GetLookupType(index);

    public LookupTypeReadModel GetSecurityType(int index) 
        => _securityTypes.GetLookupType(index);

    public LookupTypeReadModel GetCurrency(int index) 
        => _currencies.GetLookupType(index);

    public LookupTypeReadModel GetExchange(int index) 
        => _exchanges.GetLookupType(index);

    public LookupTypeReadModel GetMultiplier(int index) 
        => _multipliers.GetLookupType(index);

    public FuturesOptionContractReadModel? GetFuturesOptionContract(int index)
        => index >= 0 && _futuresOptionContracts != null && _futuresOptionContracts.Count > index 
            ? _futuresOptionContracts[index] 
            : default;
         
    public int GetOptionTypeIndex(string shortCode) 
        => _optionTypes.GetLookupTypeIndex(shortCode);

    public int GetSecurityTypeIndex(string shortCode) 
        => _securityTypes.GetLookupTypeIndex(shortCode);

    public int GetCurrencyIndex(string shortCode) 
        => _currencies.GetLookupTypeIndex(shortCode);

    public int GetExchangeIndex(string shortCode) 
        => _exchanges.GetLookupTypeIndex(shortCode);

    public int GetMultiplierIndex(string shortCode) 
        => _multipliers.GetLookupTypeIndex(shortCode);

    /// <summary>
    /// load option types
    /// </summary>
    /// <param name="optionTypesLoaded"></param>
    public void LoadOptionTypes(Action<LookupTypeReadModel[]> optionTypesLoaded)
        => _referenceQueryModel?.Execute(async model => {
            model.OnError((errorCode, errorMsg) => OnError(errorCode, errorMsg));
            await model.LoadOptionTypesAsync(lookupTypes =>
            {
                _optionTypes = [];
                if (lookupTypes?.Count > 0)
                {
                    _optionTypes.AddRange(lookupTypes);
                    optionTypesLoaded?.Invoke([.. lookupTypes]);
                }
                _optionTypesLoaded = true;
                LoadFuturesOptionContracts();
            });
        });

    /// <summary>
    /// load security types
    /// </summary>
    /// <param name="securityTypesLoaded"></param>
    public void LoadSecurityTypes(Action<LookupTypeReadModel[]> securityTypesLoaded)
         => _referenceQueryModel?.Execute(async model => {
             model.OnError((errorCode, errorMsg) => OnError(errorCode, errorMsg));
             await model.LoadSecurityTypesAsync(lookupTypes =>
             {
                 _securityTypes = [];
                 if (lookupTypes?.Count  > 0)
                 {
                     _securityTypes.AddRange(lookupTypes);
                     securityTypesLoaded?.Invoke([.. lookupTypes]);
                 }
                    _securityTypesLoaded = true;
                 LoadFuturesOptionContracts();
             });
         });

    /// <summary>
    /// load currencies
    /// </summary>
    /// <param name="currencyTypesLoaded"></param>
    public void LoadCurrencies(Action<LookupTypeReadModel[]> currencyTypesLoaded)
          => _referenceQueryModel?.Execute(async model => 
              await model.LoadCurrenciesAsync(lookupTypes =>  {
                  _currencies = [];
                  if (lookupTypes?.Count > 0)
                  {
                      _currencies.AddRange(lookupTypes);
                      currencyTypesLoaded?.Invoke([.. lookupTypes]);
                  }
                    _currenciesLoaded = true;
                  LoadFuturesOptionContracts();
              }));

    /// <summary>
    /// load exchanges
    /// </summary>
    /// <param name="exchangesLoaded"></param>
    public void LoadExchanges(Action<LookupTypeReadModel[]> exchangesLoaded)
          => _referenceQueryModel?.Execute(async model => 
              await model.LoadExchangesAsync((lookupTypes) => {
                  _exchanges = [];
                  if (lookupTypes?.Count > 0)
                  {
                      _exchanges.AddRange(lookupTypes);
                      exchangesLoaded?.Invoke([.. lookupTypes]);
                  }
                  _exchangesLoaded = true;
                  LoadFuturesOptionContracts();
              }));

    /// <summary>
    /// load multipliers
    /// </summary>
    /// <param name="multipliersLoaded"></param>
    public void LoadMultipliers(Action<LookupTypeReadModel[]> multipliersLoaded)
        => _referenceQueryModel?.Execute(async model => 
            await model.LoadMultipliersAsync((lookupTypes) => {
                _multipliers = [];
                if (lookupTypes?.Count > 0)
                {
                    _multipliers.AddRange(lookupTypes);
                    multipliersLoaded?.Invoke([.. lookupTypes]);
                }
                _multipliersLoaded = true;
                LoadFuturesOptionContracts();
            }));

    /// <summary>
    /// load symbols
    /// </summary>
    /// <param name="symbolsLoaded"></param>
    public void LoadSymbols(Action<LookupTypeReadModel[]> symbolsLoaded)
        => _referenceQueryModel?.Execute(async model =>
                await model.LoadSymbolsAsync(lookupTypes => {
                    _symbols = [];
                    if (lookupTypes?.Count > 0)
                    {
                        _symbols.AddRange(lookupTypes);
                        symbolsLoaded?.Invoke([.. lookupTypes]);
                    }
                    _symbolsLoaded = true;
                    LoadFuturesOptionContracts();
                }));

    /// <summary>
    /// load futures option contracts
    /// </summary>
    /// <param name="symbol"></param>
    /// <param name="onContractsLoaded"></param>
    public void LoadFuturesOptionContracts(string symbol, Action<List<FuturesOptionContractReadModel>> onContractsLoaded)
        => _queryModel?.Execute(async ctlr => 
            await ctlr.GetFuturesOptionContractsAsync(symbol, futuresOptionContracts => {
                _futuresOptionContracts = [];
                if (futuresOptionContracts?.Length > 0)
                {
                    _futuresOptionContracts.AddRange(futuresOptionContracts);
                    onContractsLoaded?.Invoke(_futuresOptionContracts);
                }
            }));

    /// <summary>
    /// Loads futures option contracts and triggers the associated event if all lookup types are loaded.
    /// </summary>
    /// <remarks>This method checks whether all required lookup types are loaded. If they are, it invokes the 
    /// <see cref="OnFuturesOptionContractLoaded"/> event to signal that the futures option contracts  have been
    /// successfully loaded.</remarks>
    void LoadFuturesOptionContracts()
    {
        if (AllLookupTypesLoaded())
            OnFuturesOptionContractLoaded.Invoke();
    }

    /// <summary>
    /// Determines whether all lookup types have been successfully loaded.
    /// </summary>
    /// <returns><see langword="true"/> if all lookup types, including symbols, security types, currencies, exchanges,
    /// multipliers, and option types, are loaded; otherwise, <see langword="false"/>.</returns>
    bool AllLookupTypesLoaded()
        => _symbolsLoaded && 
        _securityTypesLoaded && 
        _currenciesLoaded && 
        _exchangesLoaded && 
        _multipliersLoaded && 
        _optionTypesLoaded;
     
    /// <summary>
    /// show status message and execute refresh action
    /// </summary>
    /// <param name="statusMsg"></param>
    /// <param name="refreshAction"></param>
    void RefreshFuturesOptionContracts(string statusMsg, Action refreshAction)
    {
        try
        {
            refreshAction?.Invoke();
            WriteStatusConsole(LogSourceType.MarketData, statusMsg);
        }
        catch (Exception ex)
        {
            WriteStatusConsole(LogSourceType.MarketData, ErrorCode, $"{ex}");
        }
    }

}
