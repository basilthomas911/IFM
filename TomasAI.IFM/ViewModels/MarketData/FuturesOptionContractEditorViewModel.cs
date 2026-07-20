using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TomasAI.IFM.Contracts;
using TomasAI.IFM.Extensions;
using TomasAI.IFM.Models;
using TomasAI.IFM.Shared.Reference.ViewModels;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.MarketData.Events;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Log;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.ViewModels.MarketData
{
    public class FuturesOptionContractEditorViewModel: BaseEditorViewModel
    {
        const int ErrorCode = 9241;

        List<LookupTypeReadModel> _symbols;
        List<LookupTypeReadModel> _securityTypes;
        List<LookupTypeReadModel> _currencies;
        List<LookupTypeReadModel> _exchanges;
        List<LookupTypeReadModel> _multipliers;
        List<LookupTypeReadModel> _optionTypes;
        List<FuturesOptionContractReadModel> _futuresOptionContracts;
        Guid _commandId;
        Action<Guid> _setCommandId;
        ICollection<IEvent> _consumeEvents;
        MarketDataEventModel _eventModel;
        MarketDataCommandModel _commandModel;
        MarketDataQueryModel _queryModel;
        ReferenceQueryModel _referenceQueryModel;

        /// <summary>
        /// futures option contract editor view model
        /// </summary>
        /// <param name="appRoot"></param>
        public FuturesOptionContractEditorViewModel(IAppRoot appRoot) : base(appRoot)
        {
            _commandId = Guid.Empty;
            _eventModel = AppRoot.GetModel<MarketDataEventModel>();
            _commandModel = AppRoot.GetModel<MarketDataCommandModel>();
            _queryModel = AppRoot.GetModel<MarketDataQueryModel>();
            _referenceQueryModel = AppRoot.GetModel<ReferenceQueryModel>();
            _setCommandId = commandId => _commandId = commandId;
            _consumeEvents = new IEvent[]{
                new FuturesOptionContractAddedCompleteEvent { }.SetEventSource($"{EventTopic.MarketDataEvents}"),
                new FuturesOptionContractAddedFailEvent{ }.SetEventSource($"{EventTopic.MarketDataEvents}"),
                new FuturesOptionContractChangedCompleteEvent { }.SetEventSource($"{EventTopic.MarketDataEvents}"),
                new FuturesOptionContractChangedFailEvent{ }.SetEventSource($"{EventTopic.MarketDataEvents}"),
                new FuturesOptionContractRemovedCompleteEvent { }.SetEventSource($"{EventTopic.MarketDataEvents}"),
                new FuturesOptionContractRemovedFailEvent{ }.SetEventSource($"{EventTopic.MarketDataEvents}")
            };
        }

        // public event properties set by editor control...
        public Action<bool> OnDataLoaded;
        public Action<bool> OnAddAction;
        public Action<bool> OnChangeAction;
        public Action OnFuturesOptionContractAdded;
        public Action OnFuturesOptionContractRemoved;
        public Action OnFuturesOptionContractChanged;

        /// <summary>
        /// start listening for futures options data editor events
        /// </summary>
        public void StartListener()
            => _eventModel.Execute(async e => {
                e.OnError((errorCode, errorMsg) => OnError(errorCode, errorMsg));
                await e.StartMarketDataListenerAsync(_consumeEvents, HandleEventAsync);
            });

        /// <summary>
        /// stop listening for futures options editor events
        /// </summary>
        public void StopListener()
            => _eventModel.Execute(async e => {
                e.OnError((errorCode, errorMsg) => OnError(errorCode, errorMsg));
                await e.StopMarketDataListenerAsync();
            });

        /// <summary>
        /// execute futures options editor event actions
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        Task HandleEventAsync(IEvent e)
        {
            if (_commandId != e.CommandId)
                return Task.CompletedTask;
            return e switch
            {
                FuturesOptionContractAddedCompleteEvent o => AddFuturesOptionContractCompleted(o),
                FuturesOptionContractAddedFailEvent o => AddFuturesOptionContractFailed(o),
                FuturesOptionContractChangedCompleteEvent o => ChangeFuturesOptionContractCompleted(o),
                FuturesOptionContractChangedFailEvent o => ChangeFuturesOptionContractFailed(o),
                FuturesOptionContractRemovedCompleteEvent o => RemoveFuturesOptionContractCompleted(o),
                FuturesOptionContractRemovedFailEvent o => RemoveFuturesOptionContractFailed(o),
                _ => Task.CompletedTask
            };
        }

        /// <summary>
        /// add futures option contract
        /// </summary>
        /// <param name="futuresOptionContract">new futures option contract</param>
        public void AddFuturesOptionContract(FuturesOptionContractReadModel futuresOptionContract)
        {
            IsArgumentNull.Check(futuresOptionContract);

             _commandModel.Execute(async model => {
                 model.OnError((errorCode, errorMsg) => OnError(errorCode, errorMsg));
                 await model.AddFuturesOptionContractAsync(futuresOptionContract, _setCommandId);
             });
        }

        Task AddFuturesOptionContractCompleted(FuturesOptionContractAddedCompleteEvent e)
        {
            RefreshFuturesOptionContracts($"Futures Option Contract {e.Contract.ContractId} Added", OnFuturesOptionContractAdded);
            _commandId = Guid.Empty;
            return Task.CompletedTask;
        }

        Task AddFuturesOptionContractFailed(FuturesOptionContractAddedFailEvent e)
        {
            WriteStatusConsole(LogSourceType.MarketData, e.ErrorCode, $"Futures Option Contract Add failed due to: {e.ErrorMessage}");
            _commandId = Guid.Empty;
            return Task.CompletedTask;
        }

        /// <summary>
        /// change futures option contract
        /// </summary>
        /// <param name="originalContractId"></param>
        /// <param name="futuresOptionContract">updated futures option contract</param>
        public void ChangeFuturesOptionContract(string originalContractId, FuturesOptionContractReadModel futuresOptionContract)
        {
            IsArgumentNull.Check(originalContractId);
            IsArgumentNull.Check(futuresOptionContract);

            _commandModel.Execute(async model => {
                model.OnError((errorCode, errorMsg) => OnError(errorCode, errorMsg));
                await model.ChangeFuturesOptionContractAsync(originalContractId, futuresOptionContract,_setCommandId);
            });
        }

        Task ChangeFuturesOptionContractCompleted(FuturesOptionContractChangedCompleteEvent e)
        {
            RefreshFuturesOptionContracts($"Futures Option Contract {e.Contract.ContractId} Changed", OnFuturesOptionContractChanged);
            _commandId = Guid.Empty;
            return Task.CompletedTask;
        }

        Task ChangeFuturesOptionContractFailed(FuturesOptionContractChangedFailEvent e)
        {
            WriteStatusConsole(LogSourceType.MarketData, e.ErrorCode, $"Futures Option Contract Change failed due to: {e.ErrorMessage}");
            _commandId = Guid.Empty;
            return Task.CompletedTask;
        }

        /// <summary>
        /// remove futures contract
        /// </summary>
        /// <param name="contractId"></param>
        public void RemoveFuturesOptionContract(string contractId)
        {
            IsArgumentNull.Check(contractId);

            AppRoot.GetModel<MarketDataCommandModel>()
                .Execute(async model => {
                    model.OnError((errorCode, errorMsg) => OnError(errorCode, errorMsg));
                    await model.RemoveFuturesOptionContractAsync(contractId,_setCommandId);
                });
        }

        Task RemoveFuturesOptionContractCompleted(FuturesOptionContractRemovedCompleteEvent e)
        {
            RefreshFuturesOptionContracts($"Futures Option Contract {e.ContractId} Removed", OnFuturesOptionContractRemoved);
            _commandId = Guid.Empty;
            return Task.CompletedTask;
        }

        Task RemoveFuturesOptionContractFailed(FuturesOptionContractRemovedFailEvent e)
        {
            WriteStatusConsole(LogSourceType.MarketData, e.ErrorCode, $"Futures Option Contract Remove failed due to: {e.ErrorMessage}");
            _commandId = Guid.Empty;
            return Task.CompletedTask;
        }

        public LookupTypeReadModel GetSymbol(int index) => _symbols.GetLookupType(index);
        public LookupTypeReadModel GetOptionType(int index) => _optionTypes.GetLookupType(index);
        public LookupTypeReadModel GetSecurityType(int index) => _securityTypes.GetLookupType(index);
        public LookupTypeReadModel GetCurrency(int index) => _currencies.GetLookupType(index);
        public LookupTypeReadModel GetExchange(int index) => _exchanges.GetLookupType(index);
        public LookupTypeReadModel GetMultiplier(int index) => _multipliers.GetLookupType(index);
        public FuturesOptionContractReadModel GetFuturesOptionContract(int index)
            => _futuresOptionContracts != null && _futuresOptionContracts.Count > index ? _futuresOptionContracts[index] : null;
             
        public int GetOptionTypeIndex(string shortCode) => _optionTypes.GetLookupTypeIndex(shortCode);
        public int GetSecurityTypeIndex(string shortCode) => _securityTypes.GetLookupTypeIndex(shortCode);
        public int GetCurrencyIndex(string shortCode) => _currencies.GetLookupTypeIndex(shortCode);
        public int GetExchangeIndex(string shortCode) => _exchanges.GetLookupTypeIndex(shortCode);
        public int GetMultiplierIndex(string shortCode) => _multipliers.GetLookupTypeIndex(shortCode);

        /// <summary>
        /// load option types
        /// </summary>
        /// <param name="optionTypesLoaded"></param>
        public void LoadOptionTypes(Action<LookupTypeReadModel[]> optionTypesLoaded)
            => _referenceQueryModel.Execute(async model => {
                model.OnError((errorCode, errorMsg) => OnError(errorCode, errorMsg));
                await model.LoadOptionTypesAsync(lookupTypes =>
                {
                    _optionTypes = new List<LookupTypeReadModel>();
                    if ((lookupTypes?.Length ?? 0) > 0)
                    {
                        _optionTypes.AddRange(lookupTypes);
                        optionTypesLoaded?.Invoke(lookupTypes);
                    }
                });
            });

        /// <summary>
        /// return option types
        /// </summary>
        /// <returns></returns>
        public async Task<ICollection<LookupTypeReadModel>> GetOptionTypesAsync()
            => await _referenceQueryModel.ExecuteQuery(async model => {
                model.OnError((errorCode, errorMsg) => OnError(errorCode, errorMsg));
                await model.LoadOptionTypesAsync(lookupTypes => {
                    _optionTypes = default;
                    if ((lookupTypes?.Length ?? 0) > 0)
                        _optionTypes = new List<LookupTypeReadModel>(lookupTypes);
                });
                return _optionTypes as ICollection<LookupTypeReadModel>;
            });
        
        /// <summary>
        /// load security types
        /// </summary>
        /// <param name="securityTypesLoaded"></param>
        public void LoadSecurityTypes(Action<LookupTypeReadModel[]> securityTypesLoaded)
             => _referenceQueryModel.Execute(async model => {
                 model.OnError((errorCode, errorMsg) => OnError(errorCode, errorMsg));
                 await model.LoadSecurityTypesAsync(lookupTypes =>
                 {
                     if ((lookupTypes?.Length ?? 0) > 0)
                     {
                         _securityTypes = new List<LookupTypeReadModel>(lookupTypes);
                         securityTypesLoaded?.Invoke(lookupTypes);
                     }
                 });
             });

        /// <summary>
        /// load currencies
        /// </summary>
        /// <param name="currencyTypesLoaded"></param>
        public void LoadCurrencies(Action<LookupTypeReadModel[]> currencyTypesLoaded)
              => _referenceQueryModel.Execute(async model => 
                  await model.LoadCurrenciesAsync(lookupTypes =>  {
                      _currencies = new List<LookupTypeReadModel>();
                      if ((lookupTypes?.Length ?? 0) > 0)
                      {
                          _currencies.AddRange(lookupTypes);
                          currencyTypesLoaded?.Invoke(lookupTypes);
                      }
                  }));

        /// <summary>
        /// load exchanges
        /// </summary>
        /// <param name="exchangesLoaded"></param>
        public void LoadExchanges(Action<LookupTypeReadModel[]> exchangesLoaded)
              => _referenceQueryModel.Execute(async model => 
                  await model.LoadExchangesAsync((lookupTypes) => {
                      _exchanges = new List<LookupTypeReadModel>();
                      if ((lookupTypes?.Length ?? 0)> 0)
                      {
                          _exchanges.AddRange(lookupTypes);
                          exchangesLoaded?.Invoke(lookupTypes);
                      }
                  }));

        /// <summary>
        /// load multipliers
        /// </summary>
        /// <param name="multipliersLoaded"></param>
        public void LoadMultipliers(Action<LookupTypeReadModel[]> multipliersLoaded)
            => _referenceQueryModel.Execute(async model => 
                await model.LoadMultipliersAsync((lookupTypes) => {
                    _multipliers = new List<LookupTypeReadModel>();
                    if ((lookupTypes?.Length ?? 0)> 0)
                    {
                        _multipliers.AddRange(lookupTypes);
                        multipliersLoaded?.Invoke(lookupTypes);
                    }
                }));

        /// <summary>
        /// load symbols
        /// </summary>
        /// <param name="symbolsLoaded"></param>
        public void LoadSymbols(Action<LookupTypeReadModel[]> symbolsLoaded)
        {
            _symbols ??=  new List<LookupTypeReadModel>();
            if (_symbols.Count == 0)
                _referenceQueryModel.Execute(async model => 
                    await model.LoadSymbolsAsync(lookupTypes =>  {
                        if ((lookupTypes?.Length ?? 0) > 0)
                        {
                            _symbols.AddRange(lookupTypes);
                            symbolsLoaded?.Invoke(lookupTypes);
                        }
                    }));
            else
                symbolsLoaded(_symbols.ToArray());
        }

        /// <summary>
        /// load futures option contracts
        /// </summary>
        /// <param name="symbol"></param>
        /// <param name="onContractsLoaded"></param>
        public void LoadFuturesOptionContracts(string symbol, Action<List<FuturesOptionContractReadModel>> onContractsLoaded)
            => _queryModel.Execute(async ctlr => 
                await ctlr.GetFuturesOptionContractsAsync(symbol, futuresOptionContracts => {
                    _futuresOptionContracts = new List<FuturesOptionContractReadModel>();
                    if ((futuresOptionContracts?.Length ?? 0) > 0)
                    {
                        _futuresOptionContracts.AddRange(futuresOptionContracts);
                        onContractsLoaded?.Invoke(_futuresOptionContracts);
                    }
                }));

        /// <summary>
        /// show status message and execute refresh action
        /// </summary>
        /// <param name="statusMsg"></param>
        /// <param name="refreshAction"></param>
        private void RefreshFuturesOptionContracts(string statusMsg, Action refreshAction)
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
}
