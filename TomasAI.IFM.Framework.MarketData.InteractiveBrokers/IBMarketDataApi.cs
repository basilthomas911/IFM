using System.Threading.Tasks.Dataflow;
using IBApi;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed.ServiceApi;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.Shared.OptionPricer;
using TomasAI.IFM.Framework.MarketData.MarketDataApi;

namespace TomasAI.IFM.Framework.MarketData.InteractiveBrokers;

public class IBMarketDataSnapshotApi(MarketDataApi.IMarketDataSnapshotApiOptions snapshotOptions, IStatusConsoleWriter statusConsoleWriter, IMarketDataFeedEventProducer marketDataFeedEventProducer)
    : IBMarketDataApi(snapshotOptions, statusConsoleWriter, marketDataFeedEventProducer), MarketDataApi.IMarketDataSnapshotApi
{
}

public class IBMarketDataApi : MarketDataApi.IMarketDataApi
{
    static Dictionary<string, FuturesTickDataV2ReadModel>? _stmFuturesTickDataMap;
    static Dictionary<string, string>? _statusMsgMap;
    MarketDataApi.IMarketDataApiOptions _options;
    IStatusConsoleWriter _statusConsoleWriter;
    IBClient _ibApi;
    Action<Guid, int, string>? _errorhandler;
    IRequestIdCollection _requestIds;

    public IBMarketDataApi(MarketDataApi.IMarketDataApiOptions options, IStatusConsoleWriter statusConsoleWriter, IMarketDataFeedEventProducer marketDataFeedEventProducer)
    {
        _options = options;
        _statusConsoleWriter = statusConsoleWriter;
        _ibApi = new IBClient(marketDataFeedEventProducer);
        _requestIds = new RequestIdCollection();
        InitMarketDataApi();
    }

    void InitMarketDataApi()
    {
        _stmFuturesTickDataMap = new();
        _statusMsgMap = new();
        _requestIds = new RequestIdCollection();
    }

    /// <summary>
    /// start interactive brokers client
    /// </summary>
    public void Start(Guid commandId, Action<Guid, int, string>? errorHandler)
    {
        InitMarketDataApi();
        _errorhandler = errorHandler;
        for (var retryCount = 0; retryCount < 3; retryCount++)
        {
            _ibApi.Start(_options.Host, _options.Port, _options.ClientId);
            Task.Delay(TimeSpan.FromSeconds(2)).Wait();
            if (_ibApi.Started)
                return;
        }
    }

    /// <summary>
    /// stop interactive brokers client
    /// </summary>
    public void Stop(Guid commandId) => _ibApi.Stop();

    /// <summary>
    /// return futures price from futures contract
    /// </summary>
    /// <param name="contract"></param>
    /// <returns></returns>
    public async Task<FuturesTickDataV2ReadModel?> GetFuturesPriceAsync(Guid commandId, FuturesContractV2ReadModel contract)
    {
        // set return data action...
        var futuresTickData = default(FuturesTickDataV2ReadModel);
        var tickDataAction = default(ActionBlock<FuturesTickDataV2ReadModel>);
        tickDataAction = new ActionBlock<FuturesTickDataV2ReadModel>(
            e => {
                futuresTickData = e;
                tickDataAction?.Complete();
            }, 
            new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = 1 });

        // handle error event...
        var requestId = commandId.GetHashCode();    
        _ibApi.AddErrorHandler(requestId, (id, errorCode, errorMsg, ex) => {
            WriteStatusConsole(commandId, errorCode, errorMsg);
            tickDataAction.Complete();
        });

        _ibApi.AddStreamingFuturesTickData(requestId, e =>
            tickDataAction.Post( new FuturesTickDataV2ReadModel (
                valueDate: DateOnly.MinValue,
                contractId: contract.ContractId,
                tickId: e.TickPriceData.TickTime,
                tickTime: TimeOnly.FromDateTime( DateTime.Now),
                price: Convert.ToDecimal(e.TickPriceData.Price),
                size: e.TickPriceData.Size
            ))
        );

        // request last tick price from ib client...
        _ibApi.ClientSocket?.reqTickByTickData(
            requestId, 
            new Contract { SecType = contract.SecurityType, Symbol = contract.Symbol, LocalSymbol = contract.LocalSymbol, Currency = contract.Currency, Exchange = contract.Exchange }, 
            "Last",
            0, true);

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
    /// <param name="commandId"></param>
    /// <param name="qfContract"></param>
    /// <returns></returns>
    public async Task<FuturesContractV2ReadModel?> GetFuturesContractAsync(
        Guid commandId, 
        FuturesContractV2ReadModel qfContract)
    {
        var futuresContract = default(FuturesContractV2ReadModel);
        var contractAction = new ActionBlock<Contract>(contract =>
        {
            futuresContract = new FuturesContractV2ReadModel(
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
        var contractRequestId = commandId.GetHashCode();
        _ibApi.AddErrorHandler(contractRequestId, (id, errorCode, errorMsg, ex) => {
            WriteStatusConsole(commandId, errorCode, errorMsg);
            contractAction.Complete();
        });

        // handle contract details event...
        _ibApi.OnContractDetails += (e) =>
        {
            if (e.RequestId == contractRequestId)
                contractAction.Post(e.ContractDetails.Contract);
        };

        // handle contract details end event...
        _ibApi.OnContractDetailsEnd += (requestId) =>
        {
            if (requestId == contractRequestId)
                contractAction.Complete();
        };

        // request contract details from ib client...
        _ibApi.ClientSocket?.reqContractDetails(contractRequestId, new Contract {
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

    /// <summary>
    /// get futures option spread data
    /// </summary>
    /// <param name="commandId"></param>
    /// <param name="qfShortContract"></param>
    /// <param name="qfLongContract"></param>
    /// <returns></returns>
    public async Task <(FuturesOptionContractReadModel? shortContract, FuturesOptionContractReadModel? longContract)> GetFuturesOptionSpreadAsync(
        Guid commandId,
        FuturesOptionContractReadModel qfShortContract,
        FuturesOptionContractReadModel qfLongContract)
    {
        var shortOptionContract = await GetFuturesOptionContractAsync(commandId, qfShortContract);
        var longOptionContract = await GetFuturesOptionContractAsync(commandId, qfLongContract);
        return (shortOptionContract, longOptionContract);
    }

    public async Task<FuturesOptionContractReadModel?> GetFuturesOptionContractAsync(
        Guid commandId,
        FuturesOptionContractReadModel qfContract)
    {
        var futuresOptionContract = default(FuturesOptionContractReadModel);
        var contractAction = new ActionBlock<Contract>(contract =>
        {
            var optionContractMonth = new DateOnly(
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
                    description: $"{qfContract.Description} {optionContractMonth}{contract.Right.Substring(0, 1)}{contract.Strike:0000}"
            );
        },
        new ExecutionDataflowBlockOptions
        {
            MaxDegreeOfParallelism = 1
        });

        // handle error event...
        var optionRequestId = commandId.GetHashCode();
        _ibApi.AddErrorHandler(optionRequestId, (id, errorCode, errorMsg, ex) => {
            WriteStatusConsole(commandId, errorCode, errorMsg);
            contractAction.Complete();
        });

        // handle contract details event...
        _ibApi.OnContractDetails += (e) =>
        {
            if (e.RequestId == optionRequestId)
                contractAction.Post(e.ContractDetails.Contract);
        };

        // handle contract details end event...
        _ibApi.OnContractDetailsEnd += (requestId) =>
        {
            if (requestId == optionRequestId)
                contractAction.Complete();
        };

        // request futures option contract details from ib client...
        _ibApi.ClientSocket?.reqContractDetails(optionRequestId, new Contract {
            SecType = qfContract.SecurityType,
            Symbol = qfContract.Symbol,
            LocalSymbol = $"{qfContract.LocalSymbol} {qfContract.OptionType.Substring(0, 1)}{qfContract.StrikePrice:0000}",
            Currency = qfContract.Currency,
            Exchange = qfContract.Exchange,
            Multiplier = qfContract.Multiplier,
            LastTradeDateOrContractMonth = $"{qfContract.ContractMonth:yyyyMMdd}",
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
    public async Task<FuturesOptionTickDataV2ReadModel?> GetFuturesOptionPriceAsync(
        Guid commandId,
        DateOnly valueDate,
        FuturesOptionContractReadModel contract)
    {
        var futuresOptionTickData = default(FuturesOptionTickDataV2ReadModel);
        var tickDataAction = default(ActionBlock<FuturesOptionTickDataV2ReadModel>);
        tickDataAction = new ActionBlock<FuturesOptionTickDataV2ReadModel>(e => 
        {
            futuresOptionTickData = e;
            tickDataAction?.Complete();
        },
        new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = 1 });

        // handle error event...
        var requestId = commandId.GetHashCode();
        _ibApi.AddErrorHandler(requestId, (id, errorCode, errorMsg, ex) => {
            WriteStatusConsole(commandId, errorCode, errorMsg);
            tickDataAction.Complete();
        });

        _ibApi.AddStreamingFuturesOptionTickData(requestId, contract, (e,oc) =>
            tickDataAction.Post(new FuturesOptionTickDataV2ReadModel
            (
                contractId: oc.ContractId,
                valueDate: valueDate,
                tickId: e.TickBidAskData.TickTime,
                tickTime: TimeOnly.FromDateTime(DateTime.Now),
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
        _ibApi.ClientSocket?.reqTickByTickData(
            requestId,
            new Contract {
                SecType = contract.SecurityType,
                Symbol = contract.Symbol,
                LocalSymbol = contract.LocalSymbol,
                Currency = contract.Currency,
                Exchange = contract.Exchange,
                Right = contract.OptionType,
                Strike = contract.StrikePrice,
                LastTradeDateOrContractMonth = $"{contract.ContractMonth:yyyyMMdd}",
                Multiplier = contract.Multiplier
            },
            "BidAsk",
            0,true);

        // wait max of 5 seconds until ib client has finished processing...
        var actionTimer = new Timer(state => {
            tickDataAction.Post(FuturesOptionTickDataV2ReadModel.Default);
        }, null, 5000, 5000);

        await tickDataAction.Completion;
        actionTimer.Dispose();  

        _ibApi.CancelTickBidAskPrice(requestId);
        _ibApi.RemoveErrorHandler(requestId);
        return futuresOptionTickData is null || futuresOptionTickData.IsEmpty ? default : futuresOptionTickData;
    }

    /// <summary>
    /// return option greeks
    /// </summary>
    /// <param name="commandId">request id</param>
    /// <param name="valueDate"></param>
    /// <param name="maturityDate"></param>
    /// <param name="contract">futures option contract</param>
    /// <param name="optionValue">futures option price</param>
    /// <param name="futuresPrice">futures price</param>
    /// <param name="riskFreeRate"></param>
    /// <returns></returns>
    public TickOptionComputation? GetFuturesOptionGreeks(
        Guid commandId,
        DateOnly valueDate, 
        DateOnly maturityDate, 
        FuturesOptionContractReadModel contract, 
        double optionValue, 
        double futuresPrice, 
        double riskFreeRate)
    {
        var optionCalculator = new OptionCalculator(valueDate, maturityDate);
        var optionGreeks = optionCalculator.GetOptionGreeks(contract.OptionType, futuresPrice, contract.StrikePrice, optionValue, riskFreeRate);
        return (!optionGreeks.Success) 
            ? default
            : new TickOptionComputation(string.Empty,
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

    /// <summary>
    /// start streaming futures tick data
    /// </summary>
    /// <param name="commandId"></param>
    /// <param name="valueDate"></param>
    /// <param name="contract"></param>
    public void StartStreamingFuturesTickData(
        Guid commandId,
        DateOnly valueDate,
        FuturesContractV2ReadModel contract)
    {
        var requestId = commandId.GetHashCode();
        _ibApi.AddErrorHandler(requestId, (id, errorCode, errorMsg, ex) 
            => WriteStatusConsole(commandId, errorCode, $"{contract.ContractId}: {errorMsg}"));

        // handle streaming futures tick data via posting tick price data event...
        _ibApi.AddStreamingFuturesTickData(requestId, _ => { }, TickDataSyncType.EventProducer);

        // request last tick price from ib client...
        var lastTradeDateOrContractMonth = $"{contract.LastTradeDate:MMMyy}".ToUpperInvariant();
        _ibApi.ClientSocket?.reqTickByTickData(
            requestId,
            new Contract
            {
                SecType = contract.SecurityType,
                Symbol = contract.Symbol == "VX" ? "VIX" : contract.Symbol,
                LocalSymbol = contract.LocalSymbol,
                Currency = contract.Currency,
                Exchange = contract.Exchange,
                TradingClass = contract.Symbol,
                Multiplier = contract.Multiplier,
                LastTradeDateOrContractMonth = lastTradeDateOrContractMonth
            },
            "AllLast",
            0, true);
        return;

    }

    /// <summary>
    /// stop streaming futures tick data
    /// </summary>
    /// <param name="commandId"></param>
    /// <returns></returns>
    public bool StopStreamingFuturesTickData(Guid commandId)
    {
        var stoppedStreaming = false;
        if (_ibApi.Started)
        {
            var requestId = commandId.GetHashCode();
            _ibApi.CancelTickLastPrice(requestId);
            stoppedStreaming = true;
        }
        return stoppedStreaming;
    }

    /// <summary>
    /// start streaming futures option tick data
    /// </summary>
    /// <param name="commandId"></param>
    /// <param name="valueDate"></param>
    /// <param name="maturityDate"></param>
    /// <param name="contract"></param>
    /// <param name="riskFreeRate"></param>
    public void StartStreamingFuturesOptionTickData(
        Guid commandId,
        DateOnly valueDate,
        DateTime maturityDate,
        FuturesOptionContractReadModel contract,
        double riskFreeRate)
    {
        var optionRequestId = commandId.GetHashCode();
        _ibApi.AddErrorHandler(optionRequestId, (_, errorCode, errorMsg, _) 
            => WriteStatusConsole(commandId, errorCode, errorMsg));

        // handle contract details event...
          _ibApi.AddStreamingFuturesOptionTickData(optionRequestId, contract, (_, _) => { }, TickDataSyncType.EventProducer);

        // request last tick price from ib client...
        _ibApi.ClientSocket?.reqTickByTickData(
            optionRequestId,
            new Contract
            {
                SecType = contract.SecurityType,
                Symbol = contract.Symbol,
                LocalSymbol = contract.LocalSymbol,
                Currency = contract.Currency,
                Exchange = contract.Exchange,
                Right = contract.OptionType,
                Strike = contract.StrikePrice,
                LastTradeDateOrContractMonth = $"{contract.ContractMonth:yyyyMMdd}",
                Multiplier = contract.Multiplier
            },
            "BidAsk",
            0, true);
    }

    /// <summary>
    /// stop streaming futures option tick data
    /// </summary>
    /// <param name="commandId"></param>
    /// <returns></returns>
    public bool StopStreamingFuturesOptionTickData(Guid commandId)
    {
        var streamingStopped = false;
        if (_ibApi.Started)
        {
            var requestId = commandId.GetHashCode();
            _ibApi.CancelTickBidAskPrice(requestId);
            streamingStopped = true;
        }
        return streamingStopped;
    }
    
    /// <summary>
    /// write status console
    /// </summary>
    /// <param name="commandId"></param>
    /// <param name="errorCode"></param>
    /// <param name="errorMsg"></param>
    private void WriteStatusConsole(Guid commandId, int errorCode, string errorMsg)
    {
        if (_statusMsgMap?.ContainsKey(errorMsg) ?? false) 
            return;
        _statusMsgMap?.Add(errorMsg, errorMsg);
        if (_errorhandler is not null)
            _errorhandler(commandId, errorCode, errorMsg);
        else
            _statusConsoleWriter.WriteConsoleAsync(LogSourceType.TWSMarketDataApi, errorCode, errorMsg).Wait();
    }
}
