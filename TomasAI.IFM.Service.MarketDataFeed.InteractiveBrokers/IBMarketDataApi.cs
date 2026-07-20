using System.Threading.Tasks.Dataflow;
using IBApi;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed.ServiceApi;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.Shared.OptionPricer;

namespace TomasAI.IFM.Service.MarketDataFeed.InteractiveBrokers;

public class IBMarketDataSnapshotApi(IMarketDataSnapshotApiOptions snapshotOptions, IStatusConsoleWriter statusConsoleWriter, IMarketDataFeedEventProducer marketDataFeedEventProducer) 
    : IBMarketDataApi(snapshotOptions, statusConsoleWriter, marketDataFeedEventProducer), IMarketDataSnapshotApi
{
}

public class IBMarketDataApi : IMarketDataApi
{
    static Dictionary<string, string> _statusMsgMap = new();
    IMarketDataApiOptions _options;
    IStatusConsoleWriter _statusConsoleWriter;
    IBClient _ibApi;
    Dictionary<DateTime, string> _errorMessages = new();
    Action<int, string>? _errorMessageAction;
    IStreamIdCollection _streamIds = new StreamIdCollection();

    public IBMarketDataApi(IMarketDataApiOptions options, IStatusConsoleWriter statusConsoleWriter, IMarketDataFeedEventProducer marketDataFeedEventProducer)
    {
        _options = options;
        _statusConsoleWriter = statusConsoleWriter;
        _ibApi = new IBClient(marketDataFeedEventProducer);
        InitMarketDataApi();
    }

    public IStreamIdCollection StreamIds => _streamIds;

    private void InitMarketDataApi()
    {
        _errorMessages = new Dictionary<DateTime, string>();
        _streamIds = new StreamIdCollection();
        _statusMsgMap = new Dictionary<string, string>();
    }

    /// <summary>
    /// start interactive brokers client
    /// </summary>
    public bool Start(Action<int, string>? errorMessageAction = null)
    {
        if (_ibApi.Started)
            return true;
        InitMarketDataApi();
        _errorMessageAction = errorMessageAction;
        for (var retryCount = 0; retryCount < 3; retryCount++)
        {
            _ibApi.Start(_options.Host, _options.Port, _options.ClientId);
            Task.Delay(TimeSpan.FromSeconds(2)).Wait();
            if (_ibApi.Started)
                return true;
        }
        return false;
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
    public async Task<FuturesTickDataV2ReadModel> GetFuturesPriceAsync(int requestId, FuturesContractV2ReadModel contract)
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
        _ibApi.AddErrorHandler(requestId, (id, errorCode, errorMsg, ex) => {
            WriteStatusConsole(errorCode, errorMsg);
            tickDataAction.Complete();
        });

        _ibApi.AddStreamingFuturesTickData(requestId, e =>
            tickDataAction.Post( new FuturesTickDataV2ReadModel (
                valueDate: DateOnly.MinValue,
                contractId: contract.ContractId,
                tickId: e.TickPriceData.TickTime,
                tickTime: TimeOnly.FromDateTime(DateTime.Now),
                price: Convert.ToDecimal( e.TickPriceData.Price),
                size: e.TickPriceData.Size
            ))
        );

        // request last tick price from ib client...
        _ibApi.ClientSocket!.reqTickByTickData(
            requestId, 
            new Contract { SecType = contract.SecurityType, Symbol = contract.Symbol, LocalSymbol = contract.LocalSymbol, Currency = contract.Currency, Exchange = contract.Exchange }, 
            "Last",
            0,
            true);

        // wait until ib client has finished processing...
        await tickDataAction.Completion;

        // close last tick price request...
        _ibApi.CancelTickLastPrice(requestId);
        _ibApi.RemoveErrorHandler(requestId);
        return futuresTickData!;
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
    public async Task<FuturesContractV2ReadModel?> GetFuturesContractAsync(int contractRequestId, FuturesContractV2ReadModel qfContract)
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
        _ibApi.AddErrorHandler(contractRequestId, (id, errorCode, errorMsg, ex) => {
            WriteStatusConsole(errorCode, errorMsg);
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
        _ibApi.ClientSocket!.reqContractDetails(contractRequestId, new Contract {
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

    public async Task <(FuturesOptionContractReadModel? shortContract, FuturesOptionContractReadModel? longContract)> GetFuturesOptionSpreadAsync(
        FuturesOptionContractReadModel qfShortContract,
        FuturesOptionContractReadModel qfLongContract)
    {
        var shortOptionContract = await GetFuturesOptionContractAsync(RequestID.ShortOption, qfShortContract);
        var longOptionContract = await GetFuturesOptionContractAsync(RequestID.LongOption, qfLongContract);
        return (shortOptionContract, longOptionContract);
    }

    public async Task<FuturesOptionContractReadModel?> GetFuturesOptionContractAsync(
        int optionRequestId,
        FuturesOptionContractReadModel qfContract)
    {
        var futuresOptionContract = default(FuturesOptionContractReadModel);
        var contractAction = new ActionBlock<Contract>(contract =>
        {
            var optionContractMonth = new DateOnly(
                year: Convert.ToInt32(contract.LastTradeDateOrContractMonth.Substring(0, 4)),
                month: Convert.ToInt32(contract.LastTradeDateOrContractMonth.Substring(4, 2)),
                day: Convert.ToInt32(contract.LastTradeDateOrContractMonth.Substring(6, 2)));
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
                    description: $"{qfContract?.Description ?? string.Empty}"
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
                contractAction.Post(e.ContractDetails.Contract);
        };

        // handle contract details end event...
        _ibApi.OnContractDetailsEnd += (requestId) =>
        {
            if (requestId == optionRequestId)
                contractAction.Complete();
        };

        var optionContractRequested = new Contract
        {
            SecType = qfContract.SecurityType,
            Symbol = qfContract.Symbol,
            LocalSymbol = qfContract.LocalSymbol,
            Currency = qfContract.Currency,
            Exchange = qfContract.Exchange,
            Multiplier = qfContract.Multiplier,
            LastTradeDateOrContractMonth = $"{qfContract.ContractMonth:MMMyy}".Replace(".","").ToUpper(),
            Right = qfContract.OptionType,
            Strike = qfContract.StrikePrice
        };

        // request futures option contract details from ib client...
        _ibApi.ClientSocket!.reqContractDetails(optionRequestId, optionContractRequested);

        // wait until ib client has finished processing...
        contractAction.Completion.Wait(TimeSpan.FromSeconds(5));
        _ibApi.RemoveErrorHandler(optionRequestId);
        return await Task.FromResult( futuresOptionContract);
    }

    /// <summary>
    /// return futures option price
    /// </summary>
    /// <param name="requestId">requets id</param>
    /// <param name="valueDate">value date</param>
    /// <param name="contract">futures option contract</param>
    /// <param name="optionTickData"></param>
    /// <returns></returns>
    public async Task GetFuturesOptionPriceAsync(int requestId, DateOnly valueDate, FuturesOptionContractReadModel contract, Action<FuturesOptionTickDataV2ReadModel> optionTickData)
    {
        var tickDataAction = default(ActionBlock<FuturesOptionTickDataV2ReadModel>);
        tickDataAction = new ActionBlock<FuturesOptionTickDataV2ReadModel>(e => 
        {
            optionTickData(e);
            tickDataAction?.Complete();
        },
        new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = 1 });

        // handle error event...
        _ibApi.AddErrorHandler(requestId, (id, errorCode, errorMsg, ex) => {
            WriteStatusConsole(errorCode, errorMsg);
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
        _ibApi.ClientSocket!.reqTickByTickData(
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
            0, 
            true);

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
    /// <param name="valueDate">request id</param>
    /// <param name="maturityDate">futures option contract</param>
    /// <param name="contract">futures option price</param>
    /// <param name="optionValue">option value</param>
    /// <param name="futuresPrice">futures price</param>
    /// <param name="riskFreeRate"> risk free rate</param>
    /// <returns></returns>
    public TickOptionComputation GetFuturesOptionGreeks(DateOnly valueDate, DateOnly maturityDate, FuturesOptionContractReadModel contract, double optionValue, double futuresPrice, double riskFreeRate)
    {
        var optionCalculator = new OptionCalculator(valueDate, maturityDate);
        var optionGreeks = optionCalculator.GetOptionGreeks(contract.OptionType, futuresPrice, contract.StrikePrice, optionValue, riskFreeRate);
        if (!optionGreeks.Success) return null!;

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
        DateOnly valueDate,
        FuturesContractV2ReadModel contract)
    {
        _ibApi.AddErrorHandler(requestId, (id, errorCode, errorMsg, ex) => WriteStatusConsole(errorCode, $"{contract.ContractId}: {errorMsg}"));

        // handle streaming futures tick data via posting tick price data event...
        _ibApi.AddStreamingFuturesTickData(requestId, _ => { }, TickDataSyncType.EventProducer);

        // request last tick price from ib client...
        var lastTradeDateOrContractMonth = $"{contract.LastTradeDate:MMMyy}".ToUpperInvariant();
        _ibApi.ClientSocket!.reqTickByTickData(
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
            0,
            true);
        return;

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
        DateOnly valueDate,
        DateOnly maturityDate,
        FuturesOptionContractReadModel contract,
        double riskFreeRate)
    {
        _ibApi.AddErrorHandler(optionRequestId, (_, errorCode, errorMsg, _) => WriteStatusConsole(errorCode, errorMsg));

        // handle contract details event...
        _ibApi.AddStreamingFuturesOptionTickData(optionRequestId, contract, (_, _) => { }, TickDataSyncType.EventProducer);

        // request last tick price from ib client...
        _ibApi.ClientSocket!.reqTickByTickData(
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
            0,
            true);
        return;
    }

    public bool StopStreamingFuturesOptionTickData(int requestId)
    {
        var streamingStopped = false;
        if (_ibApi.Started)
        {
            _ibApi.CancelTickBidAskPrice(requestId);
            streamingStopped = true;
        }
        return streamingStopped;
    }

    public void StartStreamingFuturesOptionQuoteData(int optionRequestId, FuturesOptionContractReadModel optionContract, FuturesOptionQuoteReadModel optionQuote)
    {
        _ibApi.AddErrorHandler(optionRequestId, (_, errorCode, errorMsg, _) => WriteStatusConsole(errorCode, errorMsg));

        // handle contract details event...
        _ibApi.AddStreamingFuturesOptionQuoteData(optionRequestId, optionQuote);

        // request market level 1 quote data from ib client...
        _ibApi.ClientSocket!.reqMarketDataType(4);
        _ibApi.ClientSocket!.reqMktData(
            optionRequestId,
            new Contract
            {
                SecType = optionContract.SecurityType,
                Symbol = optionContract.Symbol,
                LocalSymbol = optionContract.LocalSymbol,
                Currency = optionContract.Currency,
                Exchange = optionContract.Exchange,
                Right = optionContract.OptionType,
                Strike = optionContract.StrikePrice,
                LastTradeDateOrContractMonth = $"{optionContract.ContractMonth:yyyyMMdd}",
                Multiplier = optionContract.Multiplier
            },
            "",
            false,
            false,
            []);
    }

    public bool StopStreamingFuturesOptionQuoteData(int optionRequestId)
    {
        var streamingStopped = false;
        if (_ibApi.Started)
        {
            _ibApi.ClientSocket!.cancelMktData(optionRequestId);
            _ibApi.RemoveStreamingFuturesOptionQuoteData(optionRequestId) ;
            _ibApi.RemoveErrorHandler(optionRequestId);
            streamingStopped = true;
        }
        return streamingStopped;
    }

    void WriteStatusConsole(int errorCode, string errorMsg)
    {
        if (_statusMsgMap.ContainsKey(errorMsg)) return;
        _statusMsgMap.Add(errorMsg, errorMsg);
        if (_errorMessageAction is not null)
            _errorMessageAction(errorCode, errorMsg);
        else
            _statusConsoleWriter.WriteConsoleAsync(LogSourceType.TWSMarketDataApi, errorCode, errorMsg).Wait();
    }

    public void StartStreamingFuturesOptionQuoteData(int optionRequestId, FuturesOptionContractReadModel contract)
    {
        throw new NotImplementedException();
    }
}
