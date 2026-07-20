using System;
using System.Collections.Generic;
using TomasAI.IFM.Contracts;
using TomasAI.IFM.Extensions;
using TomasAI.IFM.Models;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Reference.ViewModels;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.MarketData.Events;
using TomasAI.IFM.Shared.Log;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.ViewModels.MarketData
{
    /// <summary>
    /// futures contract editor view model
    /// </summary>
    public class FuturesContractEditorViewModel : BaseEditorViewModel
    {
        List<LookupTypeReadModel> _symbols;
        List<LookupTypeReadModel> _securityTypes;
        List<LookupTypeReadModel> _currencies;
        List<LookupTypeReadModel> _exchanges;
        List<LookupTypeReadModel> _multipliers;
        List<string> _currentlyTraded;
        List<FuturesContractViewModel> _futuresContracts;
        Dictionary<int, string> _contractMonthMap;
        Guid _commandId;
        Action<Guid> _setCommandId;
        ICollection<IEvent> _consumeEvents;
        MarketDataEventModel _eventModel;
        MarketDataCommandModel _commandModel;
        MarketDataQueryModel _queryModel;
        ReferenceQueryModel _referenceQueryModel;

        public FuturesContractEditorViewModel(IAppRoot appRoot):base(appRoot)
        {
            _eventModel = AppRoot.GetModel<MarketDataEventModel>();
            _commandModel = AppRoot.GetModel<MarketDataCommandModel>();
            _queryModel = AppRoot.GetModel<MarketDataQueryModel>();
            _referenceQueryModel = AppRoot.GetModel<ReferenceQueryModel>();
            _commandId = Guid.Empty;
            _setCommandId = commandId => _commandId = commandId;
            _consumeEvents = new IEvent[]{
                new FuturesContractAddedCompleteEvent { }.SetEventSource($"{EventTopic.MarketDataEvents}"),
                new FuturesContractAddedFailEvent{ }.SetEventSource($"{EventTopic.MarketDataEvents}"),
                new FuturesContractChangedCompleteEvent { }.SetEventSource($"{EventTopic.MarketDataEvents}"),
                new FuturesContractChangedFailEvent{ }.SetEventSource($"{EventTopic.MarketDataEvents}"),
                new FuturesContractRemovedCompleteEvent { }.SetEventSource($"{EventTopic.MarketDataEvents}"),
                new FuturesContractRemovedFailEvent{ }.SetEventSource($"{EventTopic.MarketDataEvents}")
            };
        }

        public ICollection<LookupTypeReadModel> Currencies => _currencies;
        public ICollection<LookupTypeReadModel> SecurityTypes => _securityTypes;
        public ICollection<LookupTypeReadModel> Exchanges => _exchanges;
        public ICollection<LookupTypeReadModel> Multipliers => _multipliers;
        public ICollection<LookupTypeReadModel> Symbols => _symbols;


        public Action<LookupTypeReadModel[]> OnSecurityTypesLoaded;
        public Action<LookupTypeReadModel[]> OnCurrenciesLoaded;
        public Action<LookupTypeReadModel[]> OnExchangesLoaded;
        public Action<LookupTypeReadModel[]> OnMultipliersLoaded;
        public Action<LookupTypeReadModel[]> OnSymbolsLoaded;
        public Action<string[]> OnCurrentlyTraded;
        public Action OnFuturesContractAdded;
        public Action OnFuturesContractChanged;
        public Action OnFuturesContractRemoved;
        public Action OnFuturesContractIdsLoaded;
        public Action<FuturesContractViewModel[]> OnFuturesContractsLoaded;
    
        public void StartListener()
            => _eventModel.Execute(async e => {
                    e.OnError((errorCode, errorMsg) => OnError(errorCode, errorMsg));
                    await e.StartMarketDataListenerAsync( _consumeEvents, HandleEventAsync );
                });

        public void StopListener()
            => _eventModel.Execute(async e => {
                    e.OnError((errorCode, errorMsg) => OnError(errorCode, errorMsg));
                    await e.StopMarketDataListenerAsync();
                });

        Task HandleEventAsync(IEvent e)
        {
            if (_commandId != e.CommandId)
                return Task.CompletedTask;
            return e switch {
                FuturesContractAddedCompleteEvent o => AddFuturesContractCompleted(o),
                FuturesContractAddedFailEvent o => AddFuturesContractFailed(o),
                FuturesContractChangedCompleteEvent o => ChangeFuturesContractCompleted(o),
                FuturesContractChangedFailEvent o => ChangeFuturesContractFailed(o),
                FuturesContractRemovedCompleteEvent o => RemoveFuturesContractCompleted(o),
                FuturesContractRemovedFailEvent o => RemoveFuturesContractFailed(o),
                _ => Task.CompletedTask
            };
        }

        /// <summary>
        /// add futures contract
        /// </summary>
        /// <param name="futuresContract"></param>
        public void AddFuturesContract(FuturesContractViewModel futuresContract)
        {
            IsArgumentNull.Check(futuresContract);
            _commandModel.Execute(async model => {
                model.OnError((errorCode, errorMsg) => OnError(errorCode, errorMsg));
                await model.AddFuturesContractAsync(futuresContract, _setCommandId);
           });
        }

        Task AddFuturesContractCompleted(FuturesContractAddedCompleteEvent e)
        {
            RefreshAddedFuturesContractIds($"Futures Contract {e.Contract.ContractId} Added");
            _commandId = Guid.Empty;
            return Task.CompletedTask;
        }

        Task AddFuturesContractFailed(FuturesContractAddedFailEvent e)
        {
            WriteStatusConsole(LogSourceType.MarketData, e.ErrorCode, $"Futures Contract Add failed due to: {e.ErrorMessage}");
            _commandId = Guid.Empty;
            return Task.CompletedTask;
        }

        /// <summary>
        /// change futures contract
        /// </summary>
        /// <param name="originalContractId"></param>
        /// <param name="futuresContract"></param>
        public void ChangeFuturesContract(string originalContractId, FuturesContractViewModel futuresContract)
        {
            IsArgumentNull.Check(originalContractId);
            IsArgumentNull.Check(futuresContract);
            _commandModel.Execute(async model => {
                model.OnError((errorCode, errorMsg) => OnError(errorCode, errorMsg));
                await model.ChangeFuturesContractAsync(originalContractId, futuresContract,_setCommandId);
            });
        }

        Task ChangeFuturesContractCompleted(FuturesContractChangedCompleteEvent e)
        {
            RefreshChangedFuturesContractIds($"Futures Contract {e.Contract.ContractId} Changed");
            _commandId = Guid.Empty;
            return Task.CompletedTask;
        }

        Task ChangeFuturesContractFailed(FuturesContractChangedFailEvent e)
        {
            WriteStatusConsole(LogSourceType.MarketData, e.ErrorCode, $"Futures Contract Change failed due to: {e.ErrorMessage}");
            _commandId = Guid.Empty;
            return Task.CompletedTask;
        }

        /// <summary>
        /// remove futures contract
        /// </summary>
        /// <param name="contractId"></param>
        public void RemoveFuturesContract(string contractId)
        {
            IsArgumentNull.Check(contractId);
            _commandModel.Execute(async model => {
                model.OnError((errorCode, errorMsg) => OnError(errorCode, errorMsg));
                await model.RemoveFuturesContractAsync(contractId, _setCommandId);
            });
        }

        Task RemoveFuturesContractCompleted(FuturesContractRemovedCompleteEvent e)
        {
            RefreshRemovedFuturesContractIds($"Futures Contract {e.ContractId} Removed");
            _commandId = Guid.Empty;
            return Task.CompletedTask;
        }

        Task RemoveFuturesContractFailed(FuturesContractRemovedFailEvent e)
        {
            WriteStatusConsole(LogSourceType.MarketData, e.ErrorCode, $"Futures Contract Remove failed due to: {e.ErrorMessage}");
            _commandId = Guid.Empty;
            return Task.CompletedTask;
        }

        public string GetCurrency(int index) => _currencies.GetLookupType(index).ShortCode;
        public string GetSecurityType(int index) => _securityTypes.GetLookupType(index).ShortCode;
        public string GetExchange(int index) => _exchanges.GetLookupType(index).ShortCode;
        public string GetMultiplier(int index) => _multipliers.GetLookupType(index).ShortCode;
        public string GetSymbol(int index) => _symbols.GetLookupType(index).ShortCode;
        public string GetSymbolDescription(int index) => _symbols.GetLookupType(index).Description;
        public FuturesContractViewModel GetFuturesContract(int index) => _futuresContracts?.Count > 0 ? _futuresContracts[index] : default(FuturesContractViewModel);
        public string GetContractMonth(int monthIndex) => _contractMonthMap[monthIndex];

        /// <summary>
        /// return true if all lookyp types have been loaded
        /// </summary>
        /// <returns></returns>
        public bool AllLookupTypesLoaded()
            => (_currencies?.Count ?? 0) > 0 &&
                (_securityTypes?.Count ?? 0) > 0 &&
                (_exchanges?.Count ?? 0) > 0 &&
                (_multipliers?.Count ?? 0) > 0 &&
                (_symbols?.Count ?? 0) >  0 &&
                (_contractMonthMap?.Count ?? 0) > 0 ;
            
        /// <summary>
        /// load security types
        /// </summary>
        public void LoadSecurityTypes()
            => _referenceQueryModel.Execute(async model =>
                await model.LoadSecurityTypesAsync(lookupTypes =>
                {
                    _securityTypes = new List<LookupTypeReadModel>();
                    if ((lookupTypes?.Length ?? 0) > 0)
                    {
                        _securityTypes.AddRange(lookupTypes);
                        OnSecurityTypesLoaded?.Invoke(lookupTypes);
                    }
                }));

        /// <summary>
        /// load currencies
        /// </summary>
        public void LoadCurrencies()
            => _referenceQueryModel.Execute(async model =>
                await model.LoadCurrenciesAsync(lookupTypes =>
                {
                    _currencies = new List<LookupTypeReadModel>();
                    if ((lookupTypes?.Length ?? 0) > 0)
                    {
                        _currencies.AddRange(lookupTypes);
                        OnCurrenciesLoaded?.Invoke(lookupTypes);
                    }
                }));

        /// <summary>
        /// load exchanges
        /// </summary>
        public void LoadExchanges()
            => _referenceQueryModel.Execute(async model =>
                await model.LoadExchangesAsync(lookupTypes =>
                {
                    _exchanges = new List<LookupTypeReadModel>();
                    if ((lookupTypes?.Length ?? 0 ) > 0)
                    {
                        _exchanges.AddRange(lookupTypes);
                        OnExchangesLoaded?.Invoke(lookupTypes);
                    }
                }));

        /// <summary>
        /// load multipliers
        /// </summary>
        public void LoadMultipliers()
            => _referenceQueryModel.Execute(async model =>
                await model.LoadMultipliersAsync((lookupTypes) =>
                {
                    _multipliers = new List<LookupTypeReadModel>();
                    if ((lookupTypes?.Length ?? 0) > 0)
                    {
                        _multipliers.AddRange(lookupTypes);
                        OnMultipliersLoaded?.Invoke(lookupTypes);
                    }
                }));

        /// <summary>
        /// load currently traded futures indicator
        /// </summary>
        public void LoadCurrentlyTraded()
        {
            _currentlyTraded = new List<string> { "Yes", "No" };
            OnCurrentlyTraded?.Invoke(_currentlyTraded.ToArray());
        }

        /// <summary>
        /// load contract month symbols
        /// </summary>
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
        /// ;oad security symbols
        /// </summary>
        public void LoadSymbols()
            => _referenceQueryModel.Execute(async model =>
                    await model.LoadSymbolsAsync(lookupTypes =>
                    {
                        _symbols = new List<LookupTypeReadModel>();
                        if ((lookupTypes?.Length ?? 0) > 0)
                        {
                            _symbols.AddRange(lookupTypes);
                            OnSymbolsLoaded?.Invoke(lookupTypes);
                        }
                    }));

        /// <summary>
        /// load futures contract ids
        /// </summary>
        /// <param name="contractId"></param>
        public void LoadFuturesContractIds(string contractId = null)
            => _queryModel.Execute(async model =>  {
                await model.GetFuturesContractsAsync( futuresContracts =>
                {
                    _futuresContracts = new List<FuturesContractViewModel>();
                    if ((futuresContracts?.Length ?? 0) > 0)
                    {
                        _futuresContracts.AddRange(futuresContracts);
                        OnFuturesContractIdsLoaded?.Invoke();
                    }
                });
            });

        /// <summary>
        /// load futures contract
        /// </summary>
        public void LoadFuturesContracts()
           => _queryModel.Execute(async model => {
               await model.GetFuturesContractsAsync(futuresContracts =>
               {
                   _futuresContracts = new List<FuturesContractViewModel>();
                   if ((futuresContracts?.Length ?? 0) > 0)
                   {
                       _futuresContracts.AddRange(futuresContracts);
                       OnFuturesContractsLoaded?.Invoke(_futuresContracts.ToArray());
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
        void UpdateFuturesContracts(FuturesContractViewModel[] futuresContracts)
        {
            _futuresContracts = new List<FuturesContractViewModel>();
            if ((futuresContracts?.Length ?? 0) > 0)
                _futuresContracts.AddRange(futuresContracts);
        }

    }
}
