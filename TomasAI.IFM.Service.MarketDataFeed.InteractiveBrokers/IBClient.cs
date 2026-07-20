using IBApi;
using TomasAI.IFM.Shared.MarketDataFeed;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed.Events;
using TomasAI.IFM.Shared.MarketDataFeed.ServiceApi;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.Util;
using TomasAI.IFM.Shared.EventQueue;

namespace TomasAI.IFM.Service.MarketDataFeed.InteractiveBrokers
{
    public class IBClient : EWrapper
    {

        const int FuturesOptionTickMaxStartUpCount = 20;
        // private fields...
       EClientSocket? _clientSocket;
       EReaderSignal? _readerSignal;

        IBMessageReader? _msgReader;
        bool _ibStarted;
        int _futuresOptionTickStartUp;
        Dictionary<int, Action<TickPriceMessage>> _stmFuturesTickData;
        Dictionary<int, Action<TickBidAskMessage, FuturesOptionContractReadModel>> _stmFuturesOptionTickData;
        Dictionary<int, FuturesOptionContractReadModel> _stmFuturesOptionContract;
        Dictionary<int, FuturesOptionQuoteReadModel> _stmFuturesOptionQuote;
        Dictionary<int, long> _lastOptionTickTime;
        Dictionary<int, Action<int, int, string, Exception?>> _errorHandlers;
        Dictionary<int, Dictionary<TickBidAskType, SignalProcessor<TickBidAskData>>> _futuresOptionTickDataSignalProcessorMap;
        System.Timers.Timer? _eventQueueTimer;
        int _cacheClearCounter;
        IMarketDataFeedEventProducer _marketDataFeedEventProducer;
        ConcurrentEventQueue<FuturesTickBidAskEvent>? _futuresTickPriceEventQueue;
        ConcurrentEventQueue<FuturesOptionTickBidAskEvent>? _futuresOptionTickPriceEventQueue;
        ConcurrentEventQueue<FuturesOptionQuoteStreamingDataEvent>? _futuresOptionQuoteEventQueue;

        // public properties...
        public EClientSocket? ClientSocket => _clientSocket;
        public bool Started => _ibStarted;
        // client events...

        public IBClient(IMarketDataFeedEventProducer marketDataFeedEventProducer)
        {
            _stmFuturesTickData = new();
            _stmFuturesOptionTickData = new();
            _stmFuturesOptionContract = new ();
            _stmFuturesOptionQuote = new();
            _futuresOptionTickDataSignalProcessorMap = new();
            _lastOptionTickTime = new();
            _errorHandlers = new();
            _ibStarted = false;
            _cacheClearCounter = 0;
            _marketDataFeedEventProducer = marketDataFeedEventProducer;
            _futuresOptionTickStartUp = 0;
        }

        public void Start(string host, int port, int clientId)
        {
            _ibStarted = false;
            try
            {
                // open socket to TWS if no socket exists...
                if (_clientSocket == null)
                {
                    _readerSignal = new EReaderMonitorSignal();
                    _clientSocket = new EClientSocket(this, _readerSignal);
                }

                // open connection to TWS...
                if (_clientSocket != null && !_clientSocket.IsConnected())
                {
                    _clientSocket.eConnect(host, port, clientId);

                    // start ib message reader task...
                    _msgReader = new IBMessageReader(_clientSocket, _readerSignal!);
                    _msgReader.Start();
                    if (_msgReader.isActive)
                    {
                        _eventQueueTimer = new System.Timers.Timer(200);
                        _eventQueueTimer.Elapsed += (sender, e) =>
                        {
                            if (_cacheClearCounter < 0)
                            {
                                _cacheClearCounter++;
                                if (_cacheClearCounter == 0)
                                {
                                    foreach (var sp in _futuresOptionTickDataSignalProcessorMap.Values)
                                    {
                                        sp[TickBidAskType.Bid].Clear();
                                        sp[TickBidAskType.Ask].Clear();
                                    }
                                }
                            }
                        };
                        _eventQueueTimer.AutoReset = true;
                        _eventQueueTimer.Start();

                        _futuresTickPriceEventQueue = new(e => _marketDataFeedEventProducer.PostEventAsync(e).Wait());
                        _futuresTickPriceEventQueue.Start();

                        _futuresOptionTickPriceEventQueue = new(e => _marketDataFeedEventProducer.PostEventAsync(e).Wait());
                        _futuresOptionTickPriceEventQueue.Start();

                        _futuresOptionQuoteEventQueue = new(e => _marketDataFeedEventProducer.PostEventAsync(e).Wait());
                        _futuresOptionQuoteEventQueue.Start();
                        _ibStarted = true;
                    }
                }
            }
            catch
            {
                _readerSignal = null;
                _clientSocket = null;
                _msgReader = null;
            }
        }

        public void Stop()
        {
            // stop ib message reader task...
            if (_msgReader != null)
            {
                try
                {
                    _msgReader.Stop();
                }
                catch { }
            }
            _msgReader = null;

            // close connection to TWS...
            if (_clientSocket != null)
            {
                try
                {
                    _clientSocket.eDisconnect();
                    _clientSocket.Close();
                }
                catch { }
            }
            _clientSocket = null;

            if (_eventQueueTimer != null)
            {
                try
                {
                    _eventQueueTimer.Stop();
                }
                catch { }
            }
            _futuresTickPriceEventQueue?.Stop();
            _futuresTickPriceEventQueue = null;
            _futuresOptionTickPriceEventQueue?.Stop();
            _futuresOptionTickPriceEventQueue = null;
            _eventQueueTimer = null;
            _readerSignal = null;
            _ibStarted = false;
        }

        public void AddErrorHandler(int requestId, Action<int, int, string, Exception?> errorHandler)
        {
            if (_errorHandlers.ContainsKey(requestId))
                _errorHandlers.Remove(requestId);
            _errorHandlers.Add(requestId, errorHandler);
        }

        public void RemoveErrorHandler(int requestId)
        {
            if (_errorHandlers.ContainsKey(requestId))
                _errorHandlers.Remove(requestId);
        }

        public void error(Exception ex)
        {
            if (_errorHandlers.Count > 0)
                foreach (var entry in _errorHandlers)
                    entry.Value(entry.Key, 0, ex.Message, ex);
        }

        /// <summary>
        /// fire on error message event when error message happens
        /// </summary>
        public void error(string errorMsg)
        {
            if (_errorHandlers.Count > 0)
                foreach (var entry in _errorHandlers)
                    entry.Value(entry.Key, 0, errorMsg, null);
        }

        /// <summary>
        /// fire on error code event when error code happens
        /// </summary>
        public void error(int id, int errorCode, string errorMsg)
        {
            if (_errorHandlers.ContainsKey(id))
                _errorHandlers[id](id, errorCode, errorMsg, null);
        }

        public void currentTime(long time)
        {
            throw new NotImplementedException();
        }

        public void AddStreamingFuturesOptionQuoteData(int requestId,FuturesOptionQuoteReadModel futuresOptionQuote)
        {
            if (!_stmFuturesOptionQuote.ContainsKey(requestId))
                _stmFuturesOptionQuote.Add(requestId, futuresOptionQuote);
        }

        public void RemoveStreamingFuturesOptionQuoteData(int requestId)
        {
            if (_stmFuturesOptionQuote.ContainsKey(requestId))
                _stmFuturesOptionQuote.Remove(requestId);
        }

        public void tickPrice(int tickerId, int field, double price, TickAttrib attribs)
        {
            try
            {
                var tickType = TickType.getField(field);
                var side = tickType switch
                {
                    _ when tickType == "bidPrice" => QuoteSide.Bid,
                    _ when tickType == "askPrice" => QuoteSide.Ask,
                    _ => QuoteSide.Undefined
                };
                if (side == QuoteSide.Undefined)
                    return;
                if (_stmFuturesOptionQuote.ContainsKey(tickerId))
                {
                    var streamingQuoteData = new FuturesOptionQuoteStreamingDataEvent
                    {
                        CommandId = Guid.NewGuid(),
                        QuoteId = _stmFuturesOptionQuote[tickerId].QuoteId,
                        RequestId = tickerId,
                        QuoteData = new QuoteData(DateTime.Now, QuoteLevelType.LevelOne, -1, -1, side, QuoteType.Price, price, -1)
                    };
                    _futuresOptionQuoteEventQueue!.EnqueueAndSignal(streamingQuoteData);
                };
            }
            catch (Exception ex)
            {
                while (ex.InnerException != null) ex = ex.InnerException;
                error(tickerId, -2000, ex.Message);
            }
        } 
 
        public void tickSize(int tickerId, int field, int size)
        {
            try
            {
                var tickType = TickType.getField(field);
                var side = tickType switch
                {
                    _ when tickType == "bidSize" => QuoteSide.Bid,
                    _ when tickType == "askSize" => QuoteSide.Ask,
                    _ => QuoteSide.Undefined
                };
                if (side == QuoteSide.Undefined)
                    return;
                if (_stmFuturesOptionQuote.TryGetValue(tickerId, out FuturesOptionQuoteReadModel? value))
                {
                    var streamingQuoteData = new FuturesOptionQuoteStreamingDataEvent
                    {
                        CommandId = Guid.NewGuid(),
                        QuoteId = value.QuoteId,
                        RequestId = tickerId,
                        QuoteData = new QuoteData(DateTime.Now, QuoteLevelType.LevelOne, -1, -1, side, QuoteType.Size, -1, size)
                    };
                    _futuresOptionQuoteEventQueue!.EnqueueAndSignal(streamingQuoteData);
                };
            }
            catch (Exception ex)
            {
                while (ex.InnerException != null) ex = ex.InnerException;
                error(tickerId, -2001, ex.Message);
            }
        }

        public void tickString(int tickerId, int field, string value)
        {
            //throw new NotImplementedException();
        }

        public void tickGeneric(int tickerId, int field, double value)
        {
            //throw new NotImplementedException();
        }

        public void tickEFP(int tickerId, int tickType, double basisPoints, string formattedBasisPoints, double impliedFuture, int holdDays, string futureLastTradeDate, double dividendImpact, double dividendsToLastTradeDate)
        {
            //throw new NotImplementedException();
        }

        public void tickSnapshotEnd(int tickerId)
        {
            throw new NotImplementedException();
        }

        public event Action<int>? OnNextValidId;
        public void nextValidId(int orderId)
        {
            OnNextValidId?.Invoke(orderId);
            OnNextValidId = null;
        }

        public event Action<string>? OnManagedAccounts;
        public void managedAccounts(string accountsList)
        {
            OnManagedAccounts?.Invoke(accountsList);
            OnManagedAccounts = null;
        }

        public event Action? OnConnectionClosed;
        public void connectionClosed()
        {
            OnConnectionClosed?.Invoke();
            OnConnectionClosed = null;
        }

        public void accountSummary(int reqId, string account, string tag, string value, string currency)
        {
            throw new NotImplementedException();
        }

        public void accountSummaryEnd(int reqId)
        {
            throw new NotImplementedException();
        }

        public void bondContractDetails(int reqId, ContractDetails contract)
        {
            throw new NotImplementedException();
        }

        public void updateAccountValue(string key, string value, string currency, string accountName)
        {
            throw new NotImplementedException();
        }

        public void updatePortfolio(Contract contract, double position, double marketPrice, double marketValue, double averageCost, double unrealizedPNL, double realizedPNL, string accountName)
        {
            throw new NotImplementedException();
        }

        public void updateAccountTime(string timestamp)
        {
            throw new NotImplementedException();
        }

        public void accountDownloadEnd(string account)
        {
            throw new NotImplementedException();
        }

        public void orderStatus(int orderId, string status, double filled, double remaining, double avgFillPrice, int permId, int parentId, double lastFillPrice, int clientId, string whyHeld, double mktCapPrice)
        {
            throw new NotImplementedException();
        }

        public void openOrder(int orderId, Contract contract, Order order, OrderState orderState)
        {
            throw new NotImplementedException();
        }

        public void openOrderEnd()
        {
            throw new NotImplementedException();
        }

        public event Action<ContractDetailsMessage>? OnContractDetails;
        public void contractDetails(int requestId, ContractDetails contractDetails)
        {
            OnContractDetails?.Invoke(new ContractDetailsMessage(requestId, contractDetails));
        }

        public event Action<int>? OnContractDetailsEnd;
        public void contractDetailsEnd(int requestId)
        {
            OnContractDetailsEnd?.Invoke(requestId);
            OnContractDetails = null;
            OnContractDetailsEnd = null;
        }

        public void execDetails(int reqId, Contract contract, Execution execution)
        {
            throw new NotImplementedException();
        }

        public void execDetailsEnd(int reqId)
        {
            throw new NotImplementedException();
        }

        public void commissionReport(CommissionReport commissionReport)
        {
        }

        public void fundamentalData(int reqId, string data)
        {
            throw new NotImplementedException();
        }

        public void historicalData(int reqId, Bar bar)
        {
            throw new NotImplementedException();
        }

        public void historicalDataUpdate(int reqId, Bar bar)
        {
            throw new NotImplementedException();
        }

        public void historicalDataEnd(int reqId, string start, string end)
        {
            throw new NotImplementedException();
        }

        public void marketDataType(int reqId, int marketDataType)
        {
            //throw new NotImplementedException();
        }

        public void updateMktDepth(int tickerId, int position, int operation, int side, double price, int size)
        {
            throw new NotImplementedException();
        }

        public void updateMktDepthL2(int tickerId, int position, string marketMaker, int operation, int side, double price, int size)
        {
            throw new NotImplementedException();
        }

        public void updateNewsBulletin(int msgId, int msgType, string message, string origExchange)
        {
            throw new NotImplementedException();
        }

        public void position(string account, Contract contract, double pos, double avgCost)
        {
            throw new NotImplementedException();
        }

        public void positionEnd()
        {
            throw new NotImplementedException();
        }

   
        public void scannerParameters(string xml)
        {
            throw new NotImplementedException();
        }

        public void scannerData(int reqId, int rank, ContractDetails contractDetails, string distance, string benchmark, string projection, string legsStr)
        {
            throw new NotImplementedException();
        }

        public void scannerDataEnd(int reqId)
        {
            throw new NotImplementedException();
        }

        public void receiveFA(int faDataType, string faXmlData)
        {
            throw new NotImplementedException();
        }

        public void verifyMessageAPI(string apiData)
        {
            throw new NotImplementedException();
        }

        public void verifyCompleted(bool isSuccessful, string errorText)
        {
            throw new NotImplementedException();
        }

        public void verifyAndAuthMessageAPI(string apiData, string xyzChallenge)
        {
            throw new NotImplementedException();
        }

        public void verifyAndAuthCompleted(bool isSuccessful, string errorText)
        {
            throw new NotImplementedException();
        }

        public void displayGroupList(int reqId, string groups)
        {
            throw new NotImplementedException();
        }

        public void displayGroupUpdated(int reqId, string contractInfo)
        {
            throw new NotImplementedException();
        }

        public void connectAck()
        {
            if (ClientSocket?.AsyncEConnect == true)
                ClientSocket.startApi();
        }

        public void positionMulti(int requestId, string account, string modelCode, Contract contract, double pos, double avgCost)
        {
            throw new NotImplementedException();
        }

        public void positionMultiEnd(int requestId)
        {
            throw new NotImplementedException();
        }

        public void accountUpdateMulti(int requestId, string account, string modelCode, string key, string value, string currency)
        {
            throw new NotImplementedException();
        }

        public void accountUpdateMultiEnd(int requestId)
        {
            throw new NotImplementedException();
        }

        public void securityDefinitionOptionParameter(int reqId, string exchange, int underlyingConId, string tradingClass, string multiplier, HashSet<string> expirations, HashSet<double> strikes)
        {
            throw new NotImplementedException();
        }

        public void securityDefinitionOptionParameterEnd(int reqId)
        {
            throw new NotImplementedException();
        }

        public void softDollarTiers(int reqId, SoftDollarTier[] tiers)
        {
            throw new NotImplementedException();
        }

        public void familyCodes(FamilyCode[] familyCodes)
        {
            throw new NotImplementedException();
        }

        public void symbolSamples(int reqId, ContractDescription[] contractDescriptions)
        {
            throw new NotImplementedException();
        }

        public void mktDepthExchanges(DepthMktDataDescription[] depthMktDataDescriptions)
        {
            throw new NotImplementedException();
        }

        public void tickNews(int tickerId, long timeStamp, string providerCode, string articleId, string headline, string extraData)
        {
            throw new NotImplementedException();
        }

        public void smartComponents(int reqId, Dictionary<int, KeyValuePair<string, char>> theMap)
        {
            throw new NotImplementedException();
        }

        public void tickReqParams(int tickerId, double minTick, string bboExchange, int snapshotPermissions)
        {
            //throw new NotImplementedException();
        }

        public void newsProviders(NewsProvider[] newsProviders)
        {
            throw new NotImplementedException();
        }

        public void newsArticle(int requestId, int articleType, string articleText)
        {
            throw new NotImplementedException();
        }

        public void historicalNews(int requestId, string time, string providerCode, string articleId, string headline)
        {
            throw new NotImplementedException();
        }

        public void historicalNewsEnd(int requestId, bool hasMore)
        {
            throw new NotImplementedException();
        }

        public void headTimestamp(int reqId, string headTimestamp)
        {
            throw new NotImplementedException();
        }

        public void histogramData(int reqId, HistogramEntry[] data)
        {
            throw new NotImplementedException();
        }

        public void rerouteMktDataReq(int reqId, int conId, string exchange)
        {
            throw new NotImplementedException();
        }

        public void rerouteMktDepthReq(int reqId, int conId, string exchange)
        {
            throw new NotImplementedException();
        }

        public void marketRule(int marketRuleId, PriceIncrement[] priceIncrements)
        {
            throw new NotImplementedException();
        }

        public void pnl(int reqId, double dailyPnL, double unrealizedPnL, double realizedPnL)
        {
            throw new NotImplementedException();
        }

        public void pnlSingle(int reqId, int pos, double dailyPnL, double unrealizedPnL, double realizedPnL, double value)
        {
            throw new NotImplementedException();
        }

        public void historicalTicks(int reqId, HistoricalTick[] ticks, bool done)
        {
            throw new NotImplementedException();
        }

        public void historicalTicksBidAsk(int reqId, HistoricalTickBidAsk[] ticks, bool done)
        {
            throw new NotImplementedException();
        }

        public void historicalTicksLast(int reqId, HistoricalTickLast[] ticks, bool done)
        {
            throw new NotImplementedException();
        }

        public void AddStreamingFuturesTickData(int requestId, Action<TickPriceMessage> onTickPriceMessage, TickDataSyncType tickDataSyncType = TickDataSyncType.FunctionCall)
        {
            if (!_stmFuturesTickData.ContainsKey(requestId))
                _stmFuturesTickData.Add(requestId, onTickPriceMessage);
        }

        public void RemoveStreamingFuturesTickData(int requestId)
        {
            if (_stmFuturesTickData.ContainsKey(requestId))
                _stmFuturesTickData.Remove(requestId);
        }

        public void tickByTickAllLast(int reqId, int tickType, long tickTime, double price, int size, TickAttrib attribs, string exchange, string specialConditions)
        {
            try
            {
                if (_stmFuturesTickData.ContainsKey(reqId))
                {
                    var futuresTickPriceData = new FuturesTickBidAskEvent
                    {
                        CommandId = Guid.NewGuid(),
                        RequestId = reqId,
                        TickBidAskData = new FuturesTickBidAskReadModel(reqId, DateTime.Now, tickTime, price, size)
                    };
                    _futuresTickPriceEventQueue?.EnqueueAndSignal(futuresTickPriceData);
                   // Task.Run(async () => await _marketDataFeedEventProducer.PostEventAsync(futuresTickPriceData));
                }
            }
            catch(Exception ex)
            {
                while (ex.InnerException != null) ex = ex.InnerException;
                error(reqId, -1000, ex.Message);
            }
        }

        public void AddStreamingFuturesOptionTickData(int requestId, FuturesOptionContractReadModel futuresOptionContract, Action<TickBidAskMessage, FuturesOptionContractReadModel> onTickBidAskMessage, TickDataSyncType tickDataSyncType = TickDataSyncType.FunctionCall)
        {
            if (!_stmFuturesOptionTickData.ContainsKey(requestId))
                _stmFuturesOptionTickData.Add(requestId, onTickBidAskMessage);
            if (!_stmFuturesOptionContract.ContainsKey(requestId))
                _stmFuturesOptionContract.Add(requestId, futuresOptionContract);
            if (!_lastOptionTickTime.ContainsKey(requestId))
                _lastOptionTickTime.Add(requestId, -1);
            if (!_futuresOptionTickDataSignalProcessorMap.ContainsKey(requestId))
            {
                _futuresOptionTickDataSignalProcessorMap.Add(requestId, new Dictionary<TickBidAskType, SignalProcessor<TickBidAskData>>{
                    { TickBidAskType.Bid, new SignalProcessor<TickBidAskData>() },
                    { TickBidAskType.Ask, new SignalProcessor<TickBidAskData>() } });
            }
        }

        public void RemoveStreamingFuturesOptionTickData(int requestId)
        {
            if (_stmFuturesOptionTickData.ContainsKey(requestId))
                _stmFuturesOptionTickData.Remove(requestId);
            if (_stmFuturesOptionContract.ContainsKey(requestId))
                _stmFuturesOptionContract.Remove(requestId);
            if (_lastOptionTickTime.ContainsKey(requestId))
                _lastOptionTickTime.Remove(requestId);
            if (_futuresOptionTickDataSignalProcessorMap.ContainsKey(requestId))
                _futuresOptionTickDataSignalProcessorMap.Remove(requestId);
        }

        public void tickByTickBidAsk(int reqId, long tickTime, double bidPrice, double askPrice, int bidSize, int askSize, TickAttrib attribs)
        {
            try
            {
                var tickBidAskMsg = GetTickBidAskMessage();
                if (tickBidAskMsg is not null)
                {
                    var futuresOptionTickPriceEvent = new FuturesOptionTickBidAskEvent
                    {
                        CommandId = Guid.NewGuid(),
                        RequestId = reqId,
                        TickBidAskData = new FuturesOptionTickBidAskReadModel(DateTime.Now, tickTime, tickBidAskMsg.TickBidAskData.BidPrice, tickBidAskMsg.TickBidAskData.AskPrice, tickBidAskMsg.TickBidAskData.BidSize, tickBidAskMsg.TickBidAskData.AskSize)
                    };
                    _futuresOptionTickPriceEventQueue?.EnqueueAndSignal(futuresOptionTickPriceEvent);
                }
            }
            catch (Exception ex)
            {
                while (ex.InnerException != null) ex = ex.InnerException;
                error(reqId, -1001, ex.Message);
            }
            return;

            TickBidAskMessage? GetTickBidAskMessage()
            {
                try
                {
                    var tickData = new TickBidAskData(DateTime.Now, tickTime, bidPrice, askPrice, bidSize, askSize);
                    if (_futuresOptionTickStartUp < FuturesOptionTickMaxStartUpCount)
                        _futuresOptionTickStartUp++;
                    else
                    {
                        if (!_futuresOptionTickDataSignalProcessorMap.ContainsKey(reqId))
                            return default;
                        var signalProcessorMap = _futuresOptionTickDataSignalProcessorMap[reqId];
                        if (!signalProcessorMap.TryGetValue(TickBidAskType.Bid, out SignalProcessor<TickBidAskData>? bidValue))
                            return null;
                        tickData = bidValue.Filter(tickData, 10, e => e.BidPrice);
                        if (tickData is null)
                            return null;
                        if (!signalProcessorMap.TryGetValue(TickBidAskType.Ask, out SignalProcessor<TickBidAskData>? askValue))
                            return null;
                        tickData = askValue.Filter(tickData, 10, e => e.AskPrice);
                        if (tickData is null)
                            return null;
                    }
                    if (tickData.BidPrice > 5.00 || tickData.AskPrice > 5.0)
                    {
                        var bidAskDiff = Math.Abs(tickData.BidPrice - tickData.AskPrice);
                        if (bidAskDiff > 10.0)
                            return null;
                    }
                    else if (Math.Abs(tickData.BidPrice - tickData.AskPrice) > 1.5)
                        return null;
                    return new TickBidAskMessage(reqId, tickData);
                }
                catch { }
                return null;
            }

        }

        public event Action<RealTimeBarMessage>? OnRealTimeBars;

        public void realtimeBar(int reqId, long time, double open, double high, double low, double close, long volume, double WAP, int count)
        {
            if (OnRealTimeBars != null)
            {
                var rtbData = new RealTimeBarData(time, open, high, low, close, volume, WAP, count);
                var rtbMsg = new RealTimeBarMessage(reqId, rtbData);
                OnRealTimeBars(rtbMsg);
            }
        }

        public void CancelTickLastPrice(int requestId)
        {
            _clientSocket?.cancelTickByTickData(requestId);
            RemoveStreamingFuturesTickData(requestId);
            if (_errorHandlers.ContainsKey(requestId))
                _errorHandlers.Remove(requestId);
        }

        public void CancelTickBidAskPrice(int requestId)
        {
            _clientSocket?.cancelTickByTickData(requestId);
            RemoveStreamingFuturesOptionTickData(requestId);
            if (_errorHandlers.ContainsKey(requestId))
                _errorHandlers.Remove(requestId);
        }

        public void CancelRealTimeBars(int requestId)
        {
            _clientSocket?.cancelRealTimeBars(requestId);
            if (OnRealTimeBars != null)
                OnRealTimeBars = null;
            if (_errorHandlers.ContainsKey(requestId))
                _errorHandlers.Remove(requestId);
        }

        public void tickByTickMidPoint(int reqId, long time, double midPoint)
        {
            throw new NotImplementedException();
        }

        public void tickOptionComputation(int tickerId, int field, double impliedVolatility, double delta, double optPrice, double pvDividend, double gamma, double vega, double theta, double undPrice)
        {
            throw new NotImplementedException();
        }

        public void deltaNeutralValidation(int reqId, DeltaNeutralContract deltaNeutralContract)
        {
            throw new NotImplementedException();
        }

        public void updateMktDepthL2(int tickerId, int position, string marketMaker, int operation, int side, double price, int size, bool isSmartDepth)
        {
            throw new NotImplementedException();
        }

        public void tickByTickAllLast(int reqId, int tickType, long tickTime, double price, int size, TickAttribLast tickAttriblast, string exchange, string specialConditions)
        {
            try
            {
                if (_stmFuturesTickData.ContainsKey(reqId))
                {
                    var futuresTickPriceData = new FuturesTickBidAskEvent
                    {
                        CommandId = Guid.NewGuid(),
                        RequestId = reqId,
                        TickBidAskData = new FuturesTickBidAskReadModel(reqId, DateTime.Now, tickTime, price, size)
                    };
                    _futuresTickPriceEventQueue?.EnqueueAndSignal(futuresTickPriceData);
                    // Task.Run(async () => await _marketDataFeedEventProducer.PostEventAsync(futuresTickPriceData));
                }
            }
            catch (Exception ex)
            {
                while (ex.InnerException != null) ex = ex.InnerException;
                error(reqId, -1000, ex.Message);
            }
        }

        public void tickByTickBidAsk(int reqId, long tickTime, double bidPrice, double askPrice, int bidSize, int askSize, TickAttribBidAsk tickAttribBidAsk)
        {
            try
            {
                var tickBidAskMsg = GetTickBidAskMessage();
                if (tickBidAskMsg is not null)
                {
                    var futuresOptionTickPriceEvent = new FuturesOptionTickBidAskEvent
                    {
                        CommandId = Guid.NewGuid(),
                        RequestId = reqId,
                        TickBidAskData = new FuturesOptionTickBidAskReadModel(DateTime.Now, tickTime, tickBidAskMsg.TickBidAskData.BidPrice, tickBidAskMsg.TickBidAskData.AskPrice, tickBidAskMsg.TickBidAskData.BidSize, tickBidAskMsg.TickBidAskData.AskSize)
                    };
                    _futuresOptionTickPriceEventQueue?.EnqueueAndSignal(futuresOptionTickPriceEvent);
                }
            }
            catch (Exception ex)
            {
                while (ex.InnerException != null) ex = ex.InnerException;
                error(reqId, -1001, ex.Message);
            }
            return;

            TickBidAskMessage? GetTickBidAskMessage()
            {
                try
                {
                    var tickData = new TickBidAskData(DateTime.Now, tickTime, bidPrice, askPrice, bidSize, askSize);
                    if (_futuresOptionTickStartUp < FuturesOptionTickMaxStartUpCount)
                        _futuresOptionTickStartUp++;
                    else
                    {
                        if (!_futuresOptionTickDataSignalProcessorMap.ContainsKey(reqId))
                            return default;
                        var signalProcessorMap = _futuresOptionTickDataSignalProcessorMap[reqId];
                        if (!signalProcessorMap.TryGetValue(TickBidAskType.Bid, out SignalProcessor<TickBidAskData>? bidValue))
                            return null;
                        tickData = bidValue.Filter(tickData, 10, e => e.BidPrice);
                        if (tickData is null)
                            return null;
                        if (!signalProcessorMap.TryGetValue(TickBidAskType.Ask, out SignalProcessor<TickBidAskData>? askValue))
                            return null;
                        tickData = askValue.Filter(tickData, 10, e => e.AskPrice);
                        if (tickData is null)
                            return null;
                    }
                    if (tickData.BidPrice > 5.00 || tickData.AskPrice > 5.0)
                    {
                        var bidAskDiff = Math.Abs(tickData.BidPrice - tickData.AskPrice);
                        if (bidAskDiff > 10.0)
                            return null;
                    }
                    else if (Math.Abs(tickData.BidPrice - tickData.AskPrice) > 1.5)
                        return null;
                    return new TickBidAskMessage(reqId, tickData);
                }
                catch { }
                return null;
            }

        }

        public void orderBound(long orderId, int apiClientId, int apiOrderId)
        {
            throw new NotImplementedException();
        }

        public void completedOrder(Contract contract, Order order, OrderState orderState)
        {
            throw new NotImplementedException();
        }

        public void completedOrdersEnd()
        {
            throw new NotImplementedException();
        }
    }
}
