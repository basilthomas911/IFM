using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TomasAI.IFM.Contracts;
using TomasAI.IFM.Models;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.Fund.ViewModels;
using TomasAI.IFM.Shared.MarketData;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Trade.ViewModels;
using TomasAI.IFM.Shared.EventQueue;
using TomasAI.IFM.Shared.OptionPricer;
using TomasAI.IFM.Shared.OptionPricer.ViewModels;
using TomasAI.IFM.Shared.Log;
using TomasAI.IFM.Shared.MarketDataFeed.Events;

namespace TomasAI.IFM.ViewModels.Trade.IronCondor
{
    public class IronCondorViewModel
    {
        public const int GetTradeInfoErrorCode = 6000;
        public const int GetTradePositionsErrorCode = 6001;
        public const int GetOptionTradeErrorCode = 6002;
        public const string LiveFeedOn = "LiveFeed ON";
        public const string LiveFeedOff = "LiveFeed OFF";

        IAppRoot _appRoot;
        Guid _siteId;
        FundReadModel _fund;
        FundOrderReadModel _fundOrder;
        FundOrderTradeReadModel _fundOrderTrade;
        List<FundOrderTradeReadModel> _fundOrderTrades;
        DateTime? _valueDate;
        ICollection<FuturesContractViewModel> _baseContracts;
        OptionTradeViewModel _optionTrade;
        List<TradeHistoryReadModel> _tradeHistory;
        List<TradeInfoReadModel> _tradeInfo;
        FuturesContractViewModel _futuresContract;
        List<FuturesEodDataViewModel> _futuresEodData;
        ConcurrentAsyncEventQueue<TradePositionChangeSourceReadModel> _tradePositionQueue;
        ConcurrentStack<IronCondorSpreadPathViewModel> _spreadPathQueue;
        ConcurrentEventQueue<TradePlanReadModel> _tradePlanConsoleQueue;
        ConcurrentEventQueue<TradePlanActionReadModel> _tradePlanSummaryQueue;
        List<TradePositionReadModel> _tradePositions;
        TradeLimitReadModel _tradeLimits;
        decimal _fundBalance;
        double _riskFreeRate;
        Timer _snapshotTimer;
        Timer _spreadBarDataTimer;
        Dictionary<string, OptionLegDataReadModel> _optionLegDataMap;
        List<IronCondorSpreadPathViewModel> _spreadPaths;
        bool _liveFeedEnabled;
        Dictionary<StreamId, string> _liveStreamsIds;
        bool _futuresEodHistoryLoaded;
        AutoResetEvent _tradePositionUpdateResetEvent;

        /// <summary>
        /// create iron condor view model
        /// </summary>
        /// <param name="appRoot"></param>
        /// <param name="fundOrder"></param>
        /// <param name="fundOrderTrade"></param>
        /// <param name="valueDate"></param>
        /// <param name="baseContracts"></param>
        public IronCondorViewModel(IAppRoot appRoot, FundReadModel fund,  FundOrderReadModel fundOrder, FundOrderTradeReadModel fundOrderTrade, DateTime? valueDate, ICollection<FuturesContractViewModel> baseContracts)
        {
            _appRoot = appRoot;
            _fund = fund;
            _fundOrder = fundOrder;
            _fundOrderTrade = fundOrderTrade;
            _valueDate = valueDate;
            _baseContracts = baseContracts;
            _fundOrderTrades = new();
            _fundOrderTrades.AddRange(_fundOrder.Trades);
            _tradePositions = new();
            _futuresEodData = new();
            _optionLegDataMap = new();
            _spreadPaths = new();
            _spreadPathQueue = new();
            _siteId = _appRoot.GetModel<EventModel>().SiteId;
            _futuresEodHistoryLoaded = false;
            _tradePositionUpdateResetEvent = new(false);
            _liveStreamsIds = new();
        }

        public IAppRoot AppRoot => _appRoot;
        public FundReadModel Fund => _fund;
        public FundOrderReadModel FundOrder => _fundOrder;
        public FundOrderTradeReadModel FundOrderTrade => _fundOrderTrade;
        public DateTime? ValueDate => _valueDate;
        public ICollection<FuturesContractViewModel> BaseContracts => _baseContracts;
        public int OrderId => _fundOrder.OrderId;
        public int TradeId => _fundOrderTrade.TradeId;
        public object[] LiveFeedLabels => new object[] { LiveFeedOff, LiveFeedOn };
        public bool IsLiveFeedEnabled => _liveFeedEnabled;

        public TradeType PutSpreadTradeType => _fundOrderTrade.TradeType == TradeType.ShortIronCondor ? TradeType.PutCreditSpread : TradeType.PutDebitSpread;
        public TradeType CallSpreadTradeType => _fundOrderTrade.TradeType == TradeType.ShortIronCondor ? TradeType.CallCreditSpread : TradeType.CallDebitSpread;
        public OptionLegAction ShortOptionLegAction => _fundOrderTrade.TradeType == TradeType.ShortIronCondor ? OptionLegAction.Short : OptionLegAction.Long;
        public OptionLegAction LongOptionLegAction => _fundOrderTrade.TradeType == TradeType.ShortIronCondor ? OptionLegAction.Long : OptionLegAction.Short;
        public OptionLegAction GetShortPutOptionLegAction(TradeType tradeType) => tradeType == TradeType.PutCreditSpread ? OptionLegAction.Short : OptionLegAction.Long;
        public OptionLegAction GetLongPutOptionLegAction(TradeType tradeType) => tradeType == TradeType.PutCreditSpread ? OptionLegAction.Long : OptionLegAction.Short;
        public OptionLegAction GetShortCallOptionLegAction(TradeType tradeType) => tradeType == TradeType.CallCreditSpread ? OptionLegAction.Short : OptionLegAction.Long;
        public OptionLegAction GetLongCallOptionLegAction(TradeType tradeType) => tradeType == TradeType.CallCreditSpread ? OptionLegAction.Long : OptionLegAction.Short;
        public void TradePositionUpdated() => _tradePositionUpdateResetEvent.Set();

        public Action<string, string> ShowErrorMessage { get; set; }
        public Action<FuturesEodDataViewModel[]> OnFuturesEodDataHistoryLoaded { get; set; }
        public Action<FuturesEodDataViewModel> OnFuturesEodDataLoaded { get; set; }
        public Action<ICollection<TradeInfoReadModel>> OnTradeInfoLoaded { get; set; }
        public Action<int, (TradeLimitReadModel TradeLimit, decimal FundBalance)> OnTradeLimitsLoaded { get; set; }
        public Action<TradePositionId, (TradePositionReadModel PutCreditSpread, TradePositionReadModel CallCreditSpread), TradeLimitReadModel, decimal, decimal> OnIronCondorTradePositionsLoaded { get; set; }
        public Action<TradePositionId, (TradePositionReadModel PutCreditSpread, TradePositionReadModel CallCreditSpread), TradeLimitReadModel, decimal, decimal> OnIronCondorSpreadPathsLoaded { get; set; }
        public Action<OptionTradeSpreadBarUIViewModel[]> OnOptionTradeSpreadBarDataLoaded { get; set; }
        public Action<TradeHistoryReadModel[]> OnTradeHistoryLoaded { get; set; }
        public Action<TradeHistoryReadModel[]> OnCurrentTradeHistoryLoaded { get; set; }
        public Action<int, int, OptionTradeViewModel> ShowTrade { get; set; }
        public Action<TradePlanReadModel> ShowTradePlan { get; set; }
        public Action<TradePlanReadModel[]> ShowTradePlans { get; set; }
        public Action<Action<bool>> CanResetLiveFeed { get; set; }

        public void EnableMarketDataFeedResetListener()
            => _appRoot.GetModel<MarketDataFeedCommandModel>().Execute(async model =>
                await model.StartMarketDataFeedResetListenerAsync(_ => {
                   if (_liveFeedEnabled)
                   {
                       DisableLiveFeed();
                       Task.Delay(TimeSpan.FromSeconds(5)).Wait();
                       EnableLiveFeed();
                       DeleteOptionTradeSpreadBarData();
                   }
                }));

        private void DeleteOptionTradeSpreadBarData()
            => _appRoot.GetModel<TradeCommandModel>().Execute(async model => {
                model.OnError((errorCode, errorMsg) => ShowErrorMessage(errorMsg, $"{errorCode}: Delete Option Trade Spread Bar Data Error"));
                var optionTradeId = OptionTradeId.Create(_fundOrderTrade.OrderId, _fundOrderTrade.TradeId);
                await model.DeleteOptionTradeSpreadBarDataAsync(optionTradeId, _fundOrderTrade.TradeType, _valueDate.HasValue? _valueDate.Value: DateTime.Now.Date);
            });

        /// <summary>
        /// disable market data feed listener
        /// </summary>
        public void DisableMarketDataFeedResetListener()  
            => _appRoot.GetModel<MarketDataFeedCommandModel>().Execute(async model => 
                await model.StopMarketDataFeedResetListenerAsync());

        /// <summary>
        /// load iron condor trade from storage
        /// </summary>
        /// <param name="onLoadComplete"></param>
        public void LoadIronCondorTrade(Action<int, int, OptionTradeViewModel> onLoadComplete)
        {
            if (_fundOrderTrades?.Count > 0)
                _appRoot.GetModel<TradeQueryModel>().Execute(async model => {
                    model.OnError((_, errorMsg) => ShowErrorMessage(errorMsg, "Loading Iron Condor Trade Error"));
                    var orderId = _fundOrder.OrderId;
                    var tradeId = _fundOrderTrade.TradeId;
                    await model.GetOptionTradeAsync(orderId, tradeId, optionTrade => {
                        onLoadComplete?.Invoke(orderId, tradeId, optionTrade);
                    });
                });
        }

        /// <summary>
        /// load complete iron condor trade info from storage and display trade info
        /// </summary>
        /// <param name="trade"></param>
        /// <param name="orderId"></param>
        /// <param name="tradeId"></param>
         public void LoadIronCondorTrade(OptionTradeViewModel trade, int orderId, int tradeId)
        {
            _optionTrade = trade;
            LoadFuturesEodDataHistory();
            LoadTradeInfo();
            LoadTradePositions();
            LoadRiskFreeRate();
            LoadTradeHistory();
            LoadTradeLimits(orderId, tradeId);
            LoadIronCondorTradePlans();
            return;

            void LoadTradeInfo()
                => _appRoot.GetModel<TradeQueryModel>().Execute(async model => {
                    model.OnError((errorCode, errorMsg) => ShowErrorMessage(errorMsg, $"{errorCode}: Loading Iron Condor Trade Info Error"));
                    await model.GetTradeInfoAsync(_fundOrderTrades, tradeInfo => {
                        _tradeInfo = new (tradeInfo);
                        OnTradeInfoLoaded?.Invoke(tradeInfo);
                    });
                });
 
            void LoadTradePositions()
                => _appRoot.GetModel<TradeQueryModel>().Execute(async model => {
                    model.OnError((_, errorMsg) => ShowErrorMessage(errorMsg, "Loading Iron Condor Trade Positions Error"));
                    await model.GetTradePositionsAsync(orderId, tradeId, tradePositions => {
                        _tradePositions = new (tradePositions);
                    });
                });

            void LoadRiskFreeRate()
                => _appRoot.GetModel<MarketDataQueryModel>().Execute(async model => {
                    model.OnError((_, errorMsg) => ShowErrorMessage(errorMsg, "Loading Iron Condor Risk Free Rate Error"));
                    await model.GetRiskFreeRateAsync(riskFreeRate => _riskFreeRate = riskFreeRate);
                });
        }

        void LoadIronCondorTradePlans()
               => _appRoot.GetModel<TradePlanQueryModel>().Execute(async model => {
                   var valueDate = _valueDate.HasValue ? _valueDate.Value : DateTime.Now.Date;
                   model.OnError((_, errorMessage) => this.ShowErrorMessage(errorMessage, "Loading Iron Condor Trade Plans Error"));
                   await model.GetTradePlansAsync(_fundOrder.OrderId, _fundOrderTrade.TradeId, valueDate, tradePlans => {
                       if (tradePlans is not null)
                           ShowTradePlans?.Invoke(tradePlans);
                   });
               });

        /// <summary>
        /// load trade history from storage
        /// </summary>
        public void LoadTradeHistory()
          => _appRoot.GetModel<TradeQueryModel>().Execute(async model => {
              model.OnError((_, errorMsg) => ShowErrorMessage(errorMsg, "Loading Iron Condor Trade History Error"));
              await model.GetTradeHistoryAsync(_optionTrade.OrderId, tradeHistory =>
              {
                  _tradeHistory = new (tradeHistory);
                  OnTradeHistoryLoaded?.Invoke(tradeHistory);
              });
          });

        /// <summary>
        /// load iron condor trade position
        /// </summary>
        /// <param name="index"></param>
        public void LoadIronCondorTradePosition(int index)
        {
            if (index < 0 || index >= _tradeHistory.Count) 
                return;
            var orderId = _tradeHistory[index].OrderId;
            var tradeId = _tradeHistory[index].TradeId;
            var tradeType = _tradeHistory[index].TradeType;
            var tradeStatus = _tradeHistory[index].TradeStatus;
            var daysToExpiry = _tradeHistory[index].DaysToExpiry;
            var valueDate = _tradeHistory[index].ValueDate;
            var key = new TradePositionId(orderId, tradeId, tradeType, valueDate, daysToExpiry, tradeStatus);
            _appRoot.GetModel<TradeQueryModel>().Execute(async model =>
            {
                if (_tradeHistory is null || _tradeHistory.Count == 0)
                    return;
                var openingOrderId = _tradeHistory[0].OrderId;
                var openingTradeId = _tradeHistory[0].TradeId;
                var openingTradeStatus = _tradeHistory[0].TradeStatus;
                var openingDaysToExpiry = _tradeHistory[0].DaysToExpiry;
                var openingValueDate = _tradeHistory[0].ValueDate;
                await model.GetTradePositionTradeTypesAsync(openingOrderId, openingTradeId, openingValueDate, openingDaysToExpiry, openingTradeStatus, async openingTradeTypes =>
                {
                    if (openingTradeTypes?.Length == 2)
                    {
                        var putSpreadTradeTypeValue = Enum.TryParse<TradeType>(
                            openingTradeTypes.Where(e => e.ToLower().StartsWith("put")).SingleOrDefault(), out var putSpreadTradeType);
                        var callSpreadTradeTypeValue = Enum.TryParse<TradeType>(
                            openingTradeTypes.Where(e => e.ToLower().StartsWith("call")).SingleOrDefault(), out var callSpreadTradeType);
                        var openingNetSpread = await model.GetIronCondorNetSpreadAsync(
                            openingOrderId, 
                            openingTradeId, 
                            putSpreadTradeType, 
                            callSpreadTradeType, 
                            openingValueDate, 
                            openingDaysToExpiry, 
                            openingTradeStatus);

                        await model.GetTradePositionTradeTypesAsync(orderId, tradeId, valueDate, daysToExpiry, tradeStatus, async tradeTypes =>
                        {
                            if (tradeTypes?.Length == 2)
                            {
                                putSpreadTradeTypeValue = Enum.TryParse<TradeType>(
                                    tradeTypes.Where(e => e.ToLower().StartsWith("put")).SingleOrDefault(), out var putSpreadTradeType);
                                callSpreadTradeTypeValue = Enum.TryParse<TradeType>(
                                    tradeTypes.Where(e => e.ToLower().StartsWith("call")).SingleOrDefault(), out var callSpreadTradeType);
                                await model.GetIronCondorTradePositionsAsync(
                                    orderId: orderId,
                                    tradeId: tradeId,
                                    valueDate: valueDate,
                                    daysToExpiry: daysToExpiry,
                                    tradeStatus: tradeStatus,
                                    putSpreadTradeType: putSpreadTradeType,
                                    callSpreadTradeType: callSpreadTradeType,
                                    onViewAction: ironCondorTradePositions => OnIronCondorTradePositionsLoaded?.Invoke(key, ironCondorTradePositions, _optionTrade.TradeLimit, openingNetSpread, _fundBalance));
                            }
                        });
                    }

                });

            });
     
        }

        public void LoadTradePlanAction(int index)
            => _appRoot.GetModel<TradePlanQueryModel>().Execute(async model => {
                var valueDate = _tradeHistory[index].ValueDate;
                var orderId = _tradeHistory[index].OrderId;
                var tradeId = _tradeHistory[index].TradeId;
                model.OnError((_, errorMessage) => this.ShowErrorMessage(errorMessage, "Trade Plan Action Listener Error"));
                await model.GetTradePlansAsync(orderId, tradeId, valueDate, tradePlans => {
                     if (tradePlans is not null)
                        ShowTradePlans?.Invoke(tradePlans);
                });
            });

        void LoadFuturesEodDataHistory()
        {
            if (_futuresEodHistoryLoaded)
                return;
            _appRoot.GetModel<MarketDataQueryModel>().Execute(async model =>
                await model.GetFuturesContractAsync(_optionTrade.UnderlyingContractId, futuresContract =>
                {
                    _futuresContract = futuresContract;
                    _appRoot.GetModel<MarketDataFeedQueryModel>().Execute(async mktDataFeedQueryModel =>
                    {
                        var valueDate = _valueDate.HasValue ? _valueDate.Value.Date : DateTime.Now.Date;
                        await mktDataFeedQueryModel.GetFuturesEodDataAsync(futuresContract.ContractId, valueDate.AddMonths(-2), valueDate, futuresEodData =>
                        {
                            if (futuresEodData == null || futuresEodData.Length == 0) return;
                            OnFuturesEodDataLoaded?.Invoke(futuresEodData.First());
                            _futuresEodData.AddRange(futuresEodData);
                            OnFuturesEodDataHistoryLoaded?.Invoke(_futuresEodData.Skip(1).ToArray());
                            _futuresEodHistoryLoaded = true;
                        });
                    });
                }));
        }

        public void LoadFuturesEodData(int index)
           => _appRoot.GetModel<MarketDataQueryModel>().Execute(async mktDataModel =>
               await mktDataModel.GetFuturesContractAsync(_optionTrade.UnderlyingContractId, futuresContract => {
                   _futuresContract = futuresContract;
                   _appRoot.GetModel<MarketDataFeedQueryModel>().Execute(async mktDataFeedQueryModel => {
                       if (index >= 0 && index < _tradeHistory.Count)
                       {
                           var valueDate = _tradeHistory[index].ValueDate;
                           await mktDataFeedQueryModel.GetFuturesEodDataAsync(futuresContract.ContractId, valueDate.AddMonths(-2), valueDate, futuresEodData =>
                           {
                               if (futuresEodData == null || futuresEodData.Length == 0) 
                                   return;
                               OnFuturesEodDataLoaded?.Invoke(futuresEodData.First());
                           });
                       }
                   });
               }));

        void LoadTradeLimits(int orderId, int tradeId)
            => _appRoot.Execute(async () => {
                var tradeModel = _appRoot.GetModel<TradeQueryModel>();
                var tradeLimit = default(TradeLimitReadModel);
                await tradeModel.GetTradeLimitsAsync(tradeId, e => tradeLimit = e);
                var fundModel = _appRoot.GetModel<FundQueryModel>();
                await fundModel.GetFundBalanceAsync(_fundOrder.FundId, fundBalance =>
                {
                    _tradeLimits = tradeLimit;
                    _fundBalance = fundBalance;
                    OnTradeLimitsLoaded?.Invoke(orderId, (tradeLimit, fundBalance));
                });
            });

        void LoadOptionTradeSpreadBarData(
            int orderId,
            int tradeId, 
            TradeType tradeType,
            DateTime valueDate,
            DateTime startDate,
            DateTime endDate)
            => _appRoot.Execute(async () => {
                var model = _appRoot.GetModel<TradeQueryModel>();
                await model.GetOptionTradeSpreadBarDataAsync(orderId, tradeId, tradeType, valueDate, startDate, endDate, 
                    async optionTradeSpreadBarData => {
                        var optionTradeId = OptionTradeId.Create(orderId, tradeId);
                        await model.GetIronCondorMDILimitAsync(optionTradeId, valueDate, ironCondorMDILimit =>
                        {
                            var optionTradeSpreadBarUIData = optionTradeSpreadBarData
                                .Select(e => new OptionTradeSpreadBarUIViewModel(e, ironCondorMDILimit))
                                .ToArray();
                            OnOptionTradeSpreadBarDataLoaded?.Invoke(optionTradeSpreadBarUIData);
                        });
                    });
            });

        /// <summary>
        /// return list of option contract id's
        /// </summary>
        /// <returns></returns>
        public ICollection<string> GetOptionLegContractIds() => _optionTrade.OptionLegs.Select(e => e.ContractId).ToList();

        /// <summary>
        /// return list of trades with current trade order
        /// </summary>
        /// <returns></returns>
        public int GetTradeInfoCount() => _tradeInfo?.Count ?? 0;

        /// <summary>
        /// return current trade pnl value
        /// </summary>
        /// <returns></returns>
        public decimal GetTradePnl(TradePositionReadModel pcsIntraDay, TradePositionReadModel ccsIntraDay, int intraDaySign)
        {
            var eodTradePnl = _optionTrade.TradePositions.GetEodTradePnl();
            var pcsTradePnl = pcsIntraDay is null || pcsIntraDay.TradeStatus == TradeStatus.EndOfDay ? 0 :  (intraDaySign * pcsIntraDay.TradePnl) -  pcsIntraDay.Commission;
            var ccsTradePnl = ccsIntraDay is null || ccsIntraDay.TradeStatus == TradeStatus.EndOfDay ? 0 :  (intraDaySign * ccsIntraDay.TradePnl) -  ccsIntraDay.Commission;
            return eodTradePnl + pcsTradePnl + ccsTradePnl;
        }

        public decimal GetEodTradePnl() => _optionTrade.TradePositions.GetEodTradePnl();
        public decimal GetTradePnl() => _tradeHistory is null ? 0 : _tradeHistory.Sum(e => e.TradePnl);

        /// <summary>
        /// reload current iron condor trade when data has been changed within trade
        /// </summary>
        /// <param name="onErrorMessage"></param>
        public void ReloadIronCondorTrade()
        {
            LoadValueDate();
            return;

            void LoadValueDate()
                => _appRoot.GetModel<MarketDataQueryModel>().Execute(async model => {
                    model.OnError((errorCode, errorMsg) => ShowErrorMessage(errorMsg, "Unable to connect to IFM servers"));
                       await model.GetValueDateAsync(valueDate => {
                           _valueDate = valueDate;
                           LoadOptionTrade(_fundOrder.OrderId, _fundOrderTrade.TradeId);
                       });
                });

            void LoadOptionTrade(int orderId, int tradeId)
                => _appRoot.GetModel<TradeQueryModel>().Execute(async model => {
                    model.OnError((_, errorMsg) => ShowErrorMessage(errorMsg, "Reloading Iron Condor Trade Error"));
                    await model.GetOptionTradeAsync(orderId, tradeId, optionTrade =>
                    {
                        LoadIronCondorTrade(optionTrade, orderId, tradeId);
                        LoadTradeLimits(orderId, tradeId);
                    });
                });
        }

        /// <summary>
        /// enable live market data feeds
        /// </summary>
        public void EnableLiveFeed()
        {
            if (_liveFeedEnabled) return;
            LoadValueDate(() => {
                EnableFuturesEodDataListener();
                EnableFuturesOptionTickDataListener();
                EnableTradePositionListener();
                EnableTradePlanListener();
                EnableOptionTradeSpreadBarDataListener();
                EnableTradeLiveFeed();
                UpdateDailyProfitTarget();
            });
            return;

            void LoadValueDate(Action onDataLoad)
                =>_appRoot.GetModel<MarketDataQueryModel>().Execute(async model => {
                    model.OnError((_, errorMessage) => ShowErrorMessage(errorMessage, "Load Value Date Error"));
                    await model.GetValueDateAsync(valueDate => {
                        _valueDate = valueDate;
                        onDataLoad();
                    });
                });

            void DeleteSpreadDistributionJobsInProgress()
                => _appRoot.GetModel<SpreadDistributionJobModel>().Execute(async model => {
                    model.OnError((_, errorMessage) => ShowErrorMessage(errorMessage, "Delete Spread Distribution Jobs In Progress Error"));
                    await model.DeleteSpreadDistributionJobsInProgressAsync(OptionTradeId.Create(OrderId, TradeId));
                });

            void EnableTimers()
            {
                _snapshotTimer = new Timer(_ => snapshotTimer_Tick(), null, TimeSpan.Zero, TimeSpan.FromSeconds(15));
                _spreadBarDataTimer = new Timer(_ => spreadBarDataTimer_Tick(), null, TimeSpan.Zero, TimeSpan.FromSeconds(15));
            }

            void EnableTradeLiveFeed()
                => _appRoot.GetModel<MarketDataFeedCommandModel>().Execute(async model => {
                    model.OnError((_, errorMessage) => ShowErrorMessage(errorMessage, "Enable Trade Live Feed Error"));
                    var optionContractIds = _optionTrade.OptionLegs.OrderByDescending(e => e.ContractId).Select(e => e.ContractId.Trim()).ToArray();
                    foreach(var  contractId in optionContractIds)
                        _liveStreamsIds.Add(new StreamId(Guid.NewGuid()), contractId);
                    var baseContract = _baseContracts.Where(e => e.Id.Symbol == _fundOrderTrade.BaseContractSymbol.Trim()).FirstOrDefault();
                    await model.StartStreamingFuturesOptionTickDataAsync(_liveStreamsIds, baseContract, _valueDate.Value, _optionTrade.MaturityDate, _riskFreeRate,
                        () => DeleteSpreadDistributionJobsInProgress());
                    EnableTimers();
                    _liveFeedEnabled = true;
                });

            void EnableFuturesEodDataListener()
                => _appRoot.GetModel<MarketDataFeedCommandModel>().Execute(async model => {
                    model.OnError((_, errorMessage) => ShowErrorMessage(errorMessage, "Delete Spread Distribution Jobs In Prgoress Error"));
                    await model.StartFuturesEodDataEventConsumerAsync(_siteId, e =>
                    {
                        OnFuturesEodDataLoaded?.Invoke(e.FuturesEodData);
                        GenerateSpreadDistribution();
                    });
                });
            
            void EnableFuturesOptionTickDataListener()
            {
                _appRoot.GetModel<MarketDataFeedCommandModel>().Execute(async model =>
                {
                    model.OnError((_, errorMessage) => ShowErrorMessage(errorMessage, "Enable Futures Option Tick Data Listener Error"));
                    await model.StartFuturesOptionTickDataListenerAsync(async e => await model.ExecuteAsync(async () => await OnFuturesOptionTickDataUpdateAsync(e)));
                });

                async Task OnFuturesOptionTickDataUpdateAsync(FuturesOptionTickDataUpdatedEvent e)
                {
                    try
                    {
                        if (e is null)
                            return;
                        var optionTickData = e.OptionTickData;
                        var optionLeg = _optionTrade.OptionLegs.Where(o => o.ContractId == optionTickData.ContractId).SingleOrDefault();
                        if (optionLeg is null)
                            return;
                        var tradePostionKey = new TradePositionId(
                            orderId: _optionTrade.OrderId,
                            tradeId: _optionTrade.TradeId,
                            tradeType: GetTradePositionTradeType(optionLeg.OptionLegType),
                            valueDate: _valueDate.Value,
                            daysToExpiry: (_optionTrade.MaturityDate - _valueDate.Value).Days,
                            tradeStatus: TradeStatus.IntraDay
                        );

                        // get option trade data key for selected option tick data...
                        var optionLegData = new OptionLegDataReadModel(
                            TradeId: tradePostionKey.TradeId,
                            TradeType: tradePostionKey.TradeType,
                            ValueDate: tradePostionKey.ValueDate,
                            DaysToExpiry: tradePostionKey.DaysToExpiry,
                            TradeStatus: tradePostionKey.TradeStatus,
                            OptionLegId: optionLeg.ContractId,
                            BidPrice: Convert.ToDecimal(optionTickData.BidPrice),
                            AskPrice: Convert.ToDecimal(optionTickData.AskPrice),
                            ImpliedVolatility: optionTickData.ImpliedVolatility,
                            Delta: optionTickData.Delta,
                            Gamma: optionTickData.Gamma,
                            Theta: optionTickData.Theta,
                            Vega: optionTickData.Vega,
                            Rho: optionTickData.Rho,
                            CreatedOn: DateTime.Now,
                            CreatedBy: Environment.UserName,
                            UpdatedOn: DateTime.Now,
                            UpdatedBy: Environment.UserName
                        ).SetOptionLeg(optionLeg);

                        var tradeModel = _appRoot.GetModel<TradeCommandModel>();
                        if (_optionLegDataMap.ContainsKey(optionLeg.ContractId))
                        {
                            if (_optionLegDataMap[optionLeg.ContractId].OptionPrice != optionLegData.OptionPrice)
                            {
                                _optionLegDataMap.Remove(optionLeg.ContractId);
                                _optionLegDataMap.Add(optionLeg.ContractId, optionLegData);
                                await tradeModel.ChangeOptionLegDataAsync(
                                    orderId: _optionTrade.OrderId,
                                    tradeId: _optionTrade.TradeId,
                                    key: tradePostionKey,
                                    assetPrice: Convert.ToDecimal(optionTickData.UnderlyingPrice),
                                    riskFreeRate: _riskFreeRate,
                                    optionLegData: optionLegData
                                );
                            }
                        }
                        else
                        {
                            _optionLegDataMap.Add(optionLeg.ContractId, optionLegData);
                            await tradeModel.ChangeOptionLegDataAsync(
                                orderId: _optionTrade.OrderId,
                                tradeId: _optionTrade.TradeId,
                                key: tradePostionKey,
                                assetPrice: Convert.ToDecimal(optionTickData.UnderlyingPrice),
                                riskFreeRate: _riskFreeRate,
                                optionLegData: optionLegData
                            );
                        }
                    }
                    catch { }
                }
            }

            void EnableTradePositionListener()
            {
                _tradePositionQueue = new ConcurrentAsyncEventQueue<TradePositionChangeSourceReadModel>(tradePosition => {
                    if (OnTradePositionUpdate(tradePosition))
                        _tradePositionUpdateResetEvent.WaitOne();
                    return Task.CompletedTask;
                }, delayAfterReadInMs: TimeSpan.FromMilliseconds(50).TotalMilliseconds);
                _tradePositionQueue.Start();
                _appRoot.GetModel<TradePositionFeedEventModel>().Execute( async model => {
                    model.OnError((_, errorMessage) => ShowErrorMessage(errorMessage, "Enable Trade Position Listener Error"));
                    await model.StartTradePositionListenerAsync(e => {
                        _tradePositionQueue?.EnqueueAndSignal(new TradePositionChangeSourceReadModel(e.PutTradePosition, e.CallTradePosition, e.TradePositionChangeSource, e.OptionLegId));
                    });
                });
                return;

                bool OnTradePositionUpdate(TradePositionChangeSourceReadModel e)
                {
                    try
                    {
                        if (e.PutTradePosition?.OptionLegData is null || e.PutTradePosition?.Key?.TradeStatus != TradeStatus.IntraDay
                            || e.CallTradePosition?.OptionLegData is null || e.CallTradePosition?.Key?.TradeStatus != TradeStatus.IntraDay)
                            return false;
                        var putCreditSpread = _optionTrade.TradePositions.Get(e.PutTradePosition.Key.FromTradeType(GetTradePositionTradeType(OptionType.Put)));
                        var callCreditSpread = _optionTrade.TradePositions.Get(e.CallTradePosition.Key.FromTradeType(GetTradePositionTradeType(OptionType.Call)));
                        if ((putCreditSpread?.OptionLegData?.Length ?? 0) != 2 || (callCreditSpread?.OptionLegData?.Length ?? 0) != 2)
                        {
                            ReloadIronCondorTrade();
                            return false;
                        }
                        else
                        {
                            switch (e.TradePositionChangeSource)
                            {
                                case TradePositionChangeSourceType.PutCreditSpreadLeg:
                                    _optionTrade.TradePositions.Set(e.PutTradePosition);
                                    break;
                                case TradePositionChangeSourceType.CallCreditSpreadLeg:
                                    _optionTrade.TradePositions.Set(e.CallTradePosition);
                                    break;
                                case TradePositionChangeSourceType.SpreadDistributionStatistics:
                                    _optionTrade.TradePositions.Set(e.PutTradePosition);
                                    _optionTrade.TradePositions.Set(e.CallTradePosition);
                                    break;
                                default:
                                    return false;
                            }
                            putCreditSpread = e.PutTradePosition;
                            callCreditSpread = e.CallTradePosition;
                            var spreads = (PutCreditSpread: putCreditSpread, CallCreditSpread: callCreditSpread);
                            var netSpreadPrice = Math.Abs(Math.Abs(putCreditSpread?.NetSpread ?? 0m) + Math.Abs(callCreditSpread?.NetSpread ?? 0m));
                            netSpreadPrice = netSpreadPrice < 0.0m ? 0.0m : netSpreadPrice;

                            OnIronCondorSpreadPathsLoaded?.Invoke(e.PutTradePosition.Key, spreads, _optionTrade.TradeLimit, netSpreadPrice, _fundBalance);
                            var netForwardPrice = Math.Abs(putCreditSpread?.ForwardPrice ?? 0m) + Math.Abs(callCreditSpread?.ForwardPrice ?? 0m);
                            netForwardPrice = netForwardPrice < 0.0m ? 0.0m : netForwardPrice;
                            InsertOptionTradeSpreadData(netForwardPrice, spreads);
                            LoadCurrentTradeHistory();
                             return true;
                        }
                    }
                    catch
                    {
                    }
                    return false;
                }
                
            }

            /// <summary>
            /// start spread distribution service listener
            /// </summary>
            void EnableSpreadDistributionServiceListener()
                => _appRoot.GetModel<SpreadDistributionJobModel>().Execute(async model => {
                    model.OnError((_, errorMessage) => ShowErrorMessage(errorMessage, "Spread Distribution Service Listener Error"));
                    await model.StartSpreadDistributionJobConsumerAsync();
                });

            /// <summary>
            /// start trade plan listener
            /// </summary>
            void EnableTradePlanListener()
                => _appRoot.GetModel<TradePlanEventModel>().Execute(async model => {
                    model.OnError((_, errorMessage) => ShowErrorMessage(errorMessage, "Trade Plan Listener Error"));
                    await model.StartTradePlanListenerAsync(o => ShowTradePlan?.Invoke(o.TradePlan) );
                });

            ///             
            void EnableOptionTradeSpreadBarDataListener()
            {
                if (!_valueDate.HasValue) return;
                _appRoot.GetModel<OptionTradeSpreadBarDataEventModel>().Execute(async model => {
                    model.OnError((_, errorMessage) => ShowErrorMessage(errorMessage, "Option Trade Spread Bar Data Listener Error"));
                    await model.StartOptionTradeSpreadBarDataListenerAsync(o => 
                        LoadOptionTradeSpreadBarData(
                            orderId: o.OptionTradeSpreadBarData.OrderId,
                            tradeId: o.OptionTradeSpreadBarData.TradeId,
                            tradeType: o.OptionTradeSpreadBarData.TradeType,
                            valueDate: o.OptionTradeSpreadBarData.ValueDate,
                            startDate: DateTime.Now.AddHours(-6),
                            endDate: DateTime.Now)
                    );
                });

            }

            void UpdateDailyProfitTarget()
                => _appRoot.GetModel<MarketDataQueryModel>().Execute(async model => {
                    model.OnError((_, errorMsg) => ShowErrorMessage(errorMsg, "Loading Trade Days Error"));
                    await model.GetTradingDaysAsync(_optionTrade.TradeDate, _valueDate.Value, MarketType.Futures, CurrencyType.USD, async tradingDays =>
                        await model.GetTradingDaysAsync(_optionTrade.TradeDate, _optionTrade.MaturityDate, MarketType.Futures, CurrencyType.USD, maxTradingDays =>
                            _appRoot.GetModel<TradeCommandModel>().Execute(async tradeModel =>
                            {
                                tradeModel.OnError((_, errorMsg) => ShowErrorMessage(errorMsg, "Updating Trade Limit Daily Profit Target Error"));
                                await tradeModel.UpdateTradeLimitDailyProfitTargetAsync(_fundOrder.OrderId, _fundOrderTrade.TradeId, tradingDays, maxTradingDays);
                            })
                        )
                    );
                });

            void LoadCurrentTradeHistory()
                 => _appRoot.GetModel<TradeQueryModel>().Execute(async model => {
                     model.OnError((_, errorMessage) => ShowErrorMessage(errorMessage, "Load Current Trade History Error"));
                     await model.GetTradeHistoryAsync(_optionTrade.OrderId, tradeHistory => {
                         _tradeHistory = new (tradeHistory);
                         OnCurrentTradeHistoryLoaded?.Invoke(tradeHistory);
                     });
                 });

            void GenerateSpreadDistribution(double lossProbabilityFactor = 0)
                => _appRoot.GetModel<SpreadDistributionJobModel>().Execute(async model => {
                    model.OnError((errorCode, errorMsg) => WriteStatusConsole(errorCode, errorMsg));
                    await model.IsSpreadDistributionJobInProgressAsync(OrderId, TradeId, async jobInProgress => {
                        if (!jobInProgress)
                        {
                            await model.SubmitSpreadDistributionJobAsync(new SpreadDistributionJobReadModel
                            (
                                JobId: Math.Abs(Guid.NewGuid().GetHashCode()),
                                OrderId: _optionTrade.OrderId,
                                TradeId: _optionTrade.TradeId,
                                TradeType: _optionTrade.TradeType,
                                TradeStatus: TradeStatus.IntraDay,
                                ValueDate: _valueDate.Value,
                                DaysToExpiry: (_optionTrade.MaturityDate - _valueDate.Value).Days,
                                OptionStyle: OptionStyle.American,
                                OptionType: OptionType.Put,
                                JobSubmitted: DateTime.Now,
                                JobStatus: "InProgress",
                                JobCompleted: null,
                                JobFailed: null,
                                InProgress: true,
                                LossProbabilityFactor: lossProbabilityFactor
                            ));
                        }
                    });
             });

            TradeType GetTradePositionTradeType(OptionType optionType)
                 => _optionTrade.TradeType switch
                 {
                     TradeType.ShortIronCondor => optionType == OptionType.Put ? TradeType.PutCreditSpread : TradeType.CallCreditSpread,
                     TradeType.LongIronCondor => optionType == OptionType.Put ? TradeType.PutDebitSpread : TradeType.CallDebitSpread,
                     _ => throw new NotImplementedException()
                 };

        }

        /// <summary>
        /// disable live market data feeds
        /// </summary>
        public void DisableLiveFeed()
        {
            DisableOptionTradeSpreadBarDataListener();
            DisableTradePlanListener();
            DisableFuturesEodDataListener();
            DisableFuturesOptionTickDataListener();
            DisableTradePositionListener();
            DisableTradeLiveFeed();
            return;

            void DisableTimers()
            {
                _snapshotTimer = null;
                _spreadBarDataTimer = null;
            }

            void DisableTradeLiveFeed()
                => _appRoot.GetModel<MarketDataFeedCommandModel>().Execute(async model => {
                    model.OnError((_, errorMessage) => ShowErrorMessage(errorMessage, "Disable Trade Live Feed Error"));
                    if (_optionTrade is not null)
                    {
                        foreach (var e in _liveStreamsIds)
                        {
                            var streamId = e.Key;
                            var contractId = e.Value;
                            await model.StopStreamingFuturesOptionTickDataAsync(streamId, contractId);
                        }
                        _liveStreamsIds = new();
                    }
                    DisableTimers();
                    _liveFeedEnabled = false;
                });

            void DisableFuturesEodDataListener() 
                => _appRoot.GetModel<MarketDataFeedCommandModel>().Execute( async model => await model.StopFuturesEodDataEventConsumerAsync(_siteId));
            
            void DisableFuturesOptionTickDataListener() 
                => _appRoot.GetModel<MarketDataFeedCommandModel>().Execute(async model => await model.StopFuturesOptionTickDataListenerAsync());

            void DisableTradePositionListener()
            {
                if (_tradePositionQueue == null) return;
                _tradePositionQueue?.Stop();
                _appRoot.GetModel<TradePositionFeedEventModel>().Execute(async model => await model.StopTradePositionListenerAsync());
                _tradePositionQueue = null;
            }

            void DisableTradePlanListener()
            {
                if (_tradePlanConsoleQueue == null) return;
                _tradePlanConsoleQueue?.Stop();
                _appRoot.GetModel<TradePlanEventModel>().Execute(async model => await model.StopTradePlanListenerAsync());
                _tradePlanConsoleQueue = null;
            }

            void DisableSpreadDistributionServiceListener()
               => _appRoot.GetModel<SpreadDistributionJobModel>().Execute(async model => {
                   model.OnError((errorCode, errorMessage) => ShowErrorMessage(errorMessage, "Spread Distribution Service Listener Error"));
                   await model.StopSpreadDistributionJobConsumerAsync();
               });

            void DisableOptionTradeSpreadBarDataListener()
                => _appRoot.GetModel<OptionTradeSpreadBarDataEventModel>()
                    .Execute(async model => await model.StopOptionTradeSpreadBarDataListenerAsync());
        }

        public void InsertOptionTradeSpreadData(decimal netForwardPrice, (TradePositionReadModel PutCreditSpread, TradePositionReadModel CallCreditSpread) e)
           => _appRoot.GetModel<TradeCommandModel>()
                .Execute(async model => {
                    model.OnError((errorCode, errorMessage) => ShowErrorMessage(errorMessage, "Insert Option Trade Spread Data Error"));
                    var optionTradeSpreadData = GetOptionTradeSpreadData(netForwardPrice, e);
                    await model.InsertOptionTradeSpreadDataAsync(optionTradeSpreadData);
                });

        OptionTradeSpreadDataViewModel GetOptionTradeSpreadData(decimal netForwardPrice, (TradePositionReadModel PutCreditSpread, TradePositionReadModel CallCreditSpread) e)
                => new OptionTradeSpreadDataViewModel(
                    OrderId: _optionTrade.OrderId,
                    TradeId: _optionTrade.TradeId,
                    TradeType: _optionTrade.TradeType,
                    ValueDate: _valueDate.HasValue ? _valueDate.Value : DateTime.Now,
                    LossLimit: _tradeLimits.MaxLossLimit,
                    WinLimit: _tradeLimits.MaxProfitLimit,
                    ForwardSpread: netForwardPrice,
                    NetSpread: Math.Abs((e.PutCreditSpread?.NetSpread ?? 0m) + (e.CallCreditSpread?.NetSpread ?? 0m)),
                    CreatedOn: DateTime.Now,
                    CreatedBy: string.Empty);

        /// <summary>
        /// check every minute to snapshot trade data
        /// </summary>
        void snapshotTimer_Tick()
            // only execute if we are in trading hours...
            => _appRoot.GetModel<MarketDataQueryModel>().Execute(async model => {
                await model.GetValueDateAsync(valueDate => {
                    if (valueDate.HasValue)
                        CanResetLiveFeed(resetLiveFeed => {
                            if (resetLiveFeed)
                            {
                                _appRoot.GetModel<TradeCommandModel>()
                                    .Execute(async model => await model.SnapshotOptionTradeAsync(_optionTrade.OrderId, _optionTrade.TradeId));
                                WriteStatusConsole($"SnapshotOptionTrade executed for {_optionTrade.OrderId}:{_optionTrade.TradeId}");
                            }
                        });
                });
            });

        /// <summary>
        /// check every minute to insert option trade spread bar data
        /// </summary>
        void spreadBarDataTimer_Tick()
            => _appRoot.GetModel<TradeQueryModel>().Execute(async model => {
                var valueDate = _valueDate.HasValue ? _valueDate.Value : DateTime.Now;
                await model.GetOptionTradeSpreadDataAsync(_optionTrade.OrderId, _optionTrade.TradeId, _optionTrade.TradeType, valueDate, e => {
                    if (e is not null)
                    {
                        var optionTradeSpreadBarData = new OptionTradeSpreadBarDataViewModel(
                            OrderId: e.OrderId,
                            TradeId: e.TradeId,
                            TradeType: e.TradeType,
                            ValueDate: valueDate,
                            BarDate: DateTime.Now,
                            LossLimit: e.LossLimit,
                            WinLimit: e.WinLimit,
                            ForwardSpread: e.ForwardSpread,
                            NetSpread: e.NetSpread);
                        _appRoot.GetModel<TradeCommandModel>()
                            .Execute(async model => await model.InsertOptionTradeSpreadBarDataAsync(optionTradeSpreadBarData));
                    }
                });
            });

        void WriteStatusConsole(int errorCode, string errorMessage) 
            => _appRoot.GetModel<StatusConsoleModel>().Execute(async model => 
                await model.WriteConsoleAsync(LogSourceType.Trade, errorCode, errorMessage));

        void WriteStatusConsole(string statusMessage)
            => _appRoot.GetModel<StatusConsoleModel>().Execute(async model =>
                await model.WriteConsoleAsync(LogSourceType.Trade, statusMessage));
    }
}
