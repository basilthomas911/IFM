using IBApi;
using TomasAI.IFM.Shared.MarketDataFeed;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed.Events;
using TomasAI.IFM.Shared.MarketDataFeed.ServiceApi;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Framework.MarketData.InteractiveBrokers.Messages;

namespace TomasAI.IFM.Framework.MarketData.InteractiveBrokers
{
    public class IBClient : EWrapper
    {
        // private fields...
        EClientSocket? _clientSocket;
        EReaderSignal? _readerSignal;
        IBMessageReader? _msgReader;
        bool _ibStarted;
        static Dictionary<int, Action<TickPriceMessage>> _stmFuturesTickData = new();
        static Dictionary<int, Action<TickBidAskMessage, FuturesOptionContractReadModel>>? _stmFuturesOptionTickData = new();
        static Dictionary<int, FuturesOptionContractReadModel>? _stmFuturesOptionContract = new();
        static Dictionary<int, List<TickBidAskMessage>>? _stmFuturesOptionTickDataBuffer = new();
        static Dictionary<int, SpinLock>? _stmFuturesOptionSpinLock = new();
        static Dictionary<int, long>? _lastOptionTickTime = new();
        static Dictionary<int, Action<int, int, string, Exception?>>? _errorHandlers = new();
        static IMarketDataFeedEventProducer? _marketDataFeedEventProducer;

        // public properties...
        public EClientSocket? ClientSocket => _clientSocket;
        public bool Started => _ibStarted;
        // client events...

        public IBClient(IMarketDataFeedEventProducer marketDataFeedEventProducer)
        {
            _ibStarted = false;
            _marketDataFeedEventProducer = marketDataFeedEventProducer;
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
                if (_clientSocket is not null && !_clientSocket.IsConnected())
                {
                    _clientSocket.eConnect(host, port, clientId);

                    // start ib message reader task...
                    _msgReader = new IBMessageReader(_clientSocket, _readerSignal);
                    _msgReader.Start();
                    _ibStarted = true;
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
            _readerSignal = null;
            _ibStarted = false;
        }

        public void AddErrorHandler(int requestId, Action<int, int, string, Exception> errorHandler)
        {
            if (_errorHandlers?.ContainsKey(requestId) ?? false)
                _errorHandlers.Remove(requestId);
            _errorHandlers?.Add(requestId, errorHandler);
        }

        public void RemoveErrorHandler(int requestId)
        {
            if (_errorHandlers?.ContainsKey(requestId) ?? false)
                _errorHandlers.Remove(requestId);
        }

        public void error(Exception ex)
        {
            if ((_errorHandlers?.Count ?? 0) > 0)
                foreach (var entry in _errorHandlers)
                    entry.Value(entry.Key, 0, ex.Message, ex);
        }

        /// <summary>
        /// fire on error message event when error message happens
        /// </summary>
        public void error(string errorMsg)
        {
            if ((_errorHandlers?.Count ?? 0) > 0)
                foreach (var entry in _errorHandlers)
                    entry.Value(entry.Key, 0, errorMsg, default);
        }

        /// <summary>
        /// fire on error code event when error code happens
        /// </summary>
        public void error(int id, int errorCode, string errorMsg)
        {
            if (_errorHandlers?.ContainsKey(id) ?? false)
                _errorHandlers[id](id, errorCode, errorMsg, default);
        }

        public void currentTime(long time)
        {
            throw new NotImplementedException();
        }

        public void tickPrice(int tickerId, int field, double price, TickAttrib attribs)
        {
            //throw new NotImplementedException();
        }

        public void tickSize(int tickerId, int field, int size)
        {
            //throw new NotImplementedException();
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
            if (ClientSocket.AsyncEConnect)
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
            if (!(_stmFuturesTickData?.ContainsKey(requestId) ?? false))
                _stmFuturesTickData?.Add(requestId, onTickPriceMessage);
        }

        public void RemoveStreamingFuturesTickData(int requestId)
        {
            if (_stmFuturesTickData?.ContainsKey(requestId) ?? false)
                _stmFuturesTickData?.Remove(requestId);
        }

        public void AddStreamingFuturesOptionTickData(int requestId, FuturesOptionContractReadModel futuresOptionContract, Action<TickBidAskMessage, FuturesOptionContractReadModel> onTickBidAskMessage, TickDataSyncType tickDataSyncType = TickDataSyncType.FunctionCall)
        {
            if (!(_stmFuturesOptionTickData?.ContainsKey(requestId) ?? false))
                _stmFuturesOptionTickData?.Add(requestId, onTickBidAskMessage);
            if (! (_stmFuturesOptionContract?.ContainsKey(requestId) ?? false))
                _stmFuturesOptionContract?.Add(requestId, futuresOptionContract);
            if (! (_stmFuturesOptionTickDataBuffer?.ContainsKey(requestId) ?? false))
                _stmFuturesOptionTickDataBuffer?.Add(requestId, new());
            if (!(_lastOptionTickTime?.ContainsKey(requestId) ?? false))
                _lastOptionTickTime?.Add(requestId, -1);
            if (!(_stmFuturesOptionSpinLock?.ContainsKey(requestId) ?? false))
                _stmFuturesOptionSpinLock?.Add(requestId, new SpinLock());
        }

        public void RemoveStreamingFuturesOptionTickData(int requestId)
        {
            if (_stmFuturesOptionTickData?.ContainsKey(requestId) ?? false)
                _stmFuturesOptionTickData.Remove(requestId);
            if (_stmFuturesOptionContract?.ContainsKey(requestId) ?? false)
                _stmFuturesOptionContract.Remove(requestId);
            if (_stmFuturesOptionTickDataBuffer?.ContainsKey(requestId) ?? false)
                _stmFuturesOptionTickDataBuffer.Remove(requestId);
            if (_lastOptionTickTime?.ContainsKey(requestId) ?? false)
                _lastOptionTickTime.Remove(requestId);
            if (_stmFuturesOptionSpinLock?.ContainsKey(requestId) ?? false)
                _stmFuturesOptionSpinLock.Remove(requestId);
         }

        public event Action<RealTimeBarMessage>? OnRealTimeBars;

        public void realtimeBar(int reqId, long time, double open, double high, double low, double close, long volume, double WAP, int count)
        {
            var rtbData = new RealTimeBarData(time, open, high, low, close, volume, WAP, count);
            var rtbMsg = new RealTimeBarMessage(reqId, rtbData);
            OnRealTimeBars?.Invoke(rtbMsg);
        }

        public void CancelTickLastPrice(int requestId)
        {
            _clientSocket?.cancelTickByTickData(requestId);
            RemoveStreamingFuturesTickData(requestId);
            if (_errorHandlers?.ContainsKey(requestId) ?? false)
                _errorHandlers.Remove(requestId);
        }

        public void CancelTickBidAskPrice(int requestId)
        {
            _clientSocket?.cancelTickByTickData(requestId);
            RemoveStreamingFuturesOptionTickData(requestId);
            if (_errorHandlers?.ContainsKey(requestId) ?? false)
                _errorHandlers.Remove(requestId);
        }

        public void CancelRealTimeBars(int requestId)
        {
            _clientSocket?.cancelRealTimeBars(requestId);
            if (OnRealTimeBars != null)
                OnRealTimeBars = null;
            if (_errorHandlers?.ContainsKey(requestId) ?? false)
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

        public void updateMktDepthL2(int tickerId, int position, string marketMaker, int operation, int side, double price, int size, bool isSmartDepth)
        {
            throw new NotImplementedException();
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

        public void tickByTickAllLast(int reqId, int tickType, long time, double price, int size, TickAttrib attribs, string exchange, string specialConditions)
        {
            try
            {
                if (_stmFuturesTickData.ContainsKey(reqId))
                {
                    var futuresTickPriceData = new FuturesTickBidAskEvent
                    {
                        CommandId = Guid.NewGuid(),
                        RequestId = reqId,
                        TickBidAskData = new FuturesTickBidAskReadModel(reqId, DateTime.Now, time, price, size)
                    };
                    _marketDataFeedEventProducer?.PostEventAsync(futuresTickPriceData).Wait();
                }
            }
            catch (Exception ex)
            {
                while (ex.InnerException != null) ex = ex.InnerException;
                error(reqId, -1000, ex.Message);
            }
        }

        public void tickByTickBidAsk(int reqId, long time, double bidPrice, double askPrice, int bidSize, int askSize, TickAttrib attribs)
        {
            try
            {
                var tickBidAskMsg = GetTickBidAskMessage();
                if (tickBidAskMsg is not null)
                {
                    var futuresOptionTickPriceData = new FuturesOptionTickBidAskEvent
                    {
                        CommandId = Guid.NewGuid(),
                        RequestId = reqId,
                        TickBidAskData = new FuturesOptionTickBidAskReadModel(DateTime.Now, time, tickBidAskMsg.TickBidAskData.BidPrice, tickBidAskMsg.TickBidAskData.AskPrice, tickBidAskMsg.TickBidAskData.BidSize, tickBidAskMsg.TickBidAskData.AskSize)
                    };
                    _marketDataFeedEventProducer?.PostEventAsync(futuresOptionTickPriceData).Wait();
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
                    var tickData = new TickBidAskData(DateTime.Now, time, bidPrice, askPrice, bidSize, askSize);
                    /*
                    if (!(_futuresOptionTickDataSignalProcessorMap?.ContainsKey(reqId) ?? false))
                        return default;
                    var signalProcessorMap = _futuresOptionTickDataSignalProcessorMap[reqId];
                    if (!signalProcessorMap.ContainsKey(TickBidAskType.Bid))
                        return default;
                    tickData = signalProcessorMap[TickBidAskType.Bid].Filter(tickData, 10, e => e.BidPrice);
                    if (tickData == null)
                        return default;
                    if (!signalProcessorMap.ContainsKey(TickBidAskType.Ask))
                        return default;
                    tickData = signalProcessorMap[TickBidAskType.Ask].Filter(tickData, 10, e => e.AskPrice);
                    if (tickData == null)
                      
                    return default;
                    */
                    if (tickData.BidPrice > 5.00 || tickData.AskPrice > 5.0)
                    {
                        var bidAskDiff = Math.Abs(tickData.BidPrice - tickData.AskPrice);
                        if (bidAskDiff > 10.0)
                            return default;
                    }
                    else if (Math.Abs(tickData.BidPrice - tickData.AskPrice) > 1.5)
                        return default;
                    return new TickBidAskMessage(reqId, tickData);
                }
                catch { }
                return default;
            }

        }

        public void deltaNeutralValidation(int reqId, DeltaNeutralContract deltaNeutralContract)
        {
            throw new NotImplementedException();
        }

        public void tickByTickAllLast(int reqId, int tickType, long time, double price, int size, TickAttribLast tickAttriblast, string exchange, string specialConditions)
        {
            throw new NotImplementedException();
        }

        public void tickByTickBidAsk(int reqId, long time, double bidPrice, double askPrice, int bidSize, int askSize, TickAttribBidAsk tickAttribBidAsk)
        {
            throw new NotImplementedException();
        }
    }
}
