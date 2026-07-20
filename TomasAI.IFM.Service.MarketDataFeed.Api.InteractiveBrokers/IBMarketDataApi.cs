using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using IBApi;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Shared.Log.ServiceApi;
using TomasAI.IFM.Shared.Log.ViewModels;
using TomasAI.IFM.Shared.Log.Events;
using TomasAI.IFM.Shared.Log;
using TomasAI.IFM.Shared.OptionPricer;

namespace TomasAI.IFM.Service.MarketDataFeed.Api.InteractiveBrokers
{
    public class IBMarketDataSnapshotApi : IBMarketDataApi, IMarketDataSnapshotApi
    {
        public IBMarketDataSnapshotApi(IMarketDataSnapshotApiOptions snapshotOptions, IStatusConsoleEventProducer statusConsoleLog) :base(snapshotOptions, statusConsoleLog)
        {
        }
    }

    public class IBMarketDataApi : IMarketDataApi
    {
        private static object _stmLock = new object();
        private static Dictionary<int, Action<TickBidAskMessage>> _stmOptionTickData;
        private static Dictionary<string, FuturesTickDataViewModel> _stmFuturesTickDataMap;
        private static Dictionary<string, string> _statusMsgMap;
        private IMarketDataApiOptions _options;
        private IStatusConsoleEventProducer _statusConsoleLog;
        private IBClient _ibApi;
        private Dictionary<DateTime, string> _errorMessages;
        private Action<int, string> _errorMessageAction;
        private IStreamIdCollection _streamIds;
  
        public IBMarketDataApi(IMarketDataApiOptions options, IStatusConsoleEventProducer statusConsoleLog)
        {
            _options = options;
            _statusConsoleLog = statusConsoleLog;
            _ibApi = new IBClient();
            InitMarketDataApi();
        }

        public IStreamIdCollection StreamIds => _streamIds;

        private void InitMarketDataApi()
        {
            _stmOptionTickData = new Dictionary<int, Action<TickBidAskMessage>>();
            _stmFuturesTickDataMap = new Dictionary<string, FuturesTickDataViewModel>();
            _errorMessages = new Dictionary<DateTime, string>();
            _streamIds = new StreamIdCollection();
            _statusMsgMap = new Dictionary<string, string>();
        }

        /// <summary>
        /// start interactive brokers client
        /// </summary>
        public void Start(Action<int, string> errorMessageAction = null)
        {
            InitMarketDataApi();
            _errorMessageAction = errorMessageAction;
            for (var retryCount = 0; retryCount < 3; retryCount++)
            {
                _ibApi.Start(_options.Host, _options.Port, _options.ClientId);
                Task.Delay(TimeSpan.FromSeconds(2)).Wait();
                if (_ibApi.Started)
                    break;
            }
        }

        /// <summary>
        /// stop interactive brokers client
        /// </summary>
        public void Stop() => _ibApi.Stop();

        /// <summary>
        /// return futures price from futures contract
        /// </summary>
        /// <param name="contract"></param>
        /// <returns></returns>
        public async Task<FuturesTickDataViewModel> GetFuturesPriceAsync(int requestId, FuturesContractViewModel contract)
        {
            // set return data action...
            var futuresTickData = default(FuturesTickDataViewModel);
            var tickDataAction = default(ActionBlock<FuturesTickDataViewModel>);
            tickDataAction = new ActionBlock<FuturesTickDataViewModel>(
                (e) => {
                    futuresTickData = e;
                    tickDataAction.Complete();
                }, 
                new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = 1 });

            // handle error event...
            _ibApi.AddErrorHandler(requestId, (id, errorCode, errorMsg, ex) => {
                WriteStatusConsole(errorCode, errorMsg);
                tickDataAction.Complete();
            });

            _ibApi.AddStreamingFuturesTickData(requestId, e =>
                tickDataAction.Post( new FuturesTickDataViewModel (
                    contractId: contract.ContractId,
                    tickDate: e.TickPriceData.TickDate,
                    tickTime: e.TickPriceData.TickTime,
                    price: e.TickPriceData.Price,
                    size: e.TickPriceData.Size
                ))
            );

            // request last tick price from ib client...
            _ibApi.ClientSocket.reqTickByTickData(
                requestId, 
                new Contract { SecType = contract.SecurityType, Symbol = contract.Symbol, LocalSymbol = contract.LocalSymbol, Currency = contract.Currency, Exchange = contract.Exchange }, 
                "Last");

            // wait until ib client has finished processing...
            await tickDataAction.Completion;

            // close last tick price request...
            _ibApi.CancelTickLastPrice(requestId);
            _ibApi.RemoveErrorHandler(requestId);
            return futuresTickData;
        }

        /// <summary>
        /// return selected futures contract
        /// </summary>
        /// <param name="symbol"></param>
        /// <param name="localSymbol"></param>
        /// <param name="secType"></param>
        /// <param name="currency"></param>
        /// <param name="exchange"></param>
        /// <param name="description"></param>
        /// <returns></returns>
        public async Task<FuturesContractViewModel> GetFuturesContractAsync(int contractRequestId, FuturesContractViewModel qfContract)
        {
            var futuresContract = default(FuturesContractViewModel);
            var contractAction = new ActionBlock<Contract>(contract =>
            {
                futuresContract = new FuturesContractViewModel(
                       contractId: $"{contract.Symbol}{contract.LastTradeDateOrContractMonth.Substring(0, 8)}",
                       description: $"{qfContract.Description} {contract.SecType} {contract.LastTradeDateOrContractMonth.Substring(0, 8)}",
                       symbol: contract.Symbol,
                       securityType: contract.SecType,
                       currency: contract.Currency,
                       exchange: contract.Exchange,
                       multiplier: contract.Multiplier,
                       lastTradeDate: qfContract.LastTradeDate,
                       localSymbol: contract.LocalSymbol,
                       currentlyTraded: qfContract.CurrentlyTraded);
            }, 
            new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = 1
            });

            // handle error event...
            _ibApi.AddErrorHandler(contractRequestId, (id, errorCode, errorMsg, ex) => {
                WriteStatusConsole(errorCode, errorMsg);
                contractAction.Complete();
            });

            // handle contract details event...
            _ibApi.OnContractDetails += (e) =>
            {
                if (e.RequestId == contractRequestId)
                    contractAction.Post(e.ContractDetails.Summary);
            };

            // handle contract details end event...
            _ibApi.OnContractDetailsEnd += (requestId) =>
            {
                if (requestId == contractRequestId)
                    contractAction.Complete();
            };

            // request contract details from ib client...
            _ibApi.ClientSocket.reqContractDetails(contractRequestId, new Contract {
                SecType = qfContract.SecurityType,
                Symbol = qfContract.Symbol,
                LocalSymbol = qfContract.LocalSymbol,
                Currency = qfContract.Currency,
                Exchange = qfContract.Exchange });

            // wait until ib client has finished processing...
            await contractAction.Completion;
            _ibApi.RemoveErrorHandler(contractRequestId);
            return futuresContract;    
        }

        public async Task <(FuturesOptionContractReadModel shortContract, FuturesOptionContractReadModel longContract)> GetFuturesOptionSpreadAsync(
            FuturesOptionContractReadModel qfShortContract,
            FuturesOptionContractReadModel qfLongContract)
        {
            var shortOptionContract = await GetFuturesOptionContractAsync(RequestID.ShortOption, qfShortContract);
            var longOptionContract = await GetFuturesOptionContractAsync(RequestID.LongOption, qfLongContract);
            return (shortOptionContract, longOptionContract);
        }

        public async Task<FuturesOptionContractReadModel> GetFuturesOptionContractAsync(
            int optionRequestId,
            FuturesOptionContractReadModel qfContract)
        {
            var futuresOptionContract = default(FuturesOptionContractReadModel);
            var contractAction = new ActionBlock<Contract>(contract =>
            {
                var optionContractMonth = new DateTime(
                    year: Convert.ToInt32(contract.LastTradeDateOrContractMonth.Substring(0, 4)),
                    month: Convert.ToInt32(contract.LastTradeDateOrContractMonth.Substring(4, 2)),
                    day: Convert.ToInt32(contract.LastTradeDateOrContractMonth.Substring(6, 2)));
                contract.LastTradeDateOrContractMonth.Substring(0, 8);
                futuresOptionContract = new FuturesOptionContractReadModel (
                        contractId: qfContract.ContractId,
                        symbol: contract.Symbol,
                        localSymbol: contract.LocalSymbol,
                        securityType: contract.SecType,
                        currency: contract.Currency,
                        exchange: contract.Exchange,
                        multiplier: contract.Multiplier,
                        contractMonth: optionContractMonth,
                        optionType: qfContract.OptionType,
                        strikePrice: contract.Strike,
                        description: $"{qfContract.Description} {optionContractMonth}{contract.Right.Substring(0, 1)}{contract.Strike.ToString("0000")}"
                );
            },
            new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = 1
            });

            // handle error event...
            _ibApi.AddErrorHandler(optionRequestId, (id, errorCode, errorMsg, ex) => {
                WriteStatusConsole(errorCode, errorMsg);
                contractAction.Complete();
            });

            // handle contract details event...
            _ibApi.OnContractDetails += (e) =>
            {
                if (e.RequestId == optionRequestId)
                    contractAction.Post(e.ContractDetails.Summary);
            };

            // handle contract details end event...
            _ibApi.OnContractDetailsEnd += (requestId) =>
            {
                if (requestId == optionRequestId)
                    contractAction.Complete();
            };

            // request futures option contract details from ib client...
            _ibApi.ClientSocket.reqContractDetails(optionRequestId, new Contract {
                SecType = qfContract.SecurityType,
                Symbol = qfContract.Symbol,
                LocalSymbol = $"{qfContract.LocalSymbol} {qfContract.OptionType.Substring(0, 1)}{qfContract.StrikePrice.ToString("0000")}",
                Currency = qfContract.Currency,
                Exchange = qfContract.Exchange,
                Multiplier = qfContract.Multiplier,
                LastTradeDateOrContractMonth = qfContract.ContractMonth.ToString("yyyyMMdd"),
                Right = qfContract.OptionType,
                Strike = qfContract.StrikePrice
            });

            // wait until ib client has finished processing...
            await contractAction.Completion;
            _ibApi.RemoveErrorHandler(optionRequestId);
            return futuresOptionContract;
        }

        /// <summary>
        /// return futures option price
        /// </summary>
        /// <param name="requestId">requets id</param>
        /// <param name="contract">futures option contract</param>
        /// <returns></returns>
        public async Task GetFuturesOptionPriceAsync(int requestId, FuturesOptionContractReadModel contract, Action<FuturesOptionTickDataViewModel> optionTickData)
        {
            var tickDataAction = default(ActionBlock<FuturesOptionTickDataViewModel>);
            tickDataAction = new ActionBlock<FuturesOptionTickDataViewModel>(e => 
            {
                optionTickData(e);
                tickDataAction.Complete();
            },
            new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = 1 });

            // handle error event...
            _ibApi.AddErrorHandler(requestId, (id, errorCode, errorMsg, ex) => {
                WriteStatusConsole(errorCode, errorMsg);
                tickDataAction.Complete();
            });

            _ibApi.AddStreamingFuturesOptionTickData(requestId, contract, (e,oc) =>
                tickDataAction.Post(new FuturesOptionTickDataViewModel
                (
                    contractId: oc.ContractId,
                    tickDate: e.TickBidAskData.TickDate,
                    tickTime: e.TickBidAskData.TickTime,
                    optionPrice: (e.TickBidAskData.BidPrice + e.TickBidAskData.AskPrice) / 2,
                    bidPrice: e.TickBidAskData.BidPrice,
                    askPrice: e.TickBidAskData.AskPrice,
                    bidSize: e.TickBidAskData.BidSize,
                    askSize: e.TickBidAskData.AskSize,
                    impliedVolatility: 0,
                    underlyingPrice: 0,
                    delta: 0,
                    gamma: 0,
                    vega: 0,
                    theta: 0,
                    rho: 0
                ))
            );

            // request bid ask tick price from ib client...
            _ibApi.ClientSocket.reqTickByTickData(
                requestId,
                new Contract {
                    SecType = contract.SecurityType,
                    Symbol = contract.Symbol,
                    LocalSymbol = contract.LocalSymbol,
                    Currency = contract.Currency,
                    Exchange = contract.Exchange,
                    Right = contract.OptionType,
                    Strike = contract.StrikePrice,
                    LastTradeDateOrContractMonth = contract.ContractMonth.ToString("yyyyMMdd"),
                    Multiplier = contract.Multiplier
                },
                "BidAsk");

            // wait max of 5 seconds until ib client has finished processing...
            await Task.Run(() =>
            {
                tickDataAction.Completion.Wait(TimeSpan.FromSeconds(10));
                _ibApi.CancelTickBidAskPrice(requestId);
                _ibApi.RemoveErrorHandler(requestId);
            });
        }

        /// <summary>
        /// return option greeks
        /// </summary>
        /// <param name="requestId">request id</param>
        /// <param name="contract">futures option contract</param>
        /// <param name="optionPrice">futures option price</param>
        /// <param name="futuresPrice">futures price</param>
        /// <returns></returns>
        public TickOptionComputation GetFuturesOptionGreeks(DateTime valueDate, DateTime maturityDate, FuturesOptionContractReadModel contract, double optionValue, double futuresPrice, double riskFreeRate)
        {
            var optionCalculator = new OptionCalculator(valueDate, maturityDate);
            var optionGreeks = optionCalculator.GetOptionGreeks(contract.OptionType, futuresPrice, contract.StrikePrice, optionValue, riskFreeRate);
            if (!optionGreeks.Success) return null;

            return new TickOptionComputation(string.Empty,
                impliedVol: optionGreeks.ImpliedVolatility,
                delta: optionGreeks.Delta,
                optPrice: optionValue,
                pvDividend: 0,
                gamma: optionGreeks.Gamma,
                theta: optionGreeks.Theta,
                vega: optionGreeks.Vega,
                rho: optionGreeks.Rho,
                undPrice: futuresPrice);
        }

        public void StartStreamingFuturesTickData(
            int requestId, 
            DateTime valueDate,
            FuturesContractViewModel contract,
            Action<FuturesTickDataViewModel> onFuturesTickData)
        {
            _ibApi.AddErrorHandler(requestId, (id, errorCode, errorMsg, ex) => WriteStatusConsole(errorCode, errorMsg));

            // handle streaming futures tick data update event...
            _ibApi.AddStreamingFuturesTickData(requestId, e => OnStreamingFuturesTickData(e, valueDate, requestId, contract,  onFuturesTickData));

            // request last tick price from ib client...
            _ibApi.ClientSocket.reqTickByTickData(
                requestId,
                new Contract { SecType = contract.SecurityType, Symbol = contract.Symbol, LocalSymbol = contract.LocalSymbol, Currency = contract.Currency, Exchange = contract.Exchange },
                "AllLast");
        }

        private void OnStreamingFuturesTickData(
            TickPriceMessage e, 
            DateTime valueDate,
            int requestId, 
            FuturesContractViewModel contract, 
            Action<FuturesTickDataViewModel> onFuturesTickData)
        {
            try
            {
                if (e != null && e.RequestId == requestId)
                {
                    //_watchDogTimer.Change(TimeSpan.FromMinutes(5), Timeout.InfiniteTimeSpan);
                    var futuresTickData = new FuturesTickDataViewModel (
                        valueDate: valueDate,
                        contractId: contract.ContractId,
                        tickDate: e.TickPriceData.TickDate,
                        tickTime: e.TickPriceData.TickTime,
                        price: e.TickPriceData.Price,
                        size: e.TickPriceData.Size
                    );
                    lock (_stmFuturesTickDataMap)
                    {
                        _stmFuturesTickDataMap.Clear();
                        _stmFuturesTickDataMap.Add(contract.ContractId, futuresTickData);
                    }
                    onFuturesTickData(futuresTickData);
                    if (_statusMsgMap.Count > 0) _statusMsgMap.Clear();
                }
            }
            catch(Exception ex)
            {
                WriteStatusConsole(-1002, ex.Message);
            }
        }

        public bool StopStreamingFuturesTickData(int requestId)
        {
            var stoppedStreaming = false;
            if (_ibApi.Started)
            {
                _ibApi.CancelTickLastPrice(requestId);
                stoppedStreaming = true;
            }
            return stoppedStreaming;
        }

        public void StartStreamingFuturesOptionTickData(
            int optionRequestId,
            DateTime valueDate,
            DateTime maturityDate,
            FuturesOptionContractReadModel contract,
            double riskFreeRate,
            Action<FuturesOptionTickDataViewModel> onFuturesOptionTickData)
        {
            _ibApi.AddErrorHandler(optionRequestId, (id, errorCode, errorMsg, ex) => WriteStatusConsole(errorCode, errorMsg));

            // handle contract details event...
            var daysToExpiry = (maturityDate - valueDate).Days;
            var optionCalculator = new OptionCalculator(valueDate, maturityDate);
            var messageQueueReader = new MarketDataMessageQueueReader<FuturesOptionTickDataViewModel>();
            _ibApi.AddStreamingFuturesOptionTickData(optionRequestId, contract, (e, optionContract) => OnStreamingFuturesOptionTickData(e, messageQueueReader, optionCalculator, optionContract, daysToExpiry, riskFreeRate, onFuturesOptionTickData));

            // request last tick price from ib client...
            _ibApi.ClientSocket.reqTickByTickData(
                optionRequestId,
                new Contract {
                    SecType = contract.SecurityType,
                    Symbol = contract.Symbol,
                    LocalSymbol = contract.LocalSymbol,
                    Currency = contract.Currency,
                    Exchange = contract.Exchange,
                    Right = contract.OptionType,
                    Strike = contract.StrikePrice,
                    LastTradeDateOrContractMonth = contract.ContractMonth.ToString("yyyyMMdd"),
                    Multiplier = contract.Multiplier
                },
                "BidAsk");
        }

        private void OnStreamingFuturesOptionTickData(TickBidAskMessage e, 
            MarketDataMessageQueueReader<FuturesOptionTickDataViewModel> messageQueueReader,
            OptionCalculator optionCalculator,
            FuturesOptionContractReadModel contract,
            int daysToExpiry,
            double riskFreeRate,
            Action<FuturesOptionTickDataViewModel> onFuturesOptionTickData)
        {
            try
            {
                if (e == null || e.TickBidAskData.AskPrice == 0.0 || e.TickBidAskData.BidPrice == 0.0)
                    return;
                var futuresOptionTickData = GetFuturesOptionTickData(e.TickBidAskData);
                if (futuresOptionTickData == null)
                    return;
                onFuturesOptionTickData(futuresOptionTickData);
                if (_statusMsgMap.Count > 0)
                    _statusMsgMap.Clear();
            }
            catch(Exception ex)
            {
            }
            return;

            FuturesOptionTickDataViewModel GetFuturesOptionTickData(TickBidAskData o)
            {
                var futuresPrice = GetFuturesPrice();
                if (futuresPrice == -1.00) return null;
                var optionValue = (o.BidPrice + o.AskPrice) / 2;
                var optionGreeks = optionCalculator.GetOptionGreeks(contract.OptionType, futuresPrice, contract.StrikePrice, optionValue, riskFreeRate);
                if (!optionGreeks.Success) return null;
                var timeValue = daysToExpiry / 365.0;
                var otd = new FuturesOptionTickDataViewModel(
                    contract.ContractId,
                    o.TickDate,
                    o.TickTime,
                    optionValue,
                    o.BidPrice,
                    o.AskPrice,
                    o.BidSize,
                    o.AskSize,
                    optionGreeks.ImpliedVolatility,
                    futuresPrice);
                return new FuturesOptionTickDataViewModel
                (
                    contractId: otd.ContractId,
                    tickDate: otd.TickDate,
                    tickTime: otd.TickTime,
                    optionPrice: otd.OptionPrice,
                    bidPrice: otd.BidPrice,
                    askPrice: otd.AskPrice,
                    bidSize: otd.BidSize,
                    askSize: otd.AskSize,
                    impliedVolatility: otd.ImpliedVolatility,
                    underlyingPrice: otd.UnderlyingPrice,
                    delta: optionGreeks.Delta,
                    gamma: optionGreeks.Gamma,
                    vega: optionGreeks.Vega,
                    theta: optionGreeks.Theta,
                    rho: optionGreeks.Rho
                );
            }

            double GetFuturesPrice()
            {
                var futuresPrice = -1.0;
                lock (_stmFuturesTickDataMap)
                {
                    if (_stmFuturesTickDataMap.Count != 0)
                    {
                        try { futuresPrice = _stmFuturesTickDataMap.Values.ElementAt(0).Price; }
                        catch { futuresPrice = -1.0; }
                    }
                }
                return futuresPrice;
            }

        }

        public void StopStreamingFuturesOptionTickData(int requestId)
        {
            if (_ibApi.Started)
                _ibApi.CancelTickBidAskPrice(requestId);
        }
        
        private void WriteStatusConsole(int errorCode, string errorMsg)
        {
            if (_statusMsgMap.ContainsKey(errorMsg)) return;
            _statusMsgMap.Add(errorMsg, errorMsg);
            var isError = errorMsg != null && !errorMsg.Contains("OK");
            var log = new StatusConsoleLogReadModel
            {
                StatusDate = DateTime.Now,
                StatusCode = isError ? errorCode : 0,
                StatusCodeType = isError ? StatusCodeType.Error : StatusCodeType.Ok,
                Message = errorMsg ?? string.Empty,
                Source = LogSourceType.TWSMarketDataApi
            };
            _statusConsoleLog?.PostEventAsync(new StatusConsoleLoggedEvent {
                CommandId = Guid.NewGuid(),
                StatusConsoleLog = log
            });
        }
    }
}
